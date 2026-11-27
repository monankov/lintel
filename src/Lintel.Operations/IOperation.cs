namespace Lintel.Operations;

/// <summary>
/// Represents an operation that returns a result. Usually associated with a query.
/// </summary>
/// <typeparam name="TResult">The type of the operation result.</typeparam>
public interface IOperation<out TResult> : IBaseOperation;

/// <summary>
/// Represents an operation that does not return a result. Usually associated with a command.
/// </summary>
public interface IOperation : IOperation<VoidResult>;
