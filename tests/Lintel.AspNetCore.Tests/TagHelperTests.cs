using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Lintel.Collections;
using Lintel.Querying;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class TagHelperTests
{
    private static readonly FakeUrlHelperFactory _urls = new();

    private static PagedList<int> Page(int pageNumber, int totalItems = 100, int pageSize = 10)
        => new(Enumerable.Range(1, Math.Min(pageSize, Math.Max(0, totalItems - ((pageNumber - 1) * pageSize)))), totalItems, pageNumber, pageSize);

    private static string Pagination(PaginationOptions options, string query = "", RouteValueDictionary? route = null)
    {
        var helper = new PaginationTagHelper(_urls)
        {
            Options = options,
            ViewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext("/orders", query), route ?? TestContextFactory.Mvc()),
        };
        var output = Output("pagination");
        helper.Process(Context("pagination"), output);
        return Render(output);
    }

    [Fact]
    public void Pagination_renders_window_of_pages_with_first_last_and_ellipses()
    {
        var html = Pagination(new PaginationOptions { PagedEnumerable = Page(5) });

        html.Should().StartWith("""<div class="pagination-container"><ul class="pagination">""");
        html.Should().Contain("""<li class="pagination-first first page-item"><a class="page-link" href="/Orders/Index?page=1">&#xAB;&#xAB;</a></li>""");
        html.Should().Contain("""<li class="active page-item"><a aria-current="page" class="page-link">5</a></li>""");
        html.Should().Contain("""href="/Orders/Index?page=4">4</a>""");
        html.Should().Contain("""href="/Orders/Index?page=6">6</a>""");
        html.Should().NotContain("page=7\">7<");
        html.Should().Contain("pagination-ellipses");
        html.Should().Contain("""<li class="pagination-last last page-item"><a class="page-link" href="/Orders/Index?page=10">&#xBB;&#xBB;</a></li>""");
    }

    [Fact]
    public void Pagination_keeps_query_and_replaces_page_parameter()
        => Pagination(new PaginationOptions { PagedEnumerable = Page(2) }, "?Page=2&sort=Name&order=Desc")
            .Should().Contain("""href="/Orders/Index?order=Desc&amp;page=3&amp;sort=Name">3</a>""");

    [Fact]
    public void Pagination_window_at_the_end_shows_last_pages()
    {
        var html = Pagination(new PaginationOptions { PagedEnumerable = Page(10) });

        html.Should().Contain(">8</a>").And.Contain(">9</a>").And.Contain(">10</a>");
        html.Should().NotContain("pagination-last");
    }

    [Fact]
    public void Pagination_works_for_razor_pages_and_plain_endpoints()
    {
        var razorPage = Pagination(new PaginationOptions { PagedEnumerable = Page(1) }, route: new RouteValueDictionary { ["page"] = "/Orders/Index" });
        var endpoint = Pagination(new PaginationOptions { PagedEnumerable = Page(1) }, "?q=x", route: []);

        // Razor Pages and other endpoints link to the current path ("/orders" in these tests).
        razorPage.Should().Contain("""href="/orders?page=2">2</a>""");
        endpoint.Should().Contain("""href="/orders?q=x&amp;page=2">2</a>""");
    }

    [Fact]
    public void Pagination_display_modes()
    {
        // Display defaults to Always, as in Structr: a single page is rendered too.
        Pagination(new PaginationOptions { PagedEnumerable = Page(1, totalItems: 5) }).Should().Contain(">1</a>");
        Pagination(new PaginationOptions { PagedEnumerable = Page(1, totalItems: 5), Display = PaginationDisplayMode.IfNeeded }).Should().BeEmpty();
        Pagination(new PaginationOptions { PagedEnumerable = Page(1), Display = PaginationDisplayMode.Never }).Should().BeEmpty();
        Pagination(new PaginationOptions()).Should().BeEmpty();
    }

    [Fact]
    public void Pagination_supports_previous_next_and_custom_urls()
    {
        var html = Pagination(new PaginationOptions
        {
            PagedEnumerable = Page(1),
            DisplayLinkToPreviousPage = PaginationDisplayMode.Always,
            DisplayLinkToNextPage = PaginationDisplayMode.Always,
            PageUrlGenerator = page => $"/p/{page}",
        });

        html.Should().Contain("""<li class="pagination-previous disabled first page-item"><a class="page-link" rel="prev">&#xAB;</a></li>""");
        html.Should().Contain("""<a class="page-link" href="/p/2" rel="next">&#xBB;</a>""");
    }

    [Fact]
    public void PageInfo_renders_summary()
    {
        var helper = new PageInfoTagHelper { Options = new PageInfoOptions { PagedEnumerable = Page(2, totalItems: 15) } };
        var output = Output("page-info");

        helper.Process(Context("page-info"), output);

        Render(output).Should().Be("""<div class="page-info">Page 2 of 2. Showing items 11 through 15 of 15.</div>""");
    }

    private static string PageSize(PageSizeOptions options, string query = "?pagesize=10&page=3&sort=Name")
    {
        var helper = new PageSizeTagHelper(_urls)
        {
            Options = options,
            ViewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext("/orders", query), TestContextFactory.Mvc()),
        };
        var output = Output("page-size");
        helper.Process(Context("page-size"), output);
        return Render(output);
    }

    [Fact]
    public void PageSize_renders_bootstrap_5_dropdown_with_active_size_and_all_items()
    {
        var html = PageSize(new PageSizeOptions { ItemsPerPage = [10, 50, 10, 0], DefaultPageSize = 10 });

        html.Should().Contain("""<button class="dropdown-toggle btn btn-secondary" data-bs-toggle="dropdown" type="button">10</button>""");
        html.Should().Contain("""<div class="dropdown-menu dropdown-menu-end">""");
        html.Should().Contain("""<a aria-current="true" class="dropdown-item active" href="/Orders/Index?pagesize=10&amp;sort=Name">10</a>""");
        html.Should().Contain("""<a class="dropdown-item" href="/Orders/Index?pagesize=50&amp;sort=Name">50</a>""");
        html.Should().Contain("""<div class="dropdown-divider"></div>""");
        html.Should().Contain("""href="/Orders/Index?pagesize=0&amp;sort=Name">All</a>""");
        html.Split(">10</a>").Should().HaveCount(2, "duplicate sizes are shown once");
    }

    [Fact]
    public void PageSize_options_keep_page_number_and_show_only_all_items()
    {
        var html = PageSize(new PageSizeOptions
        {
            ItemsPerPage = [0],
            DefaultPageSize = 0,
            PageNumberRouteParamName = null,
            DropdownMenuAlign = PageSizeDropdownMenuAlign.Left,
            DropdownToggleAttribute = "data-toggle",
            AllItemsFormat = "Everything",
        });

        html.Should().Contain("""<div class="dropdown-menu dropdown-menu-start">""");
        html.Should().Contain("""data-toggle="dropdown" type="button">Everything</button>""");
        html.Should().Contain("""<a aria-current="true" class="dropdown-item active" href="/Orders/Index?page=3&amp;pagesize=0&amp;sort=Name">""");
        html.Should().NotContain("dropdown-divider", "there is nothing to separate");
    }

    private static async Task<string> Sort(string sort, string query, string childContent = "Name", string? defaultSort = null, SortOrder? defaultOrder = null)
    {
        var helper = new SortTagHelper(_urls)
        {
            Sort = sort,
            DefaultSort = defaultSort,
            DefaultOrder = defaultOrder,
            ViewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext("/orders", query), TestContextFactory.Mvc()),
        };
        var output = Output("th", childContent);
        await helper.ProcessAsync(Context("th"), output);
        return Render(output);
    }

    [Fact]
    public async Task Sort_links_toggle_order_for_active_column()
    {
        (await Sort("Name", "?sort=name&order=Asc&page=2"))
            .Should().Be("""<th><a class="sort asc active" href="/Orders/Index?order=Desc&amp;page=2&amp;sort=Name">Name</a></th>""");
        (await Sort("Name", "?sort=Name&order=desc"))
            .Should().Be("""<th><a class="sort desc active" href="/Orders/Index?order=Asc&amp;sort=Name">Name</a></th>""");
        (await Sort("Date", "?sort=Name&order=Desc"))
            .Should().Be("""<th><a class="sort" href="/Orders/Index?order=Asc&amp;sort=Date">Name</a></th>""");
    }

    [Fact]
    public async Task Sort_uses_default_sort_when_given()
        => (await Sort("Date", "", defaultSort: "Date", defaultOrder: SortOrder.Desc))
            .Should().Contain("""class="sort desc active" href="/Orders/Index?order=Asc&amp;sort=Date""");

    [Fact]
    public async Task Sort_keeps_child_html_without_double_encoding()
        => (await Sort("Name", "", childContent: "Name &amp; Co <i class=\"icon\"></i>"))
            .Should().Contain(""">Name &amp; Co <i class="icon"></i></a>""");

    [Fact]
    public async Task Sort_requires_property()
    {
        var act = () => Sort(" ", "");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true, """<li class="active"></li>""")]
    [InlineData(false, "<li></li>")]
    public void AppendClass(bool append, string expected)
    {
        var output = Output("li");

        new AppendClassTagHelper { Append = append, Class = "active" }.Process(Context("li"), output);

        Render(output).Should().Be(expected);
    }

    private static string AnchorMatch(Action<AnchorMatchTagHelper> configure, RouteValueDictionary route, string query = "")
    {
        var helper = new AnchorMatchTagHelper(Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.IHtmlGenerator>())
        {
            MatchClass = "active",
            ViewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext("/", query), route),
        };
        configure(helper);
        var output = Output("a");
        helper.Process(Context("a"), output);
        return Render(output);
    }

    [Fact]
    public void AnchorMatch_adds_class_for_current_page()
    {
        var route = TestContextFactory.Mvc("Orders", "Index", "Admin");

        AnchorMatch(h => { h.Controller = "orders"; h.Action = "index"; }, route).Should().Be("""<a class="active"></a>""");
        AnchorMatch(h => h.Controller = "Orders", route).Should().Be("""<a class="active"></a>""");
        AnchorMatch(h => { h.Controller = "Orders"; h.Action = "Edit"; }, route).Should().Be("<a></a>");
        AnchorMatch(h => h.Area = "Shop", route).Should().Be("<a></a>");
    }

    [Fact]
    public void AnchorMatch_compares_route_values_with_query_and_razor_pages()
    {
        AnchorMatch(h => { h.Controller = "Orders"; h.RouteValues["status"] = "new"; }, TestContextFactory.Mvc(), "?status=NEW")
            .Should().Be("""<a class="active"></a>""");
        AnchorMatch(h => { h.Controller = "Orders"; h.RouteValues["status"] = "new"; }, TestContextFactory.Mvc(), "?status=done")
            .Should().Be("<a></a>");
        AnchorMatch(h => h.Page = "/Orders/Index", new RouteValueDictionary { ["page"] = "/Orders/Index" })
            .Should().Be("""<a class="active"></a>""");
        AnchorMatch(h => h.Controller = "Orders", new RouteValueDictionary { ["page"] = "/Orders/Index" })
            .Should().Be("<a></a>");
    }
}
