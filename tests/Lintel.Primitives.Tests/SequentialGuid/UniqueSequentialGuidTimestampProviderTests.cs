namespace Lintel.Primitives.Tests.SequentialGuid;

public class UniqueSequentialGuidTimestampProviderTests
{
    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset _start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Returns_clock_time_when_calls_are_far_apart()
    {
        var clock = new ManualTimeProvider(_start);
        var provider = new UniqueSequentialGuidTimestampProvider(clock);

        var first = provider.GetTimestamp();
        clock.Now = _start.AddSeconds(1);
        var second = provider.GetTimestamp();

        first.Should().Be(_start.UtcDateTime);
        second.Should().Be(_start.AddSeconds(1).UtcDateTime);
        second.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Increments_when_clock_does_not_move()
    {
        var provider = new UniqueSequentialGuidTimestampProvider(new ManualTimeProvider(_start));

        var timestamps = Enumerable.Range(0, 5).Select(_ => provider.GetTimestamp()).ToList();

        timestamps.Zip(timestamps.Skip(1), (a, b) => (b - a).TotalMilliseconds)
            .Should().AllSatisfy(delta => delta.Should().Be(UniqueSequentialGuidTimestampProvider.IncrementMs));
    }

    [Fact]
    public void Keeps_increasing_when_clock_goes_back()
    {
        var clock = new ManualTimeProvider(_start);
        var provider = new UniqueSequentialGuidTimestampProvider(clock);

        var first = provider.GetTimestamp();
        clock.Now = _start.AddMinutes(-5);
        var second = provider.GetTimestamp();

        second.Should().BeAfter(first);
    }

    [Fact]
    public void Is_thread_safe_and_unique()
    {
        var provider = new UniqueSequentialGuidTimestampProvider(new ManualTimeProvider(_start));

        var timestamps = new System.Collections.Concurrent.ConcurrentBag<DateTime>();
        Parallel.For(0, 1000, _ => timestamps.Add(provider.GetTimestamp()));

        timestamps.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Default_constructor_uses_system_clock()
    {
        var before = DateTime.UtcNow;

        var timestamp = new UniqueSequentialGuidTimestampProvider().GetTimestamp();

        timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }
}
