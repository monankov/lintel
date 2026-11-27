using AngleSharp.Dom;
using System.Text.Json;
using Lintel.AspNetCore.IntegrationTests.TestUtils;

namespace Lintel.AspNetCore.IntegrationTests;

/// <summary>Tag helpers rendered by real Razor views with real routing.</summary>
public sealed class TagHelperIntegrationTests(TestApp app) : IClassFixture<TestApp>
{
    private readonly HttpClient _client = app.Browser();

    [Fact]
    public async Task Pagination_links_keep_query_and_use_conventional_route()
    {
        var page = await _client.GetDocumentAsync("/products?page=2&sort=Price&order=Desc");

        // The other query parameters come first, then the replaced page parameter.
        page.Hrefs(".pagination a[href]").Should().Contain(
        [
            "/products?sort=Price&order=Desc&page=1",
            "/products?sort=Price&order=Desc&page=3",
        ]);
        page.QuerySelector(".pagination li.active")!.TextContent.Should().Be("2");
        page.QuerySelectorAll("#products tbody tr").Should().HaveCount(10);
        // Prices fall from Product 01 (25) to Product 25 (1): by price descending the second page starts with Product 11.
        page.QuerySelector("#products tbody tr td")!.TextContent.Should().Be("Product 11");
    }

    [Fact]
    public async Task Pagination_respects_attribute_route_with_page_in_path()
    {
        var page = await _client.GetDocumentAsync("/catalog/page/2");

        page.Hrefs(".pagination a[href]").Should().BeEquivalentTo(["/catalog/page/1", "/catalog/page/3"]);
    }

    [Fact]
    public async Task Pagination_on_razor_page_links_to_the_page_path()
    {
        var page = await _client.GetDocumentAsync("/reports?page=2");

        page.QuerySelectorAll("#report-items li").Select(li => li.TextContent).First().Should().Be("Product 11");
        page.Hrefs(".pagination a[href]").Should().Contain(["/reports?page=1", "/reports?page=3"]);
    }

    [Fact]
    public async Task Page_info_and_page_size_render_with_current_query()
    {
        var page = await _client.GetDocumentAsync("/products?page=3&pagesize=5");

        page.QuerySelector(".page-info")!.TextContent.Should().Be("Page 3 of 5. Showing items 11 through 15 of 25.");
        page.QuerySelector(".page-size .dropdown-toggle")!.TextContent.Should().Be("5");
        page.Hrefs(".page-size a.dropdown-item").Should().Equal("/products?pagesize=5", "/products?pagesize=10", "/products?pagesize=0");
        page.QuerySelector(".page-size a.active")!.TextContent.Should().Be("5");
    }

    [Fact]
    public async Task Sort_links_toggle_order_and_keep_child_html()
    {
        var page = await _client.GetDocumentAsync("/products?sort=name&order=Asc&page=2");

        var name = page.QuerySelector("#sort-name a")!;
        name.GetAttribute("href").Should().Be("/products?page=2&sort=Name&order=Desc");
        name.ClassList.Should().Contain(["sort", "asc", "active"]);
        name.QuerySelector("i.icon-sort").Should().NotBeNull("child HTML is kept as markup");

        var price = page.QuerySelector("#sort-price a")!;
        price.GetAttribute("href").Should().Be("/products?page=2&sort=Price&order=Asc");
        price.ClassList.Should().NotContain("active");
        price.TextContent.Should().Be("Price & tax", "the ampersand is encoded once");
    }

    [Theory]
    [InlineData("/", "menu-home")]
    [InlineData("/products?page=2", "menu-products")]
    [InlineData("/bookings", "menu-bookings")]
    [InlineData("/reports", "menu-reports")]
    public async Task Menu_marks_the_current_page_for_controllers_and_razor_pages(string url, string activeId)
    {
        var page = await _client.GetDocumentAsync(url);

        page.QuerySelectorAll("#menu a.active").Select(a => a.Id).Should().Equal(activeId);
    }

    [Fact]
    public async Task Client_options_added_by_action_are_available_to_the_layout()
    {
        var page = await _client.GetDocumentAsync("/");

        var options = JsonDocument.Parse(page.QuerySelector("#client-options")!.TextContent).RootElement;
        options.GetProperty("home.index").GetProperty("pageSize").GetInt32().Should().Be(10);
        options.GetProperty("home.index").GetProperty("theme").GetString().Should().Be("dark");
    }

