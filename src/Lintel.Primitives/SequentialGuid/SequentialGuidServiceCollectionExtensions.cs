using Lintel.Primitives;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration of <see cref="ISequentialGuidProvider"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class SequentialGuidServiceCollectionExtensions
{
    /// <summary>
    /// Adds a singleton <see cref="ISequentialGuidProvider"/> based on <see cref="Guid.NewGuid"/>
    /// and <see cref="UniqueSequentialGuidTimestampProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSequentialGuidProvider(this IServiceCollection services)
        => AddSequentialGuidProvider(services, Guid.NewGuid);

    /// <summary>
    /// Adds a singleton <see cref="ISequentialGuidProvider"/> with a custom random part
    /// and <see cref="UniqueSequentialGuidTimestampProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="initializer">Supplies the random part.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSequentialGuidProvider(this IServiceCollection services,
        SequentialGuidInitializer initializer)
        => AddSequentialGuidProvider(services, initializer, new UniqueSequentialGuidTimestampProvider().GetTimestamp);

    /// <summary>
    /// Adds a singleton <see cref="ISequentialGuidProvider"/> with a custom random part and timestamp.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="initializer">Supplies the random part.</param>
    /// <param name="timestampProvider">Supplies the timestamp.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddSequentialGuidProvider(this IServiceCollection services,
        SequentialGuidInitializer initializer,
        SequentialGuidTimestampProvider timestampProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(timestampProvider);

        services.TryAddSingleton<ISequentialGuidProvider>(new SequentialGuidProvider(initializer, timestampProvider));
        return services;
    }
}
