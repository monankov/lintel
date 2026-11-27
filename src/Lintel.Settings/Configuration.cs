using Lintel.Settings.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lintel.Settings;

/// <summary>
/// The provider of <typeparamref name="TSettings"/>, registered by <c>AddJson</c>, <c>AddXml</c>, <c>AddInMemory</c> or <c>AddProvider</c>.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public class ConfigurationOptions<TSettings> where TSettings : class, new()
{
    /// <summary>
    /// Initializes the options.
    /// </summary>
    /// <param name="provider">The settings provider.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="provider"/> is <see langword="null"/>.</exception>
    public ConfigurationOptions(SettingsProvider<TSettings> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Provider = provider;
    }

    /// <summary>The settings provider.</summary>
    public SettingsProvider<TSettings> Provider { get; }
}

/// <summary>
/// Default <see cref="IConfiguration{TSettings}"/>.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
/// <param name="options">The options.</param>
public class Configuration<TSettings>(ConfigurationOptions<TSettings> options) : IConfiguration<TSettings>
    where TSettings : class, new()
{
    private readonly ConfigurationOptions<TSettings> _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public TSettings Settings => _options.Provider.GetSettings();
}

/// <summary>
/// Default <see cref="IConfigurator{TSettings}"/>.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
/// <param name="options">The options.</param>
public class Configurator<TSettings>(ConfigurationOptions<TSettings> options) : IConfigurator<TSettings>
    where TSettings : class, new()
{
    private readonly ConfigurationOptions<TSettings> _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If <paramref name="changes"/> is <see langword="null"/>.</exception>
    public void Configure(Action<TSettings> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var provider = _options.Provider;
        var settings = provider.GetSettings();
        changes(settings);
        provider.SetSettings(settings);
    }
}

/// <summary>
/// Registers settings types: <c>services.AddConfiguration().AddJson&lt;MySettings&gt;("settings.json")</c>.
/// </summary>
/// <param name="services">The service collection.</param>
public class ConfigurationServiceBuilder(IServiceCollection services)
{
    /// <summary>The service collection.</summary>
    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
}

/// <summary>
/// Registration of settings providers.
/// </summary>
public static class ConfigurationServiceBuilderExtensions
{
    /// <summary>Registers a provider instance for <typeparamref name="TSettings"/>.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="provider">The provider.</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddProvider<TSettings>(this ConfigurationServiceBuilder builder, SettingsProvider<TSettings> provider)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(provider);

        return builder.AddProvider(_ => provider);
    }

    /// <summary>Registers a provider factory for <typeparamref name="TSettings"/>; the first registration wins.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="providerFactory">Creates the provider once.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static ConfigurationServiceBuilder AddProvider<TSettings>(this ConfigurationServiceBuilder builder,
        Func<IServiceProvider, SettingsProvider<TSettings>> providerFactory)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);

        builder.Services.TryAddSingleton(serviceProvider => new ConfigurationOptions<TSettings>(providerFactory(serviceProvider)));
        return builder;
    }

    /// <summary>Stores <typeparamref name="TSettings"/> in a JSON file.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="path">The path of the file; it must exist.</param>
    /// <param name="configure">Configures the provider options.</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddJson<TSettings>(this ConfigurationServiceBuilder builder, string path,
        Action<SettingsProviderOptions>? configure = null)
        where TSettings : class, new()
        => builder.AddJson<TSettings>(path, (_, options) => configure?.Invoke(options));

    /// <summary>Stores <typeparamref name="TSettings"/> in a JSON file.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="path">The path of the file; it must exist.</param>
    /// <param name="configure">Configures the provider options with access to services (for example the data protector).</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddJson<TSettings>(this ConfigurationServiceBuilder builder, string path,
        Action<IServiceProvider, SettingsProviderOptions>? configure)
        where TSettings : class, new()
        => builder.AddProvider(serviceProvider => new JsonSettingsProvider<TSettings>(path, Options(serviceProvider, configure)));

    /// <summary>Stores <typeparamref name="TSettings"/> in an XML file.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="path">The path of the file; it must exist.</param>
    /// <param name="configure">Configures the provider options.</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddXml<TSettings>(this ConfigurationServiceBuilder builder, string path,
        Action<SettingsProviderOptions>? configure = null)
        where TSettings : class, new()
        => builder.AddXml<TSettings>(path, (_, options) => configure?.Invoke(options));

    /// <summary>Stores <typeparamref name="TSettings"/> in an XML file.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="path">The path of the file; it must exist.</param>
    /// <param name="configure">Configures the provider options with access to services.</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddXml<TSettings>(this ConfigurationServiceBuilder builder, string path,
        Action<IServiceProvider, SettingsProviderOptions>? configure)
        where TSettings : class, new()
        => builder.AddProvider(serviceProvider => new XmlSettingsProvider<TSettings>(path, Options(serviceProvider, configure)));

    /// <summary>Keeps <typeparamref name="TSettings"/> in memory.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="settings">The initial settings.</param>
    /// <param name="configure">Configures the provider options.</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddInMemory<TSettings>(this ConfigurationServiceBuilder builder, TSettings settings,
        Action<SettingsProviderOptions>? configure = null)
        where TSettings : class, new()
        => builder.AddInMemory(settings, (_, options) => configure?.Invoke(options));

    /// <summary>Keeps <typeparamref name="TSettings"/> in memory.</summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="settings">The initial settings.</param>
    /// <param name="configure">Configures the provider options with access to services.</param>
    /// <returns>The same builder.</returns>
    public static ConfigurationServiceBuilder AddInMemory<TSettings>(this ConfigurationServiceBuilder builder, TSettings settings,
        Action<IServiceProvider, SettingsProviderOptions>? configure)
        where TSettings : class, new()
        => builder.AddProvider(serviceProvider => new InMemorySettingsProvider<TSettings>(settings, Options(serviceProvider, configure)));

    private static SettingsProviderOptions Options(IServiceProvider serviceProvider, Action<IServiceProvider, SettingsProviderOptions>? configure)
    {
        var options = new SettingsProviderOptions();
        configure?.Invoke(serviceProvider, options);
        return options;
    }
}

/// <summary>
/// Registration of settings services.
/// </summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IConfiguration{TSettings}"/> and <see cref="IConfigurator{TSettings}"/>; register settings types on the returned builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static ConfigurationServiceBuilder AddConfiguration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(typeof(IConfiguration<>), typeof(Configuration<>));
        services.TryAddSingleton(typeof(IConfigurator<>), typeof(Configurator<>));
        return new ConfigurationServiceBuilder(services);
    }
}