    [Fact]
    public async Task Asp_enabled_disables_link_generated_by_anchor_tag_helper_and_button()
    {
        var page = await _client.GetDocumentAsync("/");

        page.QuerySelector("#edit-enabled")!.GetAttribute("href").Should().Be("/products");
        var link = page.QuerySelector("#edit-disabled")!;
        link.HasAttribute("href").Should().BeFalse();
        link.ClassList.Should().BeEquivalentTo("btn", "btn-link", "disabled");
        link.GetAttribute("aria-disabled").Should().Be("true");
        page.QuerySelector("#delete-disabled")!.HasAttribute("disabled").Should().BeTrue();
        page.QuerySelectorAll("[asp-enabled]").Should().BeEmpty();
    }

    private const string Marker = """<span class="text-danger ms-1" aria-hidden="true">*</span>""";

    private static string Label(IDocument page, string field) => page.QuerySelector($"label[for='{field}']")!.InnerHtml;

    [Fact]
    public async Task Required_label_marks_required_and_forced_fields_only()
    {
        var page = await _client.GetDocumentAsync("/bookings/create");

        Label(page, "Name").Should().Be("Your name" + Marker, "child content is kept");
        Label(page, "From").Should().Be("Arrival date", "a non-nullable property is not marked");
        Label(page, "To").Should().Be("Departure date" + Marker, "asp-required=true forces the marker");
        Label(page, "HasPets").Should().Be("Has pets");
        Label(page, "PetName").Should().Be("Pet name", "RequiredIfTrue(HasPets) is not met");
    }

