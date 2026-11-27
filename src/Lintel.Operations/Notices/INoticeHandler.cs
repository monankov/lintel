namespace Lintel.Notices;

/// <summary>
/// Defines a handler for a notice.
/// </summary>
/// <typeparam name="TNotice">The type of notice being handled.</typeparam>
public interface INoticeHandler<in TNotice> where TNotice : INotice
{
    /// <summary>
    /// Handles a notice.
    /// </summary>
    /// <param name="notice">The notice to be handled.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the handling.</returns>
    Task HandleAsync(TNotice notice, CancellationToken cancellationToken);
}
