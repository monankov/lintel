namespace Lintel.Operations;

/// <summary>
/// Represents the next step of the operation pipeline: the next filter or the handler itself.
/// </summary>
/// <typeparam name="TResult">The type of result from the handler.</typeparam>
/// <returns>A task whose result is the operation result.</returns>
public delegate Task<TResult> OperationHandlerDelegate<TResult>();
