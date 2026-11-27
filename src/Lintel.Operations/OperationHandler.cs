namespace Lintel.Operations;

/// <summary>
/// Base class for synchronous operation handlers.
/// </summary>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
/// <typeparam name="TResult">The type of result from the handler.</typeparam>
public abstract class OperationHandler<TOperation, TResult> : IOperationHandler<TOperation, TResult>
    where TOperation : IOperation<TResult>
{
    Task<TResult> IOperationHandler<TOperation, TResult>.HandleAsync(TOperation operation, CancellationToken cancellationToken)
        => Task.FromResult(Handle(operation));

    /// <summary>
    /// Handles an operation.
    /// </summary>
    /// <param name="operation">The operation to be handled.</param>
    /// <returns>The result of the operation.</returns>
    protected abstract TResult Handle(TOperation operation);
}

/// <summary>
/// Base class for synchronous operation handlers that do not return a result.
/// </summary>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
public abstract class OperationHandler<TOperation> : IOperationHandler<TOperation>
    where TOperation : IOperation
{
    Task<VoidResult> IOperationHandler<TOperation, VoidResult>.HandleAsync(TOperation operation, CancellationToken cancellationToken)
    {
        Handle(operation);
        return VoidResult.TaskValue;
    }

    /// <summary>
    /// Handles an operation.
    /// </summary>
    /// <param name="operation">The operation to be handled.</param>
    protected abstract void Handle(TOperation operation);
}
