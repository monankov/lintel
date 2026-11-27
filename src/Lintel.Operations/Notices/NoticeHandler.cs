namespace Lintel.Notices;

/// <summary>
/// Base class for synchronous notice handlers.
/// </summary>
/// <typeparam name="TNotice">The type of notice being handled.</typeparam>
public abstract class NoticeHandler<TNotice> : INoticeHandler<TNotice> where TNotice : INotice
{
    Task INoticeHandler<TNotice>.HandleAsync(TNotice notice, CancellationToken cancellationToken)
    {
        Handle(notice);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a notice.
    /// </summary>
    /// <param name="notice">The notice to be handled.</param>
    protected abstract void Handle(TNotice notice);
}
