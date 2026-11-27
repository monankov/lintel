using Lintel.Collections;

namespace Lintel.Querying.Tests.Collections;

public class PagedListTests
{
    [Fact]
    public void Ctor_computes_metadata()
    {
        List<int> items = [1, 2, 3];

        var result = new PagedList<int>(items, totalItems: 10, pageNumber: 2, pageSize: 3);

        result.TotalItems.Should().Be(10);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.TotalPages.Should().Be(4);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
        result.IsFirstPage.Should().BeFalse();
        result.IsLastPage.Should().BeFalse();
        result.FirstItemOnPage.Should().Be(4);
        result.LastItemOnPage.Should().Be(6);
        result.Should().Equal(items);
        result.Count.Should().Be(3);
        result[1].Should().Be(2);
    }

    [Fact]
    public void Last_page_is_partially_filled()
    {
        var result = new PagedList<int>([10], totalItems: 10, pageNumber: 4, pageSize: 3);

        result.IsLastPage.Should().BeTrue();
        result.HasNextPage.Should().BeFalse();
        result.FirstItemOnPage.Should().Be(10);
        result.LastItemOnPage.Should().Be(10);
    }

    [Fact]
    public void Ctor_copies_items()
    {
        List<int> items = [1, 2];
        var result = new PagedList<int>(items, 2, 1, 2);

        items.Add(3);

        result.Should().Equal(1, 2);
    }

    [Fact]
    public void Ctor_throws_when_items_is_null()
    {
        var act = () => new PagedList<int>(null!, totalItems: 1, pageNumber: 1, pageSize: 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(-1, 1, 3, "totalItems", "Total number of elements in superset must be greater or equal 0*")]
    [InlineData(1, 1, 3, "totalItems", "Total number of elements in superset must be greater or equal collection items count*")]
    [InlineData(10, 0, 3, "pageNumber", "Page number must be greater or equal 1*")]
    [InlineData(10, 1, -1, "pageSize", "Page size must be greater or equal 0*")]
    [InlineData(10, 1, 1, "pageSize", "Page size must be greater or equal collection items count*")]
    public void Ctor_validates_arguments(int totalItems, int pageNumber, int pageSize, string parameter, string message)
    {
        var act = () => new PagedList<int>([1, 2, 3], totalItems, pageNumber, pageSize);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage(message)
            .WithParameterName(parameter);
    }

    [Fact]
    public void Empty()
    {
        var result = PagedList.Empty<int>();

        result.TotalItems.Should().Be(0);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();
        result.IsFirstPage.Should().BeFalse();
        result.IsLastPage.Should().BeFalse();
        result.FirstItemOnPage.Should().Be(0);
        result.LastItemOnPage.Should().Be(0);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Map_projects_items_and_keeps_metadata()
    {
        var source = new PagedList<int>([1, 2, 3], totalItems: 10, pageNumber: 2, pageSize: 3);

        var result = source.Map(x => $"#{x}");

        result.Should().Equal("#1", "#2", "#3");
        (result.TotalItems, result.PageNumber, result.PageSize, result.TotalPages).Should().Be((10, 2, 3, 4));
    }

    [Fact]
    public void Is_read_only_list()
        => new PagedList<int>([1], 1, 1, 1).Should().BeAssignableTo<IReadOnlyList<int>>();
}
