namespace Lintel.Notices;

/// <summary>
/// Defines how the handlers of a notice are invoked.
/// </summary>
/// <seealso cref="SequentialNoticePublishStrategy"/>
/// <seealso cref="ParallelNoticePublishStrategy"/>
public interface INoticePublishStrategy
{
    /// <summary>
    /// Invokes the handlers of a notice.
    /// </summary>
    /// <param name="handlers">The handlers, in registration order.</param>
    /// <param name="notice">The notice being published.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that completes when the handlers have completed.</returns>
    Task PublishAsync(IEnumerable<Func<INotice, CancellationToken, Task>> handlers,
        INotice notice,
        CancellationToken cancellationToken);
}
