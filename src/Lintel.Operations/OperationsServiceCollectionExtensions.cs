using System.Reflection;
using Lintel.Operations;
using Lintel.Operations.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration of operation services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class OperationsServiceCollectionExtensions
{
    private static readonly Type[] _handlerInterfaces = [typeof(IOperationHandler<,>), typeof(IOperationHandler<>)];

    /// <summary>
    /// Adds <see cref="IOperationExecutor"/> and the operation handlers found in <paramref name="assembliesToScan"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembliesToScan">Assemblies to search for operation handlers.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddOperations(this IServiceCollection services, params Assembly[] assembliesToScan)
        => AddOperations(services, null, assembliesToScan);

    /// <summary>
    /// Adds <see cref="IOperationExecutor"/> and the operation handlers found in <paramref name="assembliesToScan"/>.
    /// </summary>
    /// <remarks>
    /// Only one handler per operation is registered: if several handlers exist, the first one found
    /// (or one registered earlier) wins. Calling this method again does not register duplicates.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configures <see cref="OperationServiceOptions"/>.</param>
    /// <param name="assembliesToScan">Assemblies to search for operation handlers.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddOperations(this IServiceCollection services,
        Action<OperationServiceOptions>? configureOptions,
        params Assembly[] assembliesToScan)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OperationServiceOptions();
        configureOptions?.Invoke(options);

        services.TryAdd(new ServiceDescriptor(typeof(IOperationExecutor), options.ExecutorServiceType, options.ExecutorServiceLifetime));

        HandlerScanner.Register(services, assembliesToScan, _handlerInterfaces, options.HandlerServiceLifetime, allowMultiple: false);

        return services;
    }

    /// <summary>
    /// Adds an operation filter. Filters run in registration order, the first one being the outermost.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="filterType">
    /// An open generic type implementing <see cref="IOperationFilter{TOperation, TResult}"/>
    /// (for example <c>typeof(LoggingFilter&lt;,&gt;)</c>), or a closed type implementing it for specific operations.
    /// </param>
    /// <param name="lifetime">The lifetime of the filter.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> or <paramref name="filterType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="filterType"/> does not implement <see cref="IOperationFilter{TOperation, TResult}"/>.</exception>
    public static IServiceCollection AddOperationFilter(this IServiceCollection services,
        Type filterType,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(filterType);

        var openFilterInterface = typeof(IOperationFilter<,>);

        if (HandlerScanner.IsOpenGenericImplementationOf(filterType, openFilterInterface))
        {
            services.TryAddEnumerable(new ServiceDescriptor(openFilterInterface, filterType, lifetime));
            return services;
        }

        var closedInterfaces = filterType.IsAbstract || filterType.IsInterface || filterType.ContainsGenericParameters
            ? []
            : filterType.GetInterfaces()
                .Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == openFilterInterface)
                .ToArray();

        if (closedInterfaces.Length == 0)
        {
            throw new ArgumentException($"Type '{filterType.FullName}' must be a non-abstract implementation of IOperationFilter<TOperation, TResult>.", nameof(filterType));
        }

        foreach (var closedInterface in closedInterfaces)
        {
            services.TryAddEnumerable(new ServiceDescriptor(closedInterface, filterType, lifetime));
        }

        return services;
    }
}
