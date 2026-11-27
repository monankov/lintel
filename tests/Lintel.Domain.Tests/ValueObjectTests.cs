using Lintel.Domain.Tests.TestUtils;

namespace Lintel.Domain.Tests;

public class ValueObjectTests
{
    public class Node : ValueObject<Node>
    {
        public string? Name { get; set; }
        public Node? Next { get; set; }
    }

    public class WithIndexer : ValueObject<WithIndexer>
    {
        private readonly int[] _values = [1, 2, 3];

        public int Size { get; set; }

        public int this[int index] => _values[index];
    }

    public static TheoryData<Address, Address?, bool> EqualityData()
    {
        var address = new Address { City = "Moscow", Street = "Prospekt Mira", House = "111" };
        return new()
        {
            { address, address, true },
            { address, new Address { City = "Moscow", Street = "Prospekt Mira", House = "111" }, true },
            { address, new Address { City = "Moscow", Street = "Prospekt Mira", House = "38" }, false },
            { address, new Address { City = "Moscow", Street = "Prospekt Mira" }, false },
            { address, null, false },
            { new Address(), new Address(), true },
        };
    }

    [Theory]
    [MemberData(nameof(EqualityData))]
    public void Equals_compares_all_public_properties(Address left, Address? right, bool expected)
    {
        left.Equals(right).Should().Be(expected);
        left.Equals((object?)right).Should().Be(expected);
        (left == right).Should().Be(expected);
        (left != right).Should().Be(!expected);
    }

    [Fact]
    public void Operators_handle_nulls()
    {
        Address? none = null;

        (none == null).Should().BeTrue();
        (none == new Address()).Should().BeFalse();
        (new Address() != none).Should().BeTrue();
    }

    [Fact]
    public void Equal_value_objects_have_equal_hash_codes()
    {
        var left = new Address { City = "Moscow", Street = null, House = "111" };
        var right = new Address { City = "Moscow", Street = null, House = "111" };

        left.GetHashCode().Should().Be(right.GetHashCode());
        left.GetHashCode().Should().NotBe(new Address { City = "Moscow", House = "38" }.GetHashCode());
    }

    [Fact]
    public void Hash_code_depends_on_position_of_values()
    {
        var left = new Address { City = "a", Street = null, House = null };
        var right = new Address { City = null, Street = null, House = "a" };

        left.GetHashCode().Should().NotBe(right.GetHashCode());
    }

    [Fact]
    public void Self_typed_properties_are_compared_by_reference_without_recursion()
    {
        var shared = new Node { Name = "tail" };
        var cyclic = new Node { Name = "loop" };
        cyclic.Next = cyclic;

        new Node { Name = "head", Next = shared }.Should().Be(new Node { Name = "head", Next = shared });
        new Node { Name = "head", Next = shared }.Should().NotBe(new Node { Name = "head", Next = new Node { Name = "tail" } });

        var act = () => cyclic.GetHashCode() + (cyclic.Equals(cyclic) ? 1 : 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Indexers_are_ignored()
    {
        var act = () => new WithIndexer { Size = 1 }.Equals(new WithIndexer { Size = 1 });

        act.Should().NotThrow().Which.Should().BeTrue();
    }

    [Fact]
    public void Value_objects_of_different_types_are_not_equal()
        => new Address().Equals(new Node()).Should().BeFalse();
}
