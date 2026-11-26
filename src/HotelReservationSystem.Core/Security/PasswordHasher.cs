using System.Globalization;
using System.Security.Cryptography;

namespace HotelReservationSystem.Core.Security;

/// <summary>
/// Salted PBKDF2-HMAC-SHA256 password hashing (one-way; passwords are never decrypted).
/// Format: <c>PBKDF2-SHA256$iterations$base64(salt)$base64(key)</c>.
/// </summary>
public static class PasswordHasher
{
    public const int DefaultIterations = 600_000; // OWASP recommendation for PBKDF2-HMAC-SHA256
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Scheme = "PBKDF2-SHA256";

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
        return string.Join('$', Scheme, iterations.ToString(CultureInfo.InvariantCulture),
                           Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(string? password, string? storedHash)
    {
        if (password is null || !TryParse(storedHash, out var iterations, out var salt, out var expected))
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True when the hash was created with weaker settings than the current default.</summary>
    public static bool NeedsRehash(string? storedHash) =>
        !TryParse(storedHash, out var iterations, out _, out _) || iterations < DefaultIterations;

    private static bool TryParse(string? stored, out int iterations, out byte[] salt, out byte[] key)
    {
        iterations = 0; salt = []; key = [];
        if (string.IsNullOrEmpty(stored)) return false;

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations < 1)
            return false;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            key = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }
        return salt.Length >= 8 && key.Length >= 16;
    }
}
