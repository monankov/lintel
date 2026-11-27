namespace Lintel.Notices;

/// <summary>
/// Base class for asynchronous notice handlers.
/// </summary>
/// <typeparam name="TNotice">The type of notice being handled.</typeparam>
public abstract class AsyncNoticeHandler<TNotice> : INoticeHandler<TNotice> where TNotice : INotice
{
    /// <inheritdoc />
    public abstract Task HandleAsync(TNotice notice, CancellationToken cancellationToken);
}
