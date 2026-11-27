using System.Collections.Concurrent;
using Lintel.Notices;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Tests.Notices;

public class NoticePublisherTests
{
    public sealed class Journal
    {
        public ConcurrentQueue<string> Entries { get; } = new();
    }

    public class ConfirmationNotice : INotice
    {
        public required string Message { get; init; }
    }

    public class PushConfirmationHandler(Journal journal) : INoticeHandler<ConfirmationNotice>
    {
        public Task HandleAsync(ConfirmationNotice notice, CancellationToken cancellationToken)
        {
            journal.Entries.Enqueue($"push:{notice.Message}");
            return Task.CompletedTask;
        }
    }

    public class TicketConfirmationHandler(Journal journal) : NoticeHandler<ConfirmationNotice>
    {
        protected override void Handle(ConfirmationNotice notice) => journal.Entries.Enqueue($"ticket:{notice.Message}");
    }

    public class MailConfirmationHandler(Journal journal) : AsyncNoticeHandler<ConfirmationNotice>
    {
        public override async Task HandleAsync(ConfirmationNotice notice, CancellationToken cancellationToken)
        {
            await Task.Yield();
            journal.Entries.Enqueue($"mail:{notice.Message}");
        }
    }

    public class ThrowingNotice : INotice
    {
        public required string Title { get; init; }
    }

    public class ThrowingNoticeHandler : NoticeHandler<ThrowingNotice>
    {
        protected override void Handle(ThrowingNotice notice) => throw new NotImplementedException(notice.Title);
    }

    public class UnhandledNotice : INotice;

    public class DerivedConfirmationNotice : ConfirmationNotice;

    private static (INoticePublisher Publisher, Journal Journal) CreatePublisher()
    {
        var journal = new Journal();
        var provider = new ServiceCollection()
            .AddSingleton(journal)
            .AddNotices(typeof(NoticePublisherTests).Assembly)
            .BuildServiceProvider();
        return (provider.GetRequiredService<INoticePublisher>(), journal);
    }

    [Fact]
    public async Task Publish_invokes_all_handlers_of_notice()
    {
        var (publisher, journal) = CreatePublisher();

        await publisher.PublishAsync(new ConfirmationNotice { Message = "Message" }, TestContext.Current.CancellationToken);

        journal.Entries.Should().BeEquivalentTo(["push:Message", "ticket:Message", "mail:Message"]);
    }

    [Fact]
    public async Task Publish_propagates_handler_exception()
    {
        var (publisher, _) = CreatePublisher();

        var act = () => publisher.PublishAsync(new ThrowingNotice { Title = "Title 1" }, TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<NotImplementedException>().WithMessage("Title 1");
    }

    [Fact]
    public async Task Publish_without_handlers_completes()
    {
        var (publisher, _) = CreatePublisher();

        var act = () => publisher.PublishAsync(new UnhandledNotice(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_uses_runtime_type_of_notice_exactly()
    {
        // Handlers are resolved for the exact runtime type: handlers of a base notice are not invoked.
        var (publisher, journal) = CreatePublisher();

        await publisher.PublishAsync(new DerivedConfirmationNotice { Message = "Derived" }, TestContext.Current.CancellationToken);

        journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_throws_when_notice_is_null()
    {
        var (publisher, _) = CreatePublisher();

        var act = () => publisher.PublishAsync(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }
}
