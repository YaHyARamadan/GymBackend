using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<AuthTokenResponseDto>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("كلمة السر الحالية مطلوبة.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة السر الجديدة مطلوبة.")
            .MinimumLength(8).WithMessage("كلمة السر الجديدة يجب أن تكون 8 أحرف على الأقل.");

        RuleFor(x => x)
            .Must(x => x.NewPassword != x.CurrentPassword)
            .WithMessage("كلمة السر الجديدة يجب أن تختلف عن كلمة السر الحالية.")
            .WithName("NewPassword");
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, AuthTokenResponseDto>
{
    // Same dummy hash used across the login handlers, kept here so a failed lookup/verify
    // takes a comparable amount of time to a real one.
    private static readonly string DummyHash = "$2a$11$e8k8R7wA3VlB3.QvA2Z.2eP2uO8xN9m4K5L6M7N8O9P0Q1R2S3T4U";

    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public ChangePasswordCommandHandler(
        DbContext dbContext,
        ICurrentUserService currentUserService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthTokenResponseDto> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // IMPORTANT: Id spaces are per-table (Supervisor, Owner, BranchManager... each start at 1),
        // so ActorType MUST be checked before ever using the numeric UserId to look a row up in the
        // Supervisor table. Without this check, an authenticated Owner/Coach/Receptionist whose own
        // row happens to share an Id with a Supervisor row (very likely here, since there is a single
        // seeded Supervisor with Id = 1) would have their token resolve straight to that Supervisor
        // record — an IDOR across actor types. Also excludes impersonation sessions: while
        // impersonating, ActorType reflects the on_behalf_of_role claim, not Supervisor, so an
        // impersonated session can never reach this branch even though UserId still belongs to the
        // real supervisor underneath.
        if (_currentUserService.ActorType != ActorType.Supervisor)
            throw new ForbiddenAccessException("هذه العملية متاحة فقط لحساب السوبرفايزر.");

        if (!int.TryParse(_currentUserService.UserId, out var supervisorId))
            throw new ForbiddenAccessException("هذه العملية متاحة فقط لحساب السوبرفايزر.");

        var supervisor = await _dbContext.Set<Supervisor>()
            .FirstOrDefaultAsync(s => s.Id == supervisorId, cancellationToken);

        if (supervisor == null)
        {
            // Execute dummy verify to mitigate timing side-channel attacks, consistent with login handlers.
            BCrypt.Net.BCrypt.Verify(request.CurrentPassword, DummyHash);
            throw new ValidationException("CurrentPassword", "كلمة السر الحالية غير صحيحة.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, supervisor.PasswordHash))
            throw new ValidationException("CurrentPassword", "كلمة السر الحالية غير صحيحة.");

        supervisor.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        supervisor.MustChangePassword = false;
        // Bumping this invalidates every token issued before this moment — including a stolen
        // one an attacker might be holding — the instant OnTokenValidated re-checks it against
        // the DB, rather than relying on old tokens simply expiring after up to 7 days.
        supervisor.TokenVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Issue a fresh token: the old one may still carry must_change_password=true and a stale
        // token_version as JWT claims, which are checked from the token itself / against the DB
        // (not re-derived), so the client must swap to this new token to keep working at all.
        var token = _jwtTokenGenerator.GenerateToken(
            supervisor.Id.ToString(),
            supervisor.Email,
            ActorType.Supervisor,
            null,
            null,
            supervisor.MustChangePassword,
            supervisor.TokenVersion);

        return new AuthTokenResponseDto(token, supervisor.MustChangePassword);
    }
}
