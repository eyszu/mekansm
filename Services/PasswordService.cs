using System.Security.Cryptography;

namespace Mekansm.Services;

/// <summary>PBKDF2-SHA256 password hashing. Format: v1.{iterations}.{saltB64}.{hashB64}</summary>
public sealed class PasswordService
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private static readonly string DummyHash = new PasswordService().Hash("mekansm-dummy-password");

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password. When <paramref name="stored"/> is null (unknown user) it still burns
    /// the same amount of work so response timing doesn't reveal which emails exist.
    /// </summary>
    public bool Verify(string password, string? stored)
    {
        var known = stored is not null;
        var parts = (stored ?? DummyHash).Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected) && known;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