    [Fact]
    public async Task Required_if_marker_follows_posted_model()
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = "Petrov", ["HasPets"] = "true" });
        var page = await HttpExtensions.ParseAsync(response);

        Label(page, "PetName").Should().Be("Pet name" + Marker);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Required_if_marker_uses_model_state_without_model(string hasPets, bool marked)
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");
        form.QuerySelector("#booking")!.SetAttribute("action", "/bookings/check");

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["HasPets"] = hasPets });
        var page = await HttpExtensions.ParseAsync(response);

        Label(page, "PetName").Should().Be(marked ? "Pet name" + Marker : "Pet name");
        Label(page, "Name").Should().EndWith(Marker);
    }

    [Fact]
    public async Task Form_controls_get_bootstrap_classes_and_invalid_field_is_highlighted()
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        form.QuerySelector("#Name")!.ClassName.Should().Be("form-control");
        form.QuerySelector("#From")!.ClassName.Should().Be("form-control");
        form.QuerySelector("#HasPets")!.ClassName.Should().Be("form-check-input", "InputTagHelper renders a bool as a checkbox first");
        form.QuerySelectorAll("input[type=hidden][class]").Should().BeEmpty();

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = "" });
        var page = await HttpExtensions.ParseAsync(response);

        page.QuerySelector("#Name")!.ClassList.Should().Contain(["form-control", "is-invalid"]);
        page.QuerySelector("#From")!.ClassList.Should().NotContain("is-invalid");
    }

    [Fact]
    public async Task Validation_summary_shows_errors_only_after_failed_post()
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        form.QuerySelector("#summary").Should().BeNull();
        form.QuerySelector("#summary-all").Should().BeNull();

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = "" });
        var page = await HttpExtensions.ParseAsync(response);

        page.QuerySelector("#summary").Should().BeNull("there are only field errors");
        var all = page.QuerySelector("#summary-all")!;
        all.ClassName.Should().Be("alert alert-danger");
        all.QuerySelectorAll("li").Select(li => li.TextContent).Should().Equal("The Guest name field is required.");
    }

    [Fact]
    public async Task Breadcrumbs_show_path_to_current_action_with_links()
    {
        var page = await _client.GetDocumentAsync("/bookings/edit/7");

        var items = page.QuerySelectorAll("#breadcrumbs ol.breadcrumb li.breadcrumb-item");
        items.Select(li => li.TextContent).Should().Equal("Home", "Bookings", "Edit <booking>");
        page.Hrefs("#breadcrumbs a").Should().Equal("/", "/bookings");
        items[^1].GetAttribute("aria-current").Should().Be("page");
        items[^1].QuerySelector("a").Should().BeNull();
        page.QuerySelector("#breadcrumbs")!.GetAttribute("aria-label").Should().Be("breadcrumb");
    }

    [Fact]
    public async Task Breadcrumbs_work_for_razor_pages_and_are_hidden_off_the_tree()
    {
        var reports = await _client.GetDocumentAsync("/reports");
        var products = await _client.GetDocumentAsync("/products");

        reports.QuerySelectorAll("#breadcrumbs li").Select(li => li.TextContent).Should().Equal("Home", "Reports");
        products.QuerySelector("#breadcrumbs").Should().BeNull("Products is not in the tree");
    }

    [Theory]
    [InlineData("/", "side-home")]
    [InlineData("/bookings/edit/7", "side-bookings")]
    [InlineData("/products?page=2", "side-catalog")]
    [InlineData("/catalog/page/2", "side-catalog")]
    [InlineData("/reports", "side-reports")]
    public async Task Menu_item_is_activated_by_url_prefix_or_menu_attribute(string url, string activeId)
    {
        var page = await _client.GetDocumentAsync(url);

        page.QuerySelectorAll("#side-menu li.active").Select(li => li.Id).Should().Equal(activeId);
    }

    [Fact]
    public async Task Menu_hides_items_by_policy_and_empty_groups()
    {
        var page = await _client.GetDocumentAsync("/");

        page.QuerySelectorAll("#side-menu > li").Select(li => li.Id).Should().Equal("main");
        page.QuerySelector("#side-reports").Should().NotBeNull("the Everyone policy is met");
        page.QuerySelector("#side-users").Should().BeNull("an anonymous user is not an administrator");
    }

    [Fact]
    public async Task Checkbox_and_radio_render_bootstrap_form_checks_with_generated_inputs()
    {
        var page = await _client.GetDocumentAsync("/home/checks");

        var agree = page.QuerySelector("#agree")!;
        agree.ClassName.Should().Be("form-check");
        agree.QuerySelector("input[type=checkbox]")!.GetAttribute("class").Should().Be("form-check-input");
        agree.QuerySelector("input")!.GetAttribute("data-val-required").Should().NotBeNullOrEmpty();
        agree.QuerySelector("label")!.GetAttribute("for").Should().Be("Agree");
        agree.QuerySelector("label")!.InnerHtml.Should().Be("""I agree<span class="text-danger ms-1" aria-hidden="true">*</span>""");

        var news = page.QuerySelector("#news")!;
        news.ClassName.Should().Be("form-check form-check-inline form-switch");
        news.QuerySelector("input")!.HasAttribute("checked").Should().BeTrue();
        news.QuerySelector("input")!.GetAttribute("role").Should().Be("switch");

        page.QuerySelector("#person input")!.Id.Should().Be("Kind_person");
        page.QuerySelector("#person input")!.HasAttribute("checked").Should().BeTrue();
        page.QuerySelector("#company label")!.GetAttribute("for").Should().Be("Kind_company");
        page.QuerySelector("#company input")!.HasAttribute("checked").Should().BeFalse();

        page.QuerySelector("#all input")!.Id.Should().Be("check-all", "child content is kept");
        page.QuerySelector("#all label")!.GetAttribute("for").Should().Be("check-all");
        page.QuerySelectorAll("#checks input[type=hidden][name=Agree]").Should().ContainSingle("the hidden false value goes to the end of the form");
    }

    [Theory]
    [InlineData(true, "company", "True|False|company")]
    [InlineData(false, "person", "False|False|person")]
    public async Task Checkbox_and_radio_values_are_bound(bool agree, string kind, string expected)
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/home/checks");

        using var response = await browser.SubmitAsync(form, "#checks", tamper: fields =>
        {
            fields.Remove("Newsletter");
            fields["Kind"] = kind;
            if (agree)
            {
                fields["Agree"] = "true";
            }
        });

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(expected);
    }

    [Fact]
    public async Task Tab_badge_is_compact_keeps_title_and_hides_zero()
    {
        var page = await _client.GetDocumentAsync("/home/checks");

        var big = page.QuerySelector("#badge-big")!;
        big.TextContent.Should().Be("1.2K");
        big.GetAttribute("title").Should().Be("Orders");
        big.ClassName.Should().Be("badge text-bg-secondary ms-1");
        page.QuerySelector("#badge-zero")!.ClassList.Should().Contain("d-none");
    }
}
