namespace Lintel.Notices;

/// <summary>
/// Publishes notices to all their registered handlers.
/// </summary>
public interface INoticePublisher
{
    /// <summary>
    /// Publishes a notice to all handlers registered for its runtime type.
    /// </summary>
    /// <param name="notice">The notice to publish.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that completes when all handlers have completed.</returns>
    Task PublishAsync(INotice notice, CancellationToken cancellationToken = default);
}
