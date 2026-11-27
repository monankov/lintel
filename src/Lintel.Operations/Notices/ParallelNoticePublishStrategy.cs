namespace Lintel.Notices;

/// <summary>
/// Starts all handlers at once and waits for all of them to complete.
/// </summary>
/// <remarks>
/// All handlers run even if some of them fail. A single failure is rethrown as is;
/// several failures are thrown as an <see cref="AggregateException"/>.
/// Handlers share the same service scope, so they must not use non-thread-safe scoped services
/// (such as an EF Core <c>DbContext</c>) concurrently.
/// </remarks>
public sealed class ParallelNoticePublishStrategy : INoticePublishStrategy
{
    /// <summary>
    /// The shared instance of the strategy.
    /// </summary>
    public static ParallelNoticePublishStrategy Instance { get; } = new();

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<Func<INotice, CancellationToken, Task>> handlers,
        INotice notice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var all = Task.WhenAll(handlers.Select(handler => InvokeAsync(handler, notice, cancellationToken)));

        try
        {
            await all.ConfigureAwait(false);
        }
        catch when (all.Exception is { InnerExceptions.Count: > 1 } aggregate)
        {
            throw aggregate.Flatten();
        }
    }

    // Turns synchronous exceptions into faulted tasks so that every handler gets started.
    private static async Task InvokeAsync(Func<INotice, CancellationToken, Task> handler,
        INotice notice,
        CancellationToken cancellationToken)
        => await handler(notice, cancellationToken).ConfigureAwait(false);
}
