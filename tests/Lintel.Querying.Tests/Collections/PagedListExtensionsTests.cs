using System.Text.Json;
using Lintel.Collections;

namespace Lintel.Querying.Tests.Collections;

public class PagedListExtensionsTests
{
    private static readonly int[] _numbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    [Fact]
    public void ToPagedList_with_total_items_uses_items_as_page()
    {
        List<int> page = [4, 5, 6];

        var result = page.ToPagedList(totalItems: 10, pageNumber: 2, pageSize: 3);

        result.Should().Equal(page);
        result.TotalItems.Should().Be(10);
        result.TotalPages.Should().Be(4);
    }

    [Theory]
    [InlineData(1, 3, new[] { 1, 2, 3 })]
    [InlineData(2, 3, new[] { 4, 5, 6 })]
    [InlineData(4, 3, new[] { 10 })]
    [InlineData(5, 3, new int[0])]
    public void ToPagedList_slices_enumerable(int pageNumber, int pageSize, int[] expected)
    {
        var result = _numbers.ToPagedList(pageNumber, pageSize);

        result.Should().Equal(expected);
        result.TotalItems.Should().Be(10);
        result.PageNumber.Should().Be(pageNumber);
        result.PageSize.Should().Be(pageSize);
    }

    [Theory]
    [InlineData(1, 3, new[] { 1, 2, 3 })]
    [InlineData(4, 3, new[] { 10 })]
    public void ToPagedList_slices_queryable(int pageNumber, int pageSize, int[] expected)
    {
        var result = _numbers.AsQueryable().ToPagedList(pageNumber, pageSize);

        result.Should().Equal(expected);
        result.TotalItems.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ToPagedList_with_non_positive_page_size_returns_all_items(int pageSize)
    {
        var fromEnumerable = _numbers.ToPagedList(1, pageSize);
        var fromQueryable = _numbers.AsQueryable().ToPagedList(1, pageSize);

        fromEnumerable.Should().Equal(_numbers);
        fromEnumerable.PageSize.Should().Be(10);
        fromEnumerable.TotalPages.Should().Be(1);
        fromQueryable.Should().Equal(_numbers);
    }

    [Fact]
    public void ToPagedList_of_empty_source_returns_empty_page()
    {
        Array.Empty<int>().ToPagedList(3, 10).Should().BeEquivalentTo(PagedList.Empty<int>());
        Array.Empty<int>().AsQueryable().ToPagedList(3, 10).PageNumber.Should().Be(1);
    }

    [Fact]
    public void ToPagedList_enumerates_lazy_source_once()
    {
        var enumerations = 0;
        IEnumerable<int> Lazy()
        {
            enumerations++;
            foreach (var number in _numbers)
            {
                yield return number;
            }
        }

        var result = Lazy().ToPagedList(2, 4);

        result.Should().Equal(5, 6, 7, 8);
        enumerations.Should().Be(1);
    }

    [Fact]
    public void ToPagedList_throws_for_page_number_below_one()
    {
        var fromEnumerable = () => _numbers.ToPagedList(0, 3);
        var fromQueryable = () => _numbers.AsQueryable().ToPagedList(0, 3);

        fromEnumerable.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageNumber");
        fromQueryable.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageNumber");
    }

    [Fact]
    public void ToPagedList_copies_metadata_to_destination()
    {
        var source = new PagedList<int>([1, 2, 3], 10, 2, 3);

        var result = source.ToPagedList(["a", "b", "c"]);

        result.Should().Equal("a", "b", "c");
        result.TotalItems.Should().Be(10);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(3);
    }

    [Fact]
    public void ToPagedList_from_null_paged_enumerable_throws()
    {
        var act = () => ((IPagedEnumerable)null!).ToPagedList([1]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PagedResult_round_trips_through_json()
    {
        var page = new PagedList<int>([4, 5, 6], 10, 2, 3);

        var json = JsonSerializer.Serialize(page.ToPagedResult(), JsonSerializerOptions.Web);
        var restored = JsonSerializer.Deserialize<PagedResult<int>>(json, JsonSerializerOptions.Web)!.ToPagedList();

        json.Should().Be("""{"items":[4,5,6],"totalItems":10,"pageNumber":2,"pageSize":3}""");
        restored.Should().Equal(4, 5, 6);
        restored.TotalPages.Should().Be(4);
    }
}
