namespace TruLoad.Backend.Infrastructure.Security;

/// <summary>
/// Service for encrypting and decrypting sensitive data at rest.
/// Used to protect integration credentials stored in the database.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using AES-256-GCM.
    /// Returns Base64-encoded ciphertext (nonce + ciphertext + tag).
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts AES-256-GCM encrypted ciphertext.
    /// Input must be Base64-encoded (nonce + ciphertext + tag).
    /// </summary>
    string Decrypt(string cipherText);
}
