namespace Lintel.Notices;

/// <summary>
/// Invokes handlers one after another in registration order. The first exception stops the publishing.
/// This is the default strategy.
/// </summary>
public sealed class SequentialNoticePublishStrategy : INoticePublishStrategy
{
    /// <summary>
    /// The shared instance of the strategy.
    /// </summary>
    public static SequentialNoticePublishStrategy Instance { get; } = new();

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<Func<INotice, CancellationToken, Task>> handlers,
        INotice notice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        foreach (var handler in handlers)
        {
            await handler(notice, cancellationToken).ConfigureAwait(false);
        }
    }
}
