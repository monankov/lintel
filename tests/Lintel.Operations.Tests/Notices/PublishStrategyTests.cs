using System.Collections.Concurrent;
using Lintel.Notices;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Tests.Notices;

public class PublishStrategyTests
{
    /// <summary>
    /// Completes only when <see cref="Participants"/> handlers are inside it at the same time.
    /// </summary>
    public sealed class Rendezvous
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public int Participants { get; init; } = 2;

        public Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) == Participants)
            {
                _allArrived.SetResult();
            }
            return _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    public sealed class Journal
    {
        public ConcurrentQueue<string> Entries { get; } = new();
    }

    public class ConcurrentNotice : INotice;

    public class FirstConcurrentHandler(Rendezvous rendezvous) : INoticeHandler<ConcurrentNotice>
    {
        public Task HandleAsync(ConcurrentNotice notice, CancellationToken cancellationToken) => rendezvous.ArriveAsync();
    }

    public class SecondConcurrentHandler(Rendezvous rendezvous) : INoticeHandler<ConcurrentNotice>
    {
        public Task HandleAsync(ConcurrentNotice notice, CancellationToken cancellationToken) => rendezvous.ArriveAsync();
    }

    public class FailingNotice : INotice;

    public class FirstFailingHandler : NoticeHandler<FailingNotice>
    {
        protected override void Handle(FailingNotice notice) => throw new InvalidOperationException("first");
    }

    public class SecondFailingHandler : INoticeHandler<FailingNotice>
    {
        public async Task HandleAsync(FailingNotice notice, CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("second");
        }
    }

    public class SucceedingHandler(Journal journal) : INoticeHandler<FailingNotice>
    {
        public Task HandleAsync(FailingNotice notice, CancellationToken cancellationToken)
        {
            journal.Entries.Enqueue("succeeded");
            return Task.CompletedTask;
        }
    }

    public class SingleFailureNotice : INotice;

    public class SingleFailureHandler : NoticeHandler<SingleFailureNotice>
    {
        protected override void Handle(SingleFailureNotice notice) => throw new InvalidOperationException("only");
    }

    public class SingleFailureSucceedingHandler : NoticeHandler<SingleFailureNotice>
    {
        protected override void Handle(SingleFailureNotice notice) { }
    }

    private static (INoticePublisher Publisher, Journal Journal) CreatePublisher(INoticePublishStrategy strategy)
    {
        var journal = new Journal();
        var provider = new ServiceCollection()
            .AddSingleton(journal)
            .AddSingleton(new Rendezvous())
            .AddNotices(options => options.PublishStrategy = strategy, typeof(PublishStrategyTests).Assembly)
            .BuildServiceProvider();
        return (provider.GetRequiredService<INoticePublisher>(), journal);
    }

    [Fact]
    public async Task Parallel_strategy_runs_handlers_concurrently()
    {
        var (publisher, _) = CreatePublisher(ParallelNoticePublishStrategy.Instance);

        var act = () => publisher.PublishAsync(new ConcurrentNotice(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Sequential_strategy_runs_handlers_one_by_one()
    {
        var (publisher, _) = CreatePublisher(SequentialNoticePublishStrategy.Instance);

        // The first handler waits for the second one, which never starts while the first is running.
        var act = () => publisher.PublishAsync(new ConcurrentNotice(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Parallel_strategy_runs_all_handlers_and_aggregates_failures()
    {
        var (publisher, journal) = CreatePublisher(ParallelNoticePublishStrategy.Instance);

        var act = () => publisher.PublishAsync(new FailingNotice(), TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowExactlyAsync<AggregateException>();
        exception.Which.InnerExceptions.Select(e => e.Message).Should().BeEquivalentTo(["first", "second"]);
        journal.Entries.Should().Equal("succeeded");
    }

    [Fact]
    public async Task Parallel_strategy_rethrows_single_failure_as_is()
    {
        var (publisher, _) = CreatePublisher(ParallelNoticePublishStrategy.Instance);

        var act = () => publisher.PublishAsync(new SingleFailureNotice(), TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("only");
    }

    [Fact]
    public async Task Sequential_strategy_stops_on_first_failure()
    {
        var (publisher, _) = CreatePublisher(SequentialNoticePublishStrategy.Instance);

        var act = () => publisher.PublishAsync(new FailingNotice(), TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}
