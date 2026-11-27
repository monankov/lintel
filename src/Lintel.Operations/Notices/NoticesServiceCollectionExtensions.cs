using System.Reflection;
using Lintel.Notices;
using Lintel.Operations.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration of notice services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class NoticesServiceCollectionExtensions
{
    private static readonly Type[] _handlerInterfaces = [typeof(INoticeHandler<>)];

    /// <summary>
    /// Adds <see cref="INoticePublisher"/> and the notice handlers found in <paramref name="assembliesToScan"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembliesToScan">Assemblies to search for notice handlers.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddNotices(this IServiceCollection services, params Assembly[] assembliesToScan)
        => AddNotices(services, null, assembliesToScan);

    /// <summary>
    /// Adds <see cref="INoticePublisher"/> and the notice handlers found in <paramref name="assembliesToScan"/>.
    /// </summary>
    /// <remarks>
    /// Every handler of a notice is registered. Calling this method again with the same assemblies
    /// does not register the same handler twice.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configures <see cref="NoticeServiceOptions"/>.</param>
    /// <param name="assembliesToScan">Assemblies to search for notice handlers.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddNotices(this IServiceCollection services,
        Action<NoticeServiceOptions>? configureOptions,
        params Assembly[] assembliesToScan)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new NoticeServiceOptions();
        configureOptions?.Invoke(options);

        services.TryAddSingleton(options.PublishStrategy);
        services.TryAdd(new ServiceDescriptor(typeof(INoticePublisher), options.PublisherServiceType, options.PublisherServiceLifetime));

        HandlerScanner.Register(services, assembliesToScan, _handlerInterfaces, options.HandlerServiceLifetime, allowMultiple: true);

        return services;
    }
}
