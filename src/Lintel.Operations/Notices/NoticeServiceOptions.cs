using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Notices;

/// <summary>
/// Configures the services registered by <c>services.AddNotices(...)</c>.
/// </summary>
public class NoticeServiceOptions
{
    /// <summary>
    /// The implementation of <see cref="INoticePublisher"/>. Defaults to <see cref="NoticePublisher"/>.
    /// </summary>
    /// <exception cref="ArgumentException">If the type is not a non-abstract implementation of <see cref="INoticePublisher"/>.</exception>
    public Type PublisherServiceType
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!typeof(INoticePublisher).IsAssignableFrom(value) || value.IsAbstract || value.IsInterface)
            {
                throw new ArgumentException($"Type '{value.FullName}' must be a non-abstract implementation of {nameof(INoticePublisher)}.", nameof(value));
            }
            field = value;
        }
    } = typeof(NoticePublisher);

    /// <summary>
    /// The lifetime of the <see cref="INoticePublisher"/> service. Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime PublisherServiceLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// The lifetime of notice handlers found by assembly scanning. Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime HandlerServiceLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// The strategy used to invoke handlers. Defaults to <see cref="SequentialNoticePublishStrategy"/>.
    /// </summary>
    public INoticePublishStrategy PublishStrategy
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = SequentialNoticePublishStrategy.Instance;
}
