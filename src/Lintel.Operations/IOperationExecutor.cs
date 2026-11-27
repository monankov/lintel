namespace Lintel.Operations;

/// <summary>
/// Executes operations by passing them through the filter pipeline to a single handler.
/// </summary>
public interface IOperationExecutor
{
    /// <summary>
    /// Executes an operation.
    /// </summary>
    /// <typeparam name="TResult">The type of the operation result.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task whose result is the handler's response.</returns>
    Task<TResult> ExecuteAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken = default);
}
