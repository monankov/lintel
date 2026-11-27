namespace Lintel.Operations;

/// <summary>
/// Base class for asynchronous operation handlers.
/// </summary>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
/// <typeparam name="TResult">The type of result from the handler.</typeparam>
public abstract class AsyncOperationHandler<TOperation, TResult> : IOperationHandler<TOperation, TResult>
    where TOperation : IOperation<TResult>
{
    /// <inheritdoc />
    public abstract Task<TResult> HandleAsync(TOperation operation, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for asynchronous operation handlers that do not return a result.
/// </summary>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
public abstract class AsyncOperationHandler<TOperation> : IOperationHandler<TOperation>
    where TOperation : IOperation
{
    async Task<VoidResult> IOperationHandler<TOperation, VoidResult>.HandleAsync(TOperation operation, CancellationToken cancellationToken)
    {
        await HandleAsync(operation, cancellationToken).ConfigureAwait(false);
        return VoidResult.Value;
    }

    /// <summary>
    /// Handles an operation.
    /// </summary>
    /// <param name="operation">The operation to be handled.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the handling.</returns>
    protected abstract Task HandleAsync(TOperation operation, CancellationToken cancellationToken);
}
