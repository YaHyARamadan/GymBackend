namespace GymSaaS.Application.Common.Interfaces;

public interface ITotpService
{
    (string Secret, string QrCodeUri) GenerateSetupSecret(string userEmail);
    bool VerifyCode(string secret, string code);
}
