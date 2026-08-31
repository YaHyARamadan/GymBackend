using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record VerifyTotpCommand(string Email, string Code, string? SecretIfSetup) : IRequest<AuthTokenResponseDto>;

public record AuthTokenResponseDto(string Token, bool MustChangePassword);

public class VerifyTotpCommandValidator : AbstractValidator<VerifyTotpCommand>
{
    public VerifyTotpCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6).WithMessage("رمز التحقق يجب أن يكون 6 أرقام.");
    }
}

public class VerifyTotpCommandHandler : IRequestHandler<VerifyTotpCommand, AuthTokenResponseDto>
{
    private readonly DbContext _dbContext;
    private readonly ITotpService _totpService;
    private readonly IEncryptionService _encryptionService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public VerifyTotpCommandHandler(DbContext dbContext, ITotpService totpService, IEncryptionService encryptionService, IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _totpService = totpService;
        _encryptionService = encryptionService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthTokenResponseDto> Handle(VerifyTotpCommand request, CancellationToken cancellationToken)
    {
        var supervisor = await _dbContext.Set<Supervisor>()
            .FirstOrDefaultAsync(s => s.Email == request.Email, cancellationToken);

        if (supervisor == null)
            throw new NotFoundException("حساب السوبرفايزر غير موجود.");

        string secretToUse;
        if (!supervisor.TotpEnabled)
        {
            if (string.IsNullOrEmpty(request.SecretIfSetup))
                throw new ValidationException("Secret", "رمز الإعداد غير موجود.");

            secretToUse = request.SecretIfSetup;
        }
        else
        {
            if (string.IsNullOrEmpty(supervisor.TotpSecretEncrypted))
                throw new ValidationException("Totp", "إعدادات 2FA غير اكتمال.");

            secretToUse = _encryptionService.Decrypt(supervisor.TotpSecretEncrypted);
        }

        bool isValid = _totpService.VerifyCode(secretToUse, request.Code);
        if (!isValid)
            throw new ValidationException("Code", "رمز التحقق خاطئ أو منتهي الصلاحية.");

        if (!supervisor.TotpEnabled)
        {
            supervisor.TotpEnabled = true;
            supervisor.TotpSecretEncrypted = _encryptionService.Encrypt(secretToUse);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenGenerator.GenerateToken(supervisor.Id.ToString(), supervisor.Email, ActorType.Supervisor, null, null);
        return new AuthTokenResponseDto(token, supervisor.MustChangePassword);
    }
}
