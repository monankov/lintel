using System.Reflection;
using Lintel.Stateflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lintel.Stateflows
{
    /// <summary>
    /// Configures the services registered by <c>services.AddStateflows(...)</c>.
    /// </summary>
    public class StateflowServiceOptions
    {
        /// <summary>The implementation of <see cref="IStateMachineProvider"/>. Defaults to <see cref="StateMachineProvider"/>.</summary>
        /// <exception cref="ArgumentException">If the type is not a non-abstract implementation of <see cref="IStateMachineProvider"/>.</exception>
        public Type ProviderServiceType
        {
            get;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (!typeof(IStateMachineProvider).IsAssignableFrom(value) || value.IsAbstract || value.IsInterface)
                {
                    throw new ArgumentException($"Type '{value.FullName}' must be a non-abstract implementation of {nameof(IStateMachineProvider)}.", nameof(value));
                }
                field = value;
            }
        } = typeof(StateMachineProvider);

        /// <summary>The lifetime of <see cref="IStateMachineProvider"/>. Defaults to <see cref="ServiceLifetime.Transient"/>.</summary>
        public ServiceLifetime ProviderServiceLifetime { get; set; } = ServiceLifetime.Transient;
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registration of stateflow services.
    /// </summary>
    public static class StateflowsServiceCollectionExtensions
    {
        private static readonly Type[] _configurationInterfaces = [typeof(IStateMachineConfiguration<,,>), typeof(IStateMachineConfigurator<,,>)];

        /// <summary>
        /// Adds <see cref="IStateMachineProvider"/>, the state machine configurations and the stateflow providers found in
        /// <paramref name="assembliesToScan"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assembliesToScan">Assemblies to search.</param>
        /// <returns>The same service collection.</returns>
        public static IServiceCollection AddStateflows(this IServiceCollection services, params Assembly[] assembliesToScan)
            => AddStateflows(services, null, assembliesToScan);

        /// <summary>
        /// Adds <see cref="IStateMachineProvider"/>, the state machine configurations and the stateflow providers found in
        /// <paramref name="assembliesToScan"/>.
        /// </summary>
        /// <remarks>
        /// Every configuration of an entity is registered and applied (calling this method again does not duplicate them);
        /// only the first <see cref="IStateflowProvider{TEntity, TKey, TState, TTrigger}"/> of a type is registered.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Configures <see cref="StateflowServiceOptions"/>.</param>
        /// <param name="assembliesToScan">Assemblies to search.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
        public static IServiceCollection AddStateflows(this IServiceCollection services,
            Action<StateflowServiceOptions>? configureOptions,
            params Assembly[] assembliesToScan)
        {
            ArgumentNullException.ThrowIfNull(services);

            var options = new StateflowServiceOptions();
            configureOptions?.Invoke(options);

            services.TryAdd(new ServiceDescriptor(typeof(IStateMachineProvider), options.ProviderServiceType, options.ProviderServiceLifetime));

            var ownAssembly = typeof(IStateMachineProvider).Assembly;
            var types = (assembliesToScan ?? [])
                .Where(assembly => !assembly.IsDynamic && assembly != ownAssembly)
                .Distinct()
                .SelectMany(assembly => assembly.DefinedTypes)
                .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters);

            foreach (var type in types)
            {
                foreach (var @interface in type.ImplementedInterfaces.Where(i => i.IsGenericType))
                {
                    var definition = @interface.GetGenericTypeDefinition();
                    if (_configurationInterfaces.Contains(definition))
                    {
                        services.TryAddEnumerable(ServiceDescriptor.Transient(@interface, type));
                    }
                    else if (definition == typeof(IStateflowProvider<,,,>))
                    {
                        services.TryAddTransient(@interface, type);
                    }
                }
            }

            return services;
        }
    }
}
