using System.Security.Cryptography;
using System.Text;
using GymSaaS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GymSaaS.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private const int NonceSize = 12; // 96 bits standard for AES-GCM
    private const int TagSize = 16;   // 128 bits standard tag
    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration)
    {
        string? pass = configuration["Encryption:SecretKey"];
        if (string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException("Encryption:SecretKey is not configured. The application cannot start without a valid encryption key.");
        _key = Encoding.UTF8.GetBytes(pass.PadRight(32)[..32]);
    }

    public string Encrypt(string plainText)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        byte[] nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        byte[] tag = new byte[TagSize];
        byte[] cipherBytes = new byte[plainBytes.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combined payload structure: [Nonce (12) | Tag (16) | Ciphertext (N)]
        byte[] result = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        byte[] fullPayload = Convert.FromBase64String(cipherText);

        try
        {
            if (fullPayload.Length >= NonceSize + TagSize)
            {
                byte[] nonce = new byte[NonceSize];
                byte[] tag = new byte[TagSize];
                byte[] cipherBytes = new byte[fullPayload.Length - NonceSize - TagSize];

                Buffer.BlockCopy(fullPayload, 0, nonce, 0, NonceSize);
                Buffer.BlockCopy(fullPayload, NonceSize, tag, 0, TagSize);
                Buffer.BlockCopy(fullPayload, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

                byte[] plainBytes = new byte[cipherBytes.Length];
                using var aesGcm = new AesGcm(_key, TagSize);
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
        }
        catch
        {
            // Fallback for legacy AES-CBC encrypted payloads
        }

        return DecryptLegacyCbc(fullPayload);
    }

    private string DecryptLegacyCbc(byte[] fullCipher)
    {
        using var aes = Aes.Create();
        aes.Key = _key;

        byte[] iv = new byte[16];
        byte[] cipher = new byte[fullCipher.Length - 16];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        byte[] decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
