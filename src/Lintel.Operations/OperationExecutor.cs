using System.Collections.Concurrent;
using Lintel.Operations.Internal;

namespace Lintel.Operations;

/// <summary>
/// Default implementation of <see cref="IOperationExecutor"/> that resolves handlers and filters
/// from an <see cref="IServiceProvider"/>.
/// </summary>
public class OperationExecutor : IOperationExecutor
{
    private static readonly ConcurrentDictionary<Type, object> _cache = new();

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve handlers and filters from.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public OperationExecutor(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If <paramref name="operation"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If no handler is registered for the operation.</exception>
    public virtual Task<TResult> ExecuteAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var handler = (InternalHandler<TResult>)_cache.GetOrAdd(operation.GetType(),
            static (operationType, resultType) => Activator.CreateInstance(
                typeof(InternalOperationHandler<,>).MakeGenericType(operationType, resultType))!,
            typeof(TResult));

        return handler.HandleAsync(operation, _serviceProvider, cancellationToken);
    }
}
