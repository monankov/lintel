using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lintel.Operations.Internal;

internal static class HandlerScanner
{
    /// <summary>
    /// Registers every concrete, non-generic class from <paramref name="assemblies"/> that implements
    /// one of <paramref name="openInterfaces"/>, once per closed interface it implements.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan; <see langword="null"/> registers nothing.</param>
    /// <param name="openInterfaces">Open generic handler interfaces to look for.</param>
    /// <param name="lifetime">The lifetime of registered handlers.</param>
    /// <param name="allowMultiple">
    /// <see langword="false"/>: the first registration of an interface wins (single handler per operation).
    /// <see langword="true"/>: every implementation is registered, but the same implementation only once.
    /// </param>
    public static void Register(IServiceCollection services,
        IEnumerable<Assembly>? assemblies,
        Type[] openInterfaces,
        ServiceLifetime lifetime,
        bool allowMultiple)
    {
        if (assemblies is null)
        {
            return;
        }

        var ownAssembly = typeof(HandlerScanner).Assembly;

        var implementationTypes = assemblies
            .Where(assembly => !assembly.IsDynamic && assembly != ownAssembly)
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters);

        foreach (var implementationType in implementationTypes)
        {
            var serviceTypes = implementationType.ImplementedInterfaces
                .Where(@interface => @interface.IsGenericType
                    && openInterfaces.Contains(@interface.GetGenericTypeDefinition()));

            foreach (var serviceType in serviceTypes)
            {
                var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);
                if (allowMultiple)
                {
                    services.TryAddEnumerable(descriptor);
                }
                else
                {
                    services.TryAdd(descriptor);
                }
            }
        }
    }

    public static bool IsOpenGenericImplementationOf(Type type, Type openInterface)
        => type.IsGenericTypeDefinition
            && !type.IsAbstract
            && !type.IsInterface
            && type.GetInterfaces().Any(@interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == openInterface);
}
