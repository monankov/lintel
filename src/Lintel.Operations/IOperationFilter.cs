namespace Lintel.Operations;

/// <summary>
/// Defines a filter that wraps operation handling (logging, validation, transactions, etc.).
/// </summary>
/// <remarks>
/// Filters are resolved as <c>IEnumerable&lt;IOperationFilter&lt;TOperation, TResult&gt;&gt;</c> and run in
/// registration order: the first registered filter is the outermost one. Open generic filters can be
/// registered with <c>services.AddOperationFilter(typeof(MyFilter&lt;,&gt;))</c>.
/// </remarks>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
/// <typeparam name="TResult">The type of result from the handler.</typeparam>
public interface IOperationFilter<in TOperation, TResult>
    where TOperation : IOperation<TResult>
{
    /// <summary>
    /// Filters an executed operation.
    /// </summary>
    /// <param name="operation">The operation being executed.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <param name="next">The next filter in the pipeline or the operation handler itself.</param>
    /// <returns>A task whose result is the operation result.</returns>
    Task<TResult> FilterAsync(TOperation operation, CancellationToken cancellationToken, OperationHandlerDelegate<TResult> next);
}
