using System.Text.Json;
using System.Xml.Linq;
using Lintel.Settings.Internal;

namespace Lintel.Settings.Providers;

/// <summary>
/// Base class of providers that store settings in a file: detects changes by the last write time.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public abstract class FileSettingsProvider<TSettings> : SettingsProvider<TSettings>
    where TSettings : class, new()
{
    private DateTime? _lastModifiedTime;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="path">The path of the file; the file must exist.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="path"/> is empty.</exception>
    protected FileSettingsProvider(string path, SettingsProviderOptions options) : base(options)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        Path = path;
    }

    /// <summary>The path of the file.</summary>
    protected string Path { get; }

    /// <inheritdoc />
    protected override void LogFirstAccess()
    {
        ValidatePathOrThrow();
        _lastModifiedTime = File.GetLastWriteTimeUtc(Path);
    }

    /// <inheritdoc />
    protected override bool IsSettingsModified()
    {
        ValidatePathOrThrow();
        var lastModifiedTime = File.GetLastWriteTimeUtc(Path);
        var isModified = _lastModifiedTime != lastModifiedTime;
        _lastModifiedTime = lastModifiedTime;
        return isModified;
    }

    /// <summary>
    /// Throws if the file does not exist.
    /// </summary>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    protected void ValidatePathOrThrow()
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException("Settings file not found.", Path);
        }
    }

    /// <summary>
    /// Replaces the file atomically: writes a temporary file next to it and moves it over the original,
    /// so readers never see a half-written file.
    /// </summary>
    /// <param name="write">Writes the content to the stream.</param>
    protected void WriteAtomically(Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        ValidatePathOrThrow();
        var temporaryPath = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
            }
            File.Move(temporaryPath, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

/// <summary>
/// Stores settings in a JSON file (System.Text.Json; comments and trailing commas are allowed).
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public class JsonSettingsProvider<TSettings> : FileSettingsProvider<TSettings>
    where TSettings : class, new()
{
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="path">The path of the JSON file; the file must exist.</param>
    /// <param name="options">The options.</param>
    public JsonSettingsProvider(string path, SettingsProviderOptions options)
        : base(path, options)
    {
        _serializerOptions = SettingsSerializer.CreateOptions(options.DataProtector, forXml: false);
    }

    /// <inheritdoc />
    protected override TSettings LoadSettings()
    {
        ValidatePathOrThrow();
        using var stream = File.OpenRead(Path);
        return stream.Length == 0
            ? new TSettings()
            : JsonSerializer.Deserialize<TSettings>(stream, _serializerOptions) ?? new TSettings();
    }

    /// <inheritdoc />
    protected override void UpdateSettings(TSettings settings)
        => WriteAtomically(stream => JsonSerializer.Serialize(stream, settings, _serializerOptions));
}

/// <summary>
/// Stores settings in an XML file: one child element per property, nested elements for nested objects,
/// repeated elements for lists. The format is the one of Structr.Configuration; the root element is named after the file.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public class XmlSettingsProvider<TSettings> : FileSettingsProvider<TSettings>
    where TSettings : class, new()
{
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="path">The path of the XML file; the file must exist.</param>
    /// <param name="options">The options.</param>
    public XmlSettingsProvider(string path, SettingsProviderOptions options)
        : base(path, options)
    {
        _serializerOptions = SettingsSerializer.CreateOptions(options.DataProtector, forXml: true);
    }

    /// <inheritdoc />
    protected override TSettings LoadSettings()
    {
        ValidatePathOrThrow();
        // XDocument.Load throws XmlException for a document without a root element.
        var json = XmlJsonConverter.ToJson(XDocument.Load(Path).Root!);
        return json.Deserialize<TSettings>(_serializerOptions) ?? new TSettings();
    }

    /// <inheritdoc />
    protected override void UpdateSettings(TSettings settings)
    {
        var json = JsonSerializer.SerializeToNode(settings, _serializerOptions);
        var document = XmlJsonConverter.ToXml(json, System.IO.Path.GetFileNameWithoutExtension(Path));
        WriteAtomically(document.Save);
    }
}

/// <summary>
/// Keeps settings in memory (tests, defaults). <see cref="OptionAttribute"/> has no effect.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public class InMemorySettingsProvider<TSettings> : SettingsProvider<TSettings>
    where TSettings : class, new()
{
    private TSettings _settings;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="settings">The initial settings.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public InMemorySettingsProvider(TSettings settings, SettingsProviderOptions options)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <inheritdoc />
    protected override bool IsSettingsModified() => false;

    /// <inheritdoc />
    protected override TSettings LoadSettings() => _settings;

    /// <inheritdoc />
    protected override void LogFirstAccess()
    {
    }

    /// <inheritdoc />
    protected override void UpdateSettings(TSettings settings) => _settings = settings;
}
