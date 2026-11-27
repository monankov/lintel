using System.Security.Cryptography;
using System.Text;

namespace Lintel.Settings.Tests.TestUtils;

public enum Mode
{
    Off,
    On,
}

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
}

public class TestSettings
{
    public string? FileName { get; set; }

    [Option(Alias = "SomeOwnerNameAlias")]
    public string? OwnerName { get; set; }

    [Option(DefaultValue = "help.example.com")]
    public string? HelpUrl { get; set; }

    [Option(DefaultValue = 25)]
    public long Timeout { get; set; }

    [Option(EncryptionPassphrase = "abcdef")]
    public string? ApiKey { get; set; }

    public bool Enabled { get; set; }

    public Mode Mode { get; set; }

    public SmtpSettings? Smtp { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? InternalNote { get; private set; }

    public string Computed => $"{FileName}!";

    public void SetNote(string note) => InternalNote = note;
}

public class ProtectedSettings
{
    [Option(Protected = true)]
    public string? Password { get; set; }
}

/// <summary>A temporary directory deleted after the test.</summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lintel-settings-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public string File(string name, string content)
    {
        var path = System.IO.Path.Combine(Path, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Rewrites a file so that its last write time changes.</summary>
    public static void Rewrite(string path, string content)
    {
        var before = System.IO.File.GetLastWriteTimeUtc(path);
        System.IO.File.WriteAllText(path, content);
        System.IO.File.SetLastWriteTimeUtc(path, before.AddSeconds(2));
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

/// <summary>The encryption of Structr.Configuration (StringEncryptor), verbatim except the obsolete SHA512CryptoServiceProvider.</summary>
public static class StructrEncryptor
{
    public static string Encrypt(string input, string passphrase)
    {
        var buffer = Encoding.UTF8.GetBytes(input);
        using var hash = SHA512.Create();
        var aesKey = new byte[24];
        Buffer.BlockCopy(hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase)), 0, aesKey, 0, 24);
        using var aes = Aes.Create();
        aes.Key = aesKey;
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var resultStream = new MemoryStream();
        using (var aesStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write))
        using (var plainStream = new MemoryStream(buffer))
        {
            plainStream.CopyTo(aesStream);
        }
        var resultArray = resultStream.ToArray();
        var combined = new byte[aes.IV.Length + resultArray.Length];
        Array.ConstrainedCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Array.ConstrainedCopy(resultArray, 0, combined, aes.IV.Length, resultArray.Length);
        return Convert.ToBase64String(combined);
    }

    public static string Decrypt(string input, string passphrase)
    {
        var combined = Convert.FromBase64String(input);
        using var hash = SHA512.Create();
        var aesKey = new byte[24];
        Buffer.BlockCopy(hash.ComputeHash(Encoding.UTF8.GetBytes(passphrase)), 0, aesKey, 0, 24);
        using var aes = Aes.Create();
        aes.Key = aesKey;
        var iv = new byte[aes.IV.Length];
        var ciphertext = new byte[combined.Length - iv.Length];
        Array.ConstrainedCopy(combined, 0, iv, 0, iv.Length);
        Array.ConstrainedCopy(combined, iv.Length, ciphertext, 0, ciphertext.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var resultStream = new MemoryStream();
        using (var aesStream = new CryptoStream(resultStream, decryptor, CryptoStreamMode.Write))
        using (var plainStream = new MemoryStream(ciphertext))
        {
            plainStream.CopyTo(aesStream);
        }
        return Encoding.UTF8.GetString(resultStream.ToArray());
    }
}
