using Lintel.Navigation;
using Lintel.Navigation.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lintel.Navigation
{
    /// <summary>
    /// Registers navigation trees: <c>services.AddNavigation().AddJson&lt;MenuItem&gt;("navigation.json", o =&gt; ...)</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public class NavigationServiceBuilder(IServiceCollection services)
    {
        /// <summary>The service collection.</summary>
        public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registration of navigation services.
    /// </summary>
    public static class NavigationServiceCollectionExtensions
    {
        /// <summary>
        /// Adds <see cref="INavigation{TNavigationItem}"/>, <see cref="IBreadcrumbNavigation{TNavigationItem}"/> and their
        /// infrastructure; register trees on the returned builder.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The builder.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
        public static NavigationServiceBuilder AddNavigation(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<INavigationCache, NavigationCache>();
            services.TryAddScoped(typeof(INavigationBuilder<>), typeof(NavigationBuilder<>));
            services.TryAddScoped(typeof(INavigation<>), typeof(Navigation<>));
            services.TryAddScoped(typeof(IBreadcrumbNavigation<>), typeof(BreadcrumbNavigation<>));
            return new NavigationServiceBuilder(services);
        }

        /// <summary>Registers the source of the <typeparamref name="TNavigationItem"/> tree.</summary>
        /// <typeparam name="TNavigationItem">The item type.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <param name="provider">The source.</param>
        /// <param name="configure">Configures the options.</param>
        /// <returns>The same builder.</returns>
        public static NavigationServiceBuilder AddProvider<TNavigationItem>(this NavigationServiceBuilder builder,
            INavigationProvider<TNavigationItem> provider,
            Action<NavigationOptions<TNavigationItem>>? configure = null)
            where TNavigationItem : NavigationItem<TNavigationItem>, new()
            => builder.AddProvider(provider, (_, options) => configure?.Invoke(options));

        /// <summary>Registers the source of the <typeparamref name="TNavigationItem"/> tree; the first registration wins.</summary>
        /// <remarks>
        /// <paramref name="configure"/> runs once per request scope, so the filter and the activator may use scoped services
        /// (for example <c>IHttpContextAccessor</c> to compare URLs or roles).
        /// </remarks>
        /// <typeparam name="TNavigationItem">The item type.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <param name="provider">The source.</param>
        /// <param name="configure">Configures the options with access to services.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="builder"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
        public static NavigationServiceBuilder AddProvider<TNavigationItem>(this NavigationServiceBuilder builder,
            INavigationProvider<TNavigationItem> provider,
            Action<IServiceProvider, NavigationOptions<TNavigationItem>>? configure)
            where TNavigationItem : NavigationItem<TNavigationItem>, new()
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(provider);

            builder.Services.TryAddSingleton(provider);
            builder.Services.TryAddScoped(serviceProvider =>
            {
                var options = new NavigationOptions<TNavigationItem>();
                configure?.Invoke(serviceProvider, options);
                return options;
            });
            return builder;
        }

        /// <summary>Loads the <typeparamref name="TNavigationItem"/> tree from a JSON file.</summary>
        /// <typeparam name="TNavigationItem">The item type.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <param name="path">The path of the file.</param>
        /// <param name="configure">Configures the options.</param>
        /// <returns>The same builder.</returns>
        public static NavigationServiceBuilder AddJson<TNavigationItem>(this NavigationServiceBuilder builder, string path,
            Action<NavigationOptions<TNavigationItem>>? configure = null)
            where TNavigationItem : NavigationItem<TNavigationItem>, new()
            => builder.AddProvider(new JsonNavigationProvider<TNavigationItem>(path), configure);

        /// <summary>Loads the <typeparamref name="TNavigationItem"/> tree from a JSON file.</summary>
        /// <typeparam name="TNavigationItem">The item type.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <param name="path">The path of the file.</param>
        /// <param name="configure">Configures the options with access to services.</param>
        /// <returns>The same builder.</returns>
        public static NavigationServiceBuilder AddJson<TNavigationItem>(this NavigationServiceBuilder builder, string path,
            Action<IServiceProvider, NavigationOptions<TNavigationItem>>? configure)
            where TNavigationItem : NavigationItem<TNavigationItem>, new()
            => builder.AddProvider(new JsonNavigationProvider<TNavigationItem>(path), configure);

        /// <summary>Loads the <typeparamref name="TNavigationItem"/> tree from an XML file.</summary>
        /// <typeparam name="TNavigationItem">The item type.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <param name="path">The path of the file.</param>
        /// <param name="configure">Configures the options.</param>
        /// <returns>The same builder.</returns>
        public static NavigationServiceBuilder AddXml<TNavigationItem>(this NavigationServiceBuilder builder, string path,
            Action<NavigationOptions<TNavigationItem>>? configure = null)
            where TNavigationItem : NavigationItem<TNavigationItem>, new()
            => builder.AddProvider(new XmlNavigationProvider<TNavigationItem>(path), configure);

        /// <summary>Loads the <typeparamref name="TNavigationItem"/> tree from an XML file.</summary>
        /// <typeparam name="TNavigationItem">The item type.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <param name="path">The path of the file.</param>
        /// <param name="configure">Configures the options with access to services.</param>
        /// <returns>The same builder.</returns>
        public static NavigationServiceBuilder AddXml<TNavigationItem>(this NavigationServiceBuilder builder, string path,
            Action<IServiceProvider, NavigationOptions<TNavigationItem>>? configure)
            where TNavigationItem : NavigationItem<TNavigationItem>, new()
            => builder.AddProvider(new XmlNavigationProvider<TNavigationItem>(path), configure);
    }
}
