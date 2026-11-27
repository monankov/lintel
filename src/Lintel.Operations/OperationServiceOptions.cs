using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations;

/// <summary>
/// Configures the services registered by <c>services.AddOperations(...)</c>.
/// </summary>
public class OperationServiceOptions
{
    /// <summary>
    /// The implementation of <see cref="IOperationExecutor"/>. Defaults to <see cref="OperationExecutor"/>.
    /// </summary>
    /// <exception cref="ArgumentException">If the type is not a non-abstract implementation of <see cref="IOperationExecutor"/>.</exception>
    public Type ExecutorServiceType
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!typeof(IOperationExecutor).IsAssignableFrom(value) || value.IsAbstract || value.IsInterface)
            {
                throw new ArgumentException($"Type '{value.FullName}' must be a non-abstract implementation of {nameof(IOperationExecutor)}.", nameof(value));
            }
            field = value;
        }
    } = typeof(OperationExecutor);

    /// <summary>
    /// The lifetime of the <see cref="IOperationExecutor"/> service. Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime ExecutorServiceLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// The lifetime of operation handlers found by assembly scanning. Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime HandlerServiceLifetime { get; set; } = ServiceLifetime.Transient;
}
