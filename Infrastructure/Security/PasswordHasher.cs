using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace TruLoad.Backend.Infrastructure.Security;

/// <summary>
/// Argon2id password hasher compatible with shared Go library and auth-service.
/// Uses identical parameters and format to ensure cross-service password verification.
/// 
/// This .NET implementation mirrors github.com/Bengo-Hub/shared-password-hasher
/// Format: $argon2id$v=19$m=65536,t=3,p=2$&lt;base64-salt&gt;$&lt;base64-hash&gt;
/// 
/// IMPORTANT: Do NOT modify hashing logic without updating Go library!
/// </summary>
public class PasswordHasher
{
    // Default parameters matching Go shared library and auth-service
    private const int DefaultMemorySize = 65536;  // 64 MiB in KB
    private const int DefaultIterations = 3;
    private const int DefaultDegreeOfParallelism = 2;
    private const int DefaultKeyLength = 32;
    private const int SaltLength = 16;
    private const int Argon2Version = 19;

    private readonly int _memorySize;
    private readonly int _iterations;
    private readonly int _degreeOfParallelism;
    private readonly int _keyLength;

    /// <summary>
    /// Creates a new PasswordHasher with default Argon2id parameters.
    /// Defaults match Go shared library: m=65536, t=3, p=2, keylen=32
    /// </summary>
    public PasswordHasher()
        : this(DefaultMemorySize, DefaultIterations, DefaultDegreeOfParallelism, DefaultKeyLength)
    {
    }

    /// <summary>
    /// Creates a new PasswordHasher with custom Argon2id parameters.
    /// Use this when shared library configuration changes.
    /// </summary>
    /// <param name="memorySize">Memory size in KB (m parameter)</param>
    /// <param name="iterations">Time cost iterations (t parameter)</param>
    /// <param name="degreeOfParallelism">Thread count (p parameter)</param>
    /// <param name="keyLength">Output hash length in bytes</param>
    public PasswordHasher(int memorySize, int iterations, int degreeOfParallelism, int keyLength)
    {
        _memorySize = memorySize;
        _iterations = iterations;
        _degreeOfParallelism = degreeOfParallelism;
        _keyLength = keyLength;
    }

    /// <summary>
    /// Hash a password using Argon2id.
    /// Returns formatted hash: $argon2id$v=19$m=65536,t=3,p=2$&lt;salt&gt;$&lt;hash&gt;
    /// Compatible with Go shared library and auth-service.
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        // Generate cryptographically random salt
        var salt = new byte[SaltLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Generate hash using Argon2id
        var hash = HashPasswordWithSalt(password, salt);

        // Encode to base64 without padding (RFC 4648 base64url)
        // IMPORTANT: Must match Go implementation - no padding!
        var saltBase64 = Convert.ToBase64String(salt).TrimEnd('=');
        var hashBase64 = Convert.ToBase64String(hash).TrimEnd('=');

        // Format: $argon2id$v=19$m=65536,t=3,p=2$<salt>$<hash>
        return $"$argon2id$v={Argon2Version}$m={_memorySize},t={_iterations},p={_degreeOfParallelism}${saltBase64}${hashBase64}";
    }

    /// <summary>
    /// Verify password against stored Argon2id hash.
    /// Compatible with hashes from Go shared library, auth-service, and other .NET services.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        if (string.IsNullOrEmpty(hashedPassword))
            throw new ArgumentException("Hashed password cannot be null or empty", nameof(hashedPassword));

        try
        {
            var (memorySize, iterations, degreeOfParallelism, salt, expectedHash) = ParseHash(hashedPassword);

            // Generate hash with extracted parameters
            var actualHash = HashPasswordWithSalt(password, salt, memorySize, iterations, degreeOfParallelism, expectedHash.Length);

            // Constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            // Invalid hash format
            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to verify password hash", ex);
        }
    }

    private byte[] HashPasswordWithSalt(string password, byte[] salt)
    {
        return HashPasswordWithSalt(password, salt, _memorySize, _iterations, _degreeOfParallelism, _keyLength);
    }

    private static byte[] HashPasswordWithSalt(string password, byte[] salt, int memorySize, int iterations, int degreeOfParallelism, int keyLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memorySize,
            Iterations = iterations,
            DegreeOfParallelism = degreeOfParallelism
        };

        return argon2.GetBytes(keyLength);
    }

    /// <summary>
    /// Parse Argon2id hash string and extract parameters, salt, and hash.
    /// Expected format: $argon2id$v=19$m=65536,t=3,p=2$&lt;base64-salt&gt;$&lt;base64-hash&gt;
    /// Must match Go shared library parsing logic exactly.
    /// </summary>
    private static (int memorySize, int iterations, int degreeOfParallelism, byte[] salt, byte[] hash) ParseHash(string hashedPassword)
    {
        // Expected format: $argon2id$v=19$m=65536,t=3,p=2$<base64-salt>$<base64-hash>
        var parts = hashedPassword.Split('$');
        if (parts.Length != 6)
            throw new FormatException($"Invalid hash format: expected 6 parts, got {parts.Length}");

        // Verify algorithm
        if (parts[1] != "argon2id")
            throw new FormatException("Hash is not Argon2id");

        // Verify version
        if (parts[2] != $"v={Argon2Version}")
            throw new FormatException($"Unsupported Argon2 version: {parts[2]}");

        // Parse m=65536,t=3,p=2
        var paramParts = parts[3].Split(',');
        if (paramParts.Length != 3)
            throw new FormatException("Invalid parameter format");

        var memorySize = int.Parse(paramParts[0].Split('=')[1]);
        var iterations = int.Parse(paramParts[1].Split('=')[1]);
        var degreeOfParallelism = int.Parse(paramParts[2].Split('=')[1]);

        // Decode base64 salt and hash (add padding if needed)
        // IMPORTANT: Must match Go's base64.RawStdEncoding (no padding)
        var saltBase64 = parts[4].PadRight(parts[4].Length + (4 - parts[4].Length % 4) % 4, '=');
        var hashBase64 = parts[5].PadRight(parts[5].Length + (4 - parts[5].Length % 4) % 4, '=');

        var salt = Convert.FromBase64String(saltBase64);
        var hash = Convert.FromBase64String(hashBase64);

        return (memorySize, iterations, degreeOfParallelism, salt, hash);
    }

    /// <summary>
    /// Hash password with provided salt for testing or when deterministic hash is needed.
    /// In production, use HashPassword() which generates random salt.
    /// </summary>
    public string HashPasswordWithProvidedSalt(string password, byte[] salt)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        if (salt == null || salt.Length != SaltLength)
            throw new ArgumentException($"Salt must be exactly {SaltLength} bytes", nameof(salt));

        var hash = HashPasswordWithSalt(password, salt);

        var saltBase64 = Convert.ToBase64String(salt).TrimEnd('=');
        var hashBase64 = Convert.ToBase64String(hash).TrimEnd('=');

        return $"$argon2id$v={Argon2Version}$m={_memorySize},t={_iterations},p={_degreeOfParallelism}${saltBase64}${hashBase64}";
    }
}
