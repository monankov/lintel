using System.Security.Cryptography;
using System.Text;

namespace Lintel.Settings.Internal;

/// <summary>
/// The encryption of <c>[Option(EncryptionPassphrase = ...)]</c> from Structr.Configuration, byte for byte:
/// AES-192-CBC with PKCS7 padding, the key is the first 24 bytes of SHA-512 of the UTF-8 passphrase,
/// the output is Base64 of a random 16-byte IV followed by the ciphertext.
/// </summary>
internal static class LegacyStringEncryptor
{
    private const int KeySize = 24;
    private const int IvSize = 16;

    public static string Encrypt(string input, string passphrase)
    {
        using var aes = Create(passphrase);
        aes.GenerateIV();

        var ciphertext = aes.EncryptCbc(Encoding.UTF8.GetBytes(input), aes.IV, PaddingMode.PKCS7);
        return Convert.ToBase64String([.. aes.IV, .. ciphertext]);
    }

    public static string Decrypt(string input, string passphrase)
    {
        var combined = Convert.FromBase64String(input);
        if (combined.Length <= IvSize)
        {
            throw new CryptographicException("The encrypted value is too short.");
        }

        using var aes = Create(passphrase);
        var plaintext = aes.DecryptCbc(combined.AsSpan(IvSize), combined.AsSpan(0, IvSize), PaddingMode.PKCS7);
        return Encoding.UTF8.GetString(plaintext);
    }

    private static Aes Create(string passphrase)
    {
        var aes = Aes.Create();
        aes.Key = SHA512.HashData(Encoding.UTF8.GetBytes(passphrase)).AsSpan(0, KeySize).ToArray();
        return aes;
    }
}
