using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record VerifyTotpCommand(string TempToken, string Code) : IRequest<AuthTokenResponseDto>;

public record AuthTokenResponseDto(string Token, bool MustChangePassword);

public class VerifyTotpCommandValidator : AbstractValidator<VerifyTotpCommand>
{
    public VerifyTotpCommandValidator()
    {
        RuleFor(x => x.TempToken).NotEmpty().WithMessage("توكن الجلسة المؤقتة مطلوب.");
        RuleFor(x => x.Code).NotEmpty().Length(6).WithMessage("رمز التحقق يجب أن يكون 6 أرقام.");
    }
}

public class VerifyTotpCommandHandler : IRequestHandler<VerifyTotpCommand, AuthTokenResponseDto>
{
    private readonly DbContext _dbContext;
    private readonly ITotpService _totpService;
    private readonly ITotpSetupTokenService _totpSetupTokenService;
    private readonly IEncryptionService _encryptionService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public VerifyTotpCommandHandler(
        DbContext dbContext,
        ITotpService totpService,
        ITotpSetupTokenService totpSetupTokenService,
        IEncryptionService encryptionService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _totpService = totpService;
        _totpSetupTokenService = totpSetupTokenService;
        _encryptionService = encryptionService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthTokenResponseDto> Handle(VerifyTotpCommand request, CancellationToken cancellationToken)
    {
        var (isValidToken, supervisorId, pendingSecret) = _totpSetupTokenService.ValidateSetupToken(request.TempToken);
        if (!isValidToken)
            throw new ValidationException("TempToken", "جلسة التحقق منتهية الصلاحية أو غير صحيحة.");

        var supervisor = await _dbContext.Set<Supervisor>()
            .FirstOrDefaultAsync(s => s.Id == supervisorId, cancellationToken);

        if (supervisor == null)
            throw new NotFoundException("حساب السوبرفايزر غير موجود.");

        // Lockout check
        if (supervisor.TotpLockoutUntil.HasValue && supervisor.TotpLockoutUntil > DateTime.UtcNow)
            throw new ForbiddenAccessException("تم قفل محاولات التحقق مؤقتًا بسبب كثرة محاولات TOTP الخاطئة.");

        string secretToUse;
        if (!supervisor.TotpEnabled)
        {
            if (string.IsNullOrEmpty(pendingSecret))
                throw new ValidationException("Secret", "رمز الإعداد غير موجود والجلسة غير صالحة.");

            secretToUse = pendingSecret;
        }
        else
        {
            if (string.IsNullOrEmpty(supervisor.TotpSecretEncrypted))
                throw new ValidationException("Totp", "إعدادات 2FA غير مكتملة.");

            secretToUse = _encryptionService.Decrypt(supervisor.TotpSecretEncrypted);
        }

        bool isValidCode = _totpService.VerifyCode(secretToUse, request.Code);
        if (!isValidCode)
        {
            supervisor.FailedTotpAttempts++;
            if (supervisor.FailedTotpAttempts >= 5)
            {
                supervisor.TotpLockoutUntil = DateTime.UtcNow.AddMinutes(15);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new ValidationException("Code", "رمز التحقق خاطئ أو منتهي الصلاحية.");
        }

        // Reset failed TOTP attempts on successful verification
        supervisor.FailedTotpAttempts = 0;
        supervisor.TotpLockoutUntil = null;

        if (!supervisor.TotpEnabled)
        {
            supervisor.TotpEnabled = true;
            supervisor.TotpSecretEncrypted = _encryptionService.Encrypt(secretToUse);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(supervisor.Id.ToString(), supervisor.Email, ActorType.Supervisor, null, null, supervisor.MustChangePassword);
        return new AuthTokenResponseDto(token, supervisor.MustChangePassword);
    }
}
