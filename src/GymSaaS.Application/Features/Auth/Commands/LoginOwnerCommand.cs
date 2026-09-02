using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record LoginOwnerCommand(string Email, string Password) : IRequest<OwnerLoginResponseDto>;

public record OwnerLoginResponseDto(
    string Token,
    bool ContractSigned,
    bool OnboardingCompleted,
    FacilityStatus FacilityStatus
);

public class LoginOwnerCommandValidator : AbstractValidator<LoginOwnerCommand>
{
    public LoginOwnerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginOwnerCommandHandler : IRequestHandler<LoginOwnerCommand, OwnerLoginResponseDto>
{
    private static readonly string DummyHash = "$2a$11$e8k8R7wA3VlB3.QvA2Z.2eP2uO8xN9m4K5L6M7N8O9P0Q1R2S3T4U";

    private readonly DbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginOwnerCommandHandler(DbContext dbContext, IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<OwnerLoginResponseDto> Handle(LoginOwnerCommand request, CancellationToken cancellationToken)
    {
        var owner = await _dbContext.Set<Owner>()
            .IgnoreQueryFilters()
            .Include(o => o.Facility)
            .FirstOrDefaultAsync(o => o.Email == request.Email, cancellationToken);

        if (owner == null)
        {
            // Execute dummy verify to mitigate timing side-channel attacks
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
            throw new ValidationException("Email", "البريد الإلكتروني أو كلمة السر غير صحيحة.");
        }

        if (owner.LockoutUntil.HasValue && owner.LockoutUntil > DateTime.UtcNow)
            throw new ForbiddenAccessException("الحساب مقفول مؤقتًا بسبب كثرة محاولات الدخول الفاشلة.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, owner.PasswordHash))
        {
            owner.FailedLoginAttempts++;
            if (owner.FailedLoginAttempts >= 5)
            {
                owner.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new ValidationException("Email", "البريد الإلكتروني أو كلمة السر غير صحيحة.");
        }

        owner.FailedLoginAttempts = 0;
        owner.LockoutUntil = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // backend.md §3.3: Facility locked completely blocks all roles
        if (owner.Facility.Status == FacilityStatus.Frozen)
        {
            throw new FacilityLockedException();
        }

        var token = _jwtTokenGenerator.GenerateToken(owner.Id.ToString(), owner.Email, ActorType.Owner, owner.FacilityId, null);

        return new OwnerLoginResponseDto(token, owner.ContractSigned, owner.OnboardingCompleted, owner.Facility.Status);
    }
}
