using Microsoft.AspNetCore.DataProtection;

namespace Lintel.Settings;

/// <summary>
/// Read access to typed settings. Inject <c>IConfiguration&lt;MySettings&gt;</c> and read <see cref="Settings"/>.
/// </summary>
/// <remarks>
/// Not to be confused with <c>Microsoft.Extensions.Configuration.IConfiguration</c> (non-generic):
/// this one is a typed view over a settings file that the application can also change at runtime.
/// </remarks>
/// <typeparam name="TSettings">The settings type.</typeparam>
public interface IConfiguration<out TSettings> where TSettings : class, new()
{
    /// <summary>The current settings. With caching on, the same instance updated when the file changes.</summary>
    TSettings Settings { get; }
}

/// <summary>
/// Write access to typed settings.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public interface IConfigurator<TSettings> where TSettings : class, new()
{
    /// <summary>
    /// Applies <paramref name="changes"/> to the current settings and saves them.
    /// </summary>
    /// <param name="changes">The changes.</param>
    void Configure(Action<TSettings> changes);
}

/// <summary>
/// Configures how a property is stored.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class OptionAttribute : Attribute
{
    /// <summary>The name of the property in the file, instead of the property name.</summary>
    public string? Alias { get; set; }

    /// <summary>The value used when the property is missing in the file.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Encrypts the string property with AES using this passphrase (the Structr format, kept for existing files).
    /// The passphrase is part of the code; prefer <see cref="Protected"/> for new settings.
    /// </summary>
    public string? EncryptionPassphrase { get; set; }

    /// <summary>
    /// Encrypts the string property with ASP.NET Core Data Protection
    /// (<see cref="SettingsProviderOptions.DataProtector"/> must be set).
    /// </summary>
    public bool Protected { get; set; }
}

/// <summary>
/// Options of a <see cref="SettingsProvider{TSettings}"/>.
/// </summary>
public class SettingsProviderOptions
{
    /// <summary>
    /// <see langword="true"/> (default): settings are loaded once and the same instance is updated when the file changes.
    /// <see langword="false"/>: settings are loaded on every access.
    /// </summary>
    public bool Cache { get; set; } = true;

    /// <summary>The protector of <see cref="OptionAttribute.Protected"/> properties.</summary>
    /// <example><c>options.DataProtector = sp.GetRequiredService&lt;IDataProtectionProvider&gt;().CreateProtector("Settings");</c></example>
    public IDataProtector? DataProtector { get; set; }
}
