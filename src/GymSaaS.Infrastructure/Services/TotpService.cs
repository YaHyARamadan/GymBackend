using GymSaaS.Application.Common.Interfaces;
using OtpNet;
using QRCoder;

namespace GymSaaS.Infrastructure.Services;

public class TotpService : ITotpService
{
    public (string Secret, string QrCodeUri) GenerateSetupSecret(string userEmail)
    {
        var secretKey = KeyGeneration.GenerateRandomKey(20);
        string secretBase32 = Base32Encoding.ToString(secretKey);

        string label = Uri.EscapeDataString($"GymSaaS:{userEmail}");
        string issuer = Uri.EscapeDataString("GymSaaS");
        string otpauthUri = $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuer}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeGraphic = qrCode.GetGraphic(20);
        string base64Qr = Convert.ToBase64String(qrCodeGraphic);
        string dataUri = $"data:image/png;base64,{base64Qr}";

        return (secretBase32, dataUri);
    }

    public bool VerifyCode(string secret, string code)
    {
        try
        {
            byte[] secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}
