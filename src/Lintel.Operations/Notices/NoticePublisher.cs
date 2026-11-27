using System.Collections.Concurrent;
using Lintel.Notices.Internal;

namespace Lintel.Notices;

/// <summary>
/// Default implementation of <see cref="INoticePublisher"/> that resolves handlers from an
/// <see cref="IServiceProvider"/> and invokes them with an <see cref="INoticePublishStrategy"/>.
/// </summary>
public class NoticePublisher : INoticePublisher
{
    private static readonly ConcurrentDictionary<Type, InternalHandler> _cache = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly INoticePublishStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoticePublisher"/> class
    /// with the <see cref="SequentialNoticePublishStrategy"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve handlers from.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public NoticePublisher(IServiceProvider serviceProvider)
        : this(serviceProvider, SequentialNoticePublishStrategy.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoticePublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve handlers from.</param>
    /// <param name="strategy">The strategy used to invoke handlers.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public NoticePublisher(IServiceProvider serviceProvider, INoticePublishStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(strategy);

        _serviceProvider = serviceProvider;
        _strategy = strategy;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If <paramref name="notice"/> is <see langword="null"/>.</exception>
    public Task PublishAsync(INotice notice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var handler = _cache.GetOrAdd(notice.GetType(),
            static noticeType => (InternalHandler)Activator.CreateInstance(
                typeof(InternalNoticeHandler<>).MakeGenericType(noticeType))!);

        return PublishAsync(handler.GetHandlers(_serviceProvider), notice, cancellationToken);
    }

    /// <summary>
    /// Invokes the handlers of a notice. By default delegates to the configured <see cref="INoticePublishStrategy"/>.
    /// </summary>
    /// <param name="handlers">The handlers, in registration order.</param>
    /// <param name="notice">The notice being published.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that completes when the handlers have completed.</returns>
    protected virtual Task PublishAsync(IEnumerable<Func<INotice, CancellationToken, Task>> handlers,
        INotice notice,
        CancellationToken cancellationToken)
        => _strategy.PublishAsync(handlers, notice, cancellationToken);
}
