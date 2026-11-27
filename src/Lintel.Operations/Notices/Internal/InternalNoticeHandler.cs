using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Notices.Internal;

internal abstract class InternalHandler
{
    public abstract IEnumerable<Func<INotice, CancellationToken, Task>> GetHandlers(IServiceProvider serviceProvider);
}

internal sealed class InternalNoticeHandler<TNotice> : InternalHandler where TNotice : INotice
{
    public override IEnumerable<Func<INotice, CancellationToken, Task>> GetHandlers(IServiceProvider serviceProvider)
        => serviceProvider
            .GetServices<INoticeHandler<TNotice>>()
            .Select(handler => new Func<INotice, CancellationToken, Task>(
                (notice, cancellationToken) => handler.HandleAsync((TNotice)notice, cancellationToken)))
            .ToArray();
}
