using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Internal;

internal abstract class InternalHandler<TResult>
{
    public abstract Task<TResult> HandleAsync(IBaseOperation operation,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class InternalOperationHandler<TOperation, TResult> : InternalHandler<TResult>
    where TOperation : IOperation<TResult>
{
    public override Task<TResult> HandleAsync(IBaseOperation operation,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedOperation = (TOperation)operation;

        Task<TResult> Handler() => serviceProvider
            .GetService<IOperationHandler<TOperation, TResult>>()?
            .HandleAsync(typedOperation, cancellationToken)
            ?? throw new InvalidOperationException($"Operation handler for operation '{typeof(TOperation).FullName}' was not found");

        var filters = serviceProvider.GetServices<IOperationFilter<TOperation, TResult>>().ToArray();

        // The first registered filter becomes the outermost one.
        OperationHandlerDelegate<TResult> pipeline = Handler;
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var next = pipeline;
            pipeline = () => filter.FilterAsync(typedOperation, cancellationToken, next);
        }

        return pipeline();
    }
}
