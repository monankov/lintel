using Lintel.Notices;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Tests.Notices;

public class NoticesServiceCollectionExtensionsTests
{
    public class CustomNotice : INotice;

    public class CustomNoticeHandler : NoticeHandler<CustomNotice>
    {
        protected override void Handle(CustomNotice notice) { }
    }

    public class CustomPublisher(IServiceProvider serviceProvider) : NoticePublisher(serviceProvider);

    [Fact]
    public void AddNotices_registers_publisher_strategy_and_handlers()
    {
        var provider = new ServiceCollection()
            .AddNotices(GetType().Assembly)
            .BuildServiceProvider();

        provider.GetService<INoticePublisher>().Should().BeOfType<NoticePublisher>();
        provider.GetService<INoticePublishStrategy>().Should().BeSameAs(SequentialNoticePublishStrategy.Instance);
        provider.GetServices<INoticeHandler<CustomNotice>>().Should().ContainSingle()
            .Which.Should().BeOfType<CustomNoticeHandler>();
    }

    [Fact]
    public void AddNotices_called_twice_does_not_duplicate_handlers()
    {
        var provider = new ServiceCollection()
            .AddNotices(GetType().Assembly)
            .AddNotices(GetType().Assembly)
            .BuildServiceProvider();

        provider.GetServices<INoticeHandler<CustomNotice>>().Should().ContainSingle();
    }

    [Fact]
    public void AddNotices_applies_options()
    {
        var services = new ServiceCollection().AddNotices(options =>
        {
            options.PublisherServiceType = typeof(CustomPublisher);
            options.PublisherServiceLifetime = ServiceLifetime.Scoped;
            options.HandlerServiceLifetime = ServiceLifetime.Singleton;
            options.PublishStrategy = ParallelNoticePublishStrategy.Instance;
        }, GetType().Assembly);

        services.Single(d => d.ServiceType == typeof(INoticePublisher))
            .Should().Match<ServiceDescriptor>(d => d.ImplementationType == typeof(CustomPublisher) && d.Lifetime == ServiceLifetime.Scoped);
        services.Single(d => d.ServiceType == typeof(INoticeHandler<CustomNotice>))
            .Lifetime.Should().Be(ServiceLifetime.Singleton);
        services.Single(d => d.ServiceType == typeof(INoticePublishStrategy))
            .ImplementationInstance.Should().BeSameAs(ParallelNoticePublishStrategy.Instance);
    }

    [Fact]
    public void Custom_publisher_with_single_constructor_is_resolved()
    {
        var provider = new ServiceCollection()
            .AddNotices(options => options.PublisherServiceType = typeof(CustomPublisher))
            .BuildServiceProvider();

        provider.GetService<INoticePublisher>().Should().BeOfType<CustomPublisher>();
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(INoticePublisher))]
    public void PublisherServiceType_rejects_invalid_types(Type type)
    {
        var options = new NoticeServiceOptions();

        var act = () => options.PublisherServiceType = type;

        act.Should().Throw<ArgumentException>();
    }
}
