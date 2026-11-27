namespace Lintel.Querying.Tests.Sorting;

public class SortingTests
{
    public sealed class Address
    {
        public required string City { get; init; }
    }

    public sealed class FooBar
    {
        public int Foo { get; init; }
        public int Bar { get; init; }
        public int Baz { get; init; }
        public Address Address { get; init; } = new() { City = "" };
    }

    private static List<FooBar> Items() =>
    [
        new() { Foo = 2, Bar = 1, Baz = 1, Address = new() { City = "Omsk" } },
        new() { Foo = 1, Bar = 1, Baz = 2, Address = new() { City = "Kazan" } },
        new() { Foo = 1, Bar = 2, Baz = 3, Address = new() { City = "Moscow" } },
        new() { Foo = 1, Bar = 1, Baz = 1, Address = new() { City = "Perm" } },
    ];

    private static readonly Dictionary<string, SortOrder> _sort = new()
    {
        { "Foo", SortOrder.Asc },
        { "Bar", SortOrder.Desc },
        { "Baz", SortOrder.Asc },
    };

    private static readonly (int Foo, int Bar, int Baz)[] _expected = [(1, 2, 3), (1, 1, 1), (1, 1, 2), (2, 1, 1)];

    [Fact]
    public void Queryable_OrderBy_dictionary_sorts_by_all_keys()
    {
        var result = Items().AsQueryable().OrderBy(_sort).Select(x => new { x.Foo, x.Bar, x.Baz }).ToList();

        result.Select(x => (x.Foo, x.Bar, x.Baz)).Should().Equal(_expected);
    }

    [Fact]
    public void Enumerable_OrderBy_dictionary_sorts_by_all_keys()
    {
        var result = Items().OrderBy(_sort).Select(x => (x.Foo, x.Bar, x.Baz));

        result.Should().Equal(_expected);
    }

    [Fact]
    public void Empty_sort_keeps_original_order()
    {
        var items = Items();
        var empty = new Dictionary<string, SortOrder>();

        items.OrderBy(empty).Should().Equal(items);
        items.AsQueryable().OrderBy(empty).Should().Equal(items);
    }

    [Fact]
    public void Nested_and_case_insensitive_property_names_are_supported()
    {
        var expected = new[] { "Kazan", "Moscow", "Omsk", "Perm" };

        Items().OrderBy("address.city").Select(x => x.Address.City).Should().Equal(expected);
        Items().AsQueryable().OrderBy("Address.City").Select(x => x.Address.City).Should().Equal(expected);
        Items().OrderByDescending("Address.City").Select(x => x.Address.City).Should().Equal(expected.Reverse());
        Items().AsQueryable().OrderByDescending("address.CITY").Select(x => x.Address.City).Should().Equal(expected.Reverse());
    }

    [Fact]
    public void ThenBy_and_ThenByDescending_by_name()
    {
        var enumerable = Items().OrderBy("Foo").ThenByDescending("Bar").ThenBy("Baz").Select(x => (x.Foo, x.Bar, x.Baz));
        var queryable = Items().AsQueryable().OrderBy("Foo").ThenByDescending("Bar").ThenBy("Baz").ToList().Select(x => (x.Foo, x.Bar, x.Baz));

        enumerable.Should().Equal(_expected);
        queryable.Should().Equal(_expected);
    }

    [Fact]
    public void Queryable_sorting_builds_typed_key_selector()
    {
        var query = Items().AsQueryable().OrderBy("Foo", SortOrder.Desc);

        query.Expression.ToString().Should().Contain("OrderByDescending(x => x.Foo)");
    }

    [Fact]
    public void Unknown_property_throws()
    {
        var enumerable = () => Items().OrderBy("Missing");
        var queryable = () => Items().AsQueryable().OrderBy("Address.Missing");

        enumerable.Should().Throw<InvalidOperationException>()
            .WithMessage("Nested property with name Missing for type FooBar was not found.");
        queryable.Should().Throw<InvalidOperationException>()
            .WithMessage("Nested property with name Address.Missing for type FooBar was not found.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_property_name_throws(string propertyName)
    {
        var enumerable = () => Items().OrderBy(propertyName);
        var queryable = () => Items().AsQueryable().OrderBy(propertyName);

        enumerable.Should().Throw<ArgumentException>();
        queryable.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Empty_property_name_in_dictionary_throws()
    {
        var sort = new Dictionary<string, SortOrder> { { " ", SortOrder.Asc } };

        var act = () => Items().OrderBy(sort);

        act.Should().Throw<InvalidOperationException>().WithMessage("Invalid property name to sort");
    }

    [Fact]
    public void ThenBy_on_unsorted_source_throws()
    {
        var enumerable = () => Items().ThenBy("Foo", SortOrder.Asc);
        var queryable = () => Items().AsQueryable().Where(x => x.Foo > 0).ThenBy("Foo", SortOrder.Asc);

        enumerable.Should().Throw<InvalidOperationException>().WithMessage("*requires a sorted sequence*");
        queryable.Should().Throw<InvalidOperationException>().WithMessage("*requires a sorted query*");
    }

    [Fact]
    public void PageBy_skips_and_takes()
    {
        var query = Enumerable.Range(0, 10).AsQueryable();

        query.PageBy(2, 5).Should().Equal(2, 3, 4, 5, 6);
        query.PageBy(0, 1).Should().Equal(0);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void PageBy_throws_for_negative_skip_or_take_below_one(int skip, int take)
    {
        var act = () => Enumerable.Range(0, 10).AsQueryable().PageBy(skip, take);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PageBy_throws_when_query_is_null()
    {
        var act = () => ((IQueryable<int>)null!).PageBy(2, 5);

        act.Should().Throw<ArgumentNullException>();
    }
}
