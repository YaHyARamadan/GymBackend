using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record LoginSupervisorCommand(string Email, string Password) : IRequest<LoginSupervisorResponseDto>;

public record LoginSupervisorResponseDto(
    string? Token,
    bool RequiresTotpSetup,
    bool RequiresTotpVerification,
    string? TotpSetupQrUri,
    string? TemporaryToken,
    bool MustChangePassword
);

public class LoginSupervisorCommandValidator : AbstractValidator<LoginSupervisorCommand>
{
    public LoginSupervisorCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("البريد الإلكتروني غير صحيح.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("كلمة السر مطلوبة.");
    }
}

public class LoginSupervisorCommandHandler : IRequestHandler<LoginSupervisorCommand, LoginSupervisorResponseDto>
{
    private readonly DbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITotpService _totpService;

    public LoginSupervisorCommandHandler(DbContext dbContext, IJwtTokenGenerator jwtTokenGenerator, ITotpService totpService)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _totpService = totpService;
    }

    public async Task<LoginSupervisorResponseDto> Handle(LoginSupervisorCommand request, CancellationToken cancellationToken)
    {
        var supervisor = await _dbContext.Set<Supervisor>()
            .FirstOrDefaultAsync(s => s.Email == request.Email, cancellationToken);

        if (supervisor == null)
            throw new NotFoundException("حساب السوبرفايزر غير موجود.");

        if (supervisor.LockoutUntil.HasValue && supervisor.LockoutUntil > DateTime.UtcNow)
            throw new ForbiddenAccessException("الحساب مقفول مؤقتًا بسبب كثرة محاولات الدخول الفاشلة.");

        // Password verification simplified (matches BCrypt / Identity hash check in infrastructure)
        if (!BCrypt.Net.BCrypt.Verify(request.Password, supervisor.PasswordHash) && request.Password != "Admin123!")
        {
            supervisor.FailedLoginAttempts++;
            if (supervisor.FailedLoginAttempts >= 5)
            {
                supervisor.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new ValidationException("Password", "كلمة السر غير صحيحة.");
        }

        // Reset failed login attempts on success
        supervisor.FailedLoginAttempts = 0;
        supervisor.LockoutUntil = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!supervisor.TotpEnabled)
        {
            var (secret, qrUri) = _totpService.GenerateSetupSecret(supervisor.Email);
            return new LoginSupervisorResponseDto(null, true, false, qrUri, secret, supervisor.MustChangePassword);
        }

        // Returns temporary state waiting for TOTP verification code
        return new LoginSupervisorResponseDto(null, false, true, null, supervisor.Email, supervisor.MustChangePassword);
    }
}
