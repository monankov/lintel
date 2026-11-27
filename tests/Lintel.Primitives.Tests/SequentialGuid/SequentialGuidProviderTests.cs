using System.Data.SqlTypes;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Primitives.Tests.SequentialGuid;

public class SequentialGuidProviderTests
{
    private static readonly Guid _fixedGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly DateTime _fixedTimestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc); // 0x016F5E66E800 ms

    [Fact]
    public void Ctor_throws_when_arguments_are_null()
    {
        var withoutInitializer = () => new SequentialGuidProvider(null!, () => DateTime.UtcNow);
        var withoutTimestamp = () => new SequentialGuidProvider(Guid.NewGuid, null!);

        withoutInitializer.Should().Throw<ArgumentNullException>();
        withoutTimestamp.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(SequentialGuidType.String, "016f5e66-e800-6677-8899-aabbccddeeff")]
    [InlineData(SequentialGuidType.Binary, "665e6f01-00e8-6677-8899-aabbccddeeff")]
    [InlineData(SequentialGuidType.Ending, "00112233-4455-6677-8899-016f5e66e800")]
    public void Places_timestamp_according_to_type(SequentialGuidType type, string expected)
    {
        var provider = new SequentialGuidProvider(() => _fixedGuid, () => _fixedTimestamp);

        provider.GetSequentialGuid(type).ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(SequentialGuidType.String)]
    [InlineData(SequentialGuidType.Binary)]
    [InlineData(SequentialGuidType.Ending)]
    public void Produces_same_bytes_as_Structr(SequentialGuidType type)
    {
        var random = new Random(12345);
        for (var i = 0; i < 1000; i++)
        {
            var guid = Guid.NewGuid();
            var timestamp = DateTime.UnixEpoch.AddMilliseconds(random.NextInt64(0, 8_000_000_000_000));
            var provider = new SequentialGuidProvider(() => guid, () => timestamp);

            provider.GetSequentialGuid(type).Should().Be(StructrReference.GetSequentialGuid(guid, timestamp, type));
        }
    }

    [Theory]
    [InlineData(SequentialGuidType.String)]
    [InlineData(SequentialGuidType.Binary)]
    public void String_and_binary_guids_grow_with_timestamp(SequentialGuidType type)
    {
        var guids = new Queue<Guid>([MaxGuid(), Guid.Empty]);
        var now = DateTime.UtcNow;
        var timestamps = new Queue<DateTime>([now, now.AddMilliseconds(1)]);
        var provider = new SequentialGuidProvider(guids.Dequeue, timestamps.Dequeue);

        var first = provider.GetSequentialGuid(type);
        var second = provider.GetSequentialGuid(type);

        if (type == SequentialGuidType.String)
        {
            string.CompareOrdinal(first.ToString(), second.ToString()).Should().BeNegative();
        }
        else
        {
            first.ToByteArray().AsSpan().SequenceCompareTo(second.ToByteArray()).Should().BeNegative();
        }
    }

    [Fact]
    public void Ending_guids_grow_in_sql_server_order()
    {
        var guids = new Queue<Guid>([MaxGuid(), Guid.Empty]);
        var now = DateTime.UtcNow;
        var timestamps = new Queue<DateTime>([now, now.AddMilliseconds(1)]);
        var provider = new SequentialGuidProvider(guids.Dequeue, timestamps.Dequeue);

        SqlGuid first = provider.GetSequentialGuid(SequentialGuidType.Ending);
        SqlGuid second = provider.GetSequentialGuid(SequentialGuidType.Ending);

        first.CompareTo(second).Should().Be(-1);
    }

    [Fact]
    public void Undefined_type_throws()
    {
        var provider = new SequentialGuidProvider(Guid.NewGuid, () => DateTime.UtcNow);

        var act = () => provider.GetSequentialGuid((SequentialGuidType)42);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void AddSequentialGuidProvider_registers_singleton_once()
    {
        var services = new ServiceCollection()
            .AddSequentialGuidProvider()
            .AddSequentialGuidProvider();
        var provider = services.BuildServiceProvider();

        services.Should().ContainSingle(d => d.ServiceType == typeof(ISequentialGuidProvider));
        provider.GetService<ISequentialGuidProvider>().Should().BeOfType<SequentialGuidProvider>()
            .And.BeSameAs(provider.GetService<ISequentialGuidProvider>());
    }

    [Fact]
    public void Registered_provider_generates_increasing_guids()
    {
        var provider = new ServiceCollection().AddSequentialGuidProvider().BuildServiceProvider()
            .GetRequiredService<ISequentialGuidProvider>();

        var guids = Enumerable.Range(0, 100).Select(_ => (SqlGuid)provider.GetSequentialGuid(SequentialGuidType.Ending)).ToList();

        guids.Should().BeInAscendingOrder();
    }

    private static Guid MaxGuid() => Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    /// <summary>The algorithm of Structr.Abstractions SequentialGuidProvider, verbatim.</summary>
    private static class StructrReference
    {
        private static readonly DateTime _minTimestamp = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static Guid GetSequentialGuid(Guid guid, DateTime timestamp, SequentialGuidType type)
        {
            var guidBytes = guid.ToByteArray();
            var timestampBytes = BitConverter.GetBytes((timestamp.Ticks - _minTimestamp.Ticks) / 10000L);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timestampBytes);
            }

            switch (type)
            {
                case SequentialGuidType.String:
                case SequentialGuidType.Binary:
                    Buffer.BlockCopy(timestampBytes, 2, guidBytes, 0, 6);
                    if (type is SequentialGuidType.String && BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(guidBytes, 0, 4);
                        Array.Reverse(guidBytes, 4, 2);
                    }
                    break;
                case SequentialGuidType.Ending:
                    Buffer.BlockCopy(timestampBytes, 2, guidBytes, 10, 6);
                    break;
            }

            return new Guid(guidBytes);
        }
    }
}
