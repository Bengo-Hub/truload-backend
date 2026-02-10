using System.Security.Cryptography;
using System.Text;

namespace TruLoad.Backend.Infrastructure.Security;

/// <summary>
/// AES-256-GCM encryption service for protecting integration credentials at rest.
/// Key is derived from configuration via HKDF.
/// Output format: Base64(nonce[12] || ciphertext[n] || tag[16])
/// </summary>
public class AesGcmEncryptionService : IEncryptionService
{
    private const int NonceSize = 12; // 96 bits per NIST recommendation
    private const int TagSize = 16;   // 128 bits
    private const int KeySize = 32;   // 256 bits

    private readonly byte[] _key;

    public AesGcmEncryptionService(IConfiguration configuration)
    {
        var encryptionKey = configuration["Security:EncryptionKey"];

        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            // Fallback for development: derive from JWT secret key
            var jwtKey = configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "Security:EncryptionKey or Jwt:SecretKey must be configured for credential encryption.");
            }

            _key = DeriveKey(Encoding.UTF8.GetBytes(jwtKey));
        }
        else
        {
            _key = DeriveKey(Encoding.UTF8.GetBytes(encryptionKey));
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combine: nonce + ciphertext + tag
        var result = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + cipherBytes.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        var combined = Convert.FromBase64String(cipherText);

        if (combined.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext: too short.");

        var nonce = new byte[NonceSize];
        var cipherBytes = new byte[combined.Length - NonceSize - TagSize];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, NonceSize, cipherBytes, 0, cipherBytes.Length);
        Buffer.BlockCopy(combined, NonceSize + cipherBytes.Length, tag, 0, TagSize);

        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Derives a 256-bit key from input material using HKDF-SHA256.
    /// </summary>
    private static byte[] DeriveKey(byte[] inputKeyMaterial)
    {
        var info = Encoding.UTF8.GetBytes("TruLoad.IntegrationCredentials.v1");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKeyMaterial, KeySize, info: info);
    }
}
