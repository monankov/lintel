using System.Reflection;

namespace Lintel.Settings;

/// <summary>
/// Base class of settings storages (files, memory, database...).
/// </summary>
/// <remarks>
/// With <see cref="SettingsProviderOptions.Cache"/> on, the first <see cref="GetSettings"/> loads the settings, and later calls
/// reload them into the same instance when <see cref="IsSettingsModified"/> says so, so references held by the application
/// see new values. All members are thread-safe.
/// </remarks>
/// <typeparam name="TSettings">The settings type.</typeparam>
public abstract class SettingsProvider<TSettings> where TSettings : class, new()
{
    private static readonly PropertyInfo[] _writableProperties = typeof(TSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.GetIndexParameters().Length == 0 && property.GetSetMethod(nonPublic: true) is not null)
        .ToArray();

    private readonly Lock _lock = new();
    private TSettings? _cache;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    protected SettingsProvider(SettingsProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    /// <summary>The options.</summary>
    protected SettingsProviderOptions Options { get; }

    /// <summary>
    /// Returns the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    public TSettings GetSettings()
    {
        lock (_lock)
        {
            if (!Options.Cache || _cache is null)
            {
                var settings = LoadSettings();
                LogFirstAccess();
                if (!Options.Cache)
                {
                    return settings;
                }
                _cache = settings;
            }
            else if (IsSettingsModified())
            {
                CopyProperties(LoadSettings(), _cache);
            }

            return _cache;
        }
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="settings"/> is <see langword="null"/>.</exception>
    public void SetSettings(TSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_lock)
        {
            UpdateSettings(settings);
            if (_cache is not null && !ReferenceEquals(_cache, settings))
            {
                CopyProperties(settings, _cache);
            }
            LogFirstAccess(); // our own write is not an external modification
        }
    }

    /// <summary>Loads the settings from the storage.</summary>
    /// <returns>The settings.</returns>
    protected abstract TSettings LoadSettings();

    /// <summary>Saves the settings to the storage.</summary>
    /// <param name="settings">The settings.</param>
    protected abstract void UpdateSettings(TSettings settings);

    /// <summary>Remembers the state of the storage after loading or saving (for example the file time).</summary>
    protected abstract void LogFirstAccess();

    /// <summary>Whether the storage changed since <see cref="LogFirstAccess"/> or the previous call.</summary>
    /// <returns><see langword="true"/> if the settings must be reloaded.</returns>
    protected abstract bool IsSettingsModified();

    private static void CopyProperties(TSettings source, TSettings destination)
    {
        foreach (var property in _writableProperties)
        {
            property.SetValue(destination, property.GetValue(source));
        }
    }
}
