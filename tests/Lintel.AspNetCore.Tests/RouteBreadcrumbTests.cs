using Lintel.AspNetCore.Navigation;
using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Lintel.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class RouteBreadcrumbTests
{
    private static DefaultHttpContext Request(RouteValueDictionary route, string query = "")
    {
        var context = TestContextFactory.HttpContext("/", query);
        context.Request.RouteValues = route;
        return context;
    }

    private static RouteValueDictionary Mvc(string controller, string action, string? area = null, object? id = null)
    {
        var values = TestContextFactory.Mvc(controller, action, area);
        if (id is not null)
        {
            values["id"] = id;
        }
        return values;
    }

    [Theory]
    [InlineData("Orders", "Index", null, true)]
    [InlineData("orders", "INDEX", null, true)]
    [InlineData("Orders", "Edit", null, false)]
    [InlineData("Invoices", "Index", null, false)]
    [InlineData("Orders", "Index", "Admin", false)]
    public void Plain_names_are_compared_case_insensitively_and_area_must_match(string controller, string action, string? area, bool expected)
        => RouteBreadcrumbMatcher.IsMatch(new RouteBreadcrumb { Controller = "Orders", Action = "Index" }, Request(Mvc(controller, action, area)))
            .Should().Be(expected);

    [Theory]
    [InlineData("SalesOrders", true)]
    [InlineData("salesorders", true)]
    [InlineData("Orders", false)]
    [InlineData("SalesOrdersArchive", false)]
    public void Patterns_must_match_the_whole_value(string controller, bool expected)
        => RouteBreadcrumbMatcher.IsMatch(new RouteBreadcrumb { Controller = "(.+)Orders", Action = "Index", Area = "Admin" },
            Request(Mvc(controller, "Index", "Admin"))).Should().Be(expected);

    [Fact]
    public void Empty_controller_matches_any_and_action_or_page_is_required()
    {
        RouteBreadcrumbMatcher.IsMatch(new RouteBreadcrumb { Action = "Index" }, Request(Mvc("Orders", "Index"))).Should().BeTrue();
        RouteBreadcrumbMatcher.IsMatch(new RouteBreadcrumb { Controller = "Orders" }, Request(Mvc("Orders", "Index"))).Should().BeFalse();
        RouteBreadcrumbMatcher.IsMatch(new RouteBreadcrumb { Action = "Index" }, Request([])).Should().BeFalse();
    }

    [Fact]
    public void Route_values_are_required_from_route_or_query()
    {
        var breadcrumb = new RouteBreadcrumb { Controller = "Orders", Action = "Index", RouteValues = new Dictionary<string, string> { ["state"] = "Draft" } };

        RouteBreadcrumbMatcher.IsMatch(breadcrumb, Request(Mvc("Orders", "Index"), "?state=draft")).Should().BeTrue();
        var route = Mvc("Orders", "Index");
        route["state"] = "Draft";
        RouteBreadcrumbMatcher.IsMatch(breadcrumb, Request(route)).Should().BeTrue();
        RouteBreadcrumbMatcher.IsMatch(breadcrumb, Request(Mvc("Orders", "Index"), "?state=Sent")).Should().BeFalse();
        RouteBreadcrumbMatcher.IsMatch(breadcrumb, Request(Mvc("Orders", "Index"))).Should().BeFalse();
    }

    [Fact]
    public void Razor_page_is_matched_by_page_name()
    {
        var breadcrumb = new RouteBreadcrumb { Page = "/Reports/Index" };

        RouteBreadcrumbMatcher.IsMatch(breadcrumb, Request(new RouteValueDictionary { ["page"] = "/reports/index" })).Should().BeTrue();
        RouteBreadcrumbMatcher.IsMatch(breadcrumb, Request(new RouteValueDictionary { ["page"] = "/Reports/Edit" })).Should().BeFalse();
    }

    [Fact]
    public void Link_values_keep_preserved_values_clear_others_and_add_defaults()
    {
        var breadcrumb = new RouteBreadcrumb
        {
            Controller = "Orders",
            Action = "Details",
            PreservedRouteValues = ["ID"],
            RouteValues = new Dictionary<string, string> { ["tab"] = "items" },
            DefaultRouteValues = new Dictionary<string, string> { ["page"] = "1", ["tab"] = "ignored", ["id"] = "0" },
        };
        var route = Mvc("OrderItems", "Edit", "Admin", id: 7);
        route["itemId"] = 3;

        var values = RouteBreadcrumbMatcher.GetLinkValues(breadcrumb, Request(route));

        values["controller"].Should().Be("Orders");
        values["action"].Should().Be("Details");
        values["area"].Should().Be("", "the breadcrumb has no area");
        values["id"].Should().Be(7);
        values["itemId"].Should().BeNull();
        values["tab"].Should().Be("items");
        values["page"].Should().Be("1", "for a controller link page is an ordinary value");
    }

    [Fact]
    public void Patterns_link_to_current_value_and_maps_translate_it()
    {
        var mapped = new RouteBreadcrumb
        {
            Area = "(.+)",
            Controller = "(.+)Orders",
            ControllerMap = new RouteValueMap { From = "(.+)(Invoices|Payments)", To = "{0}Orders" },
            Action = "Index",
        };

        var fromInvoices = RouteBreadcrumbMatcher.GetLinkValues(mapped, Request(Mvc("SalesInvoices", "Edit", "Admin")));
        var fromOrders = RouteBreadcrumbMatcher.GetLinkValues(mapped, Request(Mvc("SalesOrders", "Edit", "Admin")));

        fromInvoices["controller"].Should().Be("SalesOrders");
        fromInvoices["area"].Should().Be("Admin", "a pattern links to the current value");
        fromOrders["controller"].Should().Be("SalesOrders", "the map does not match, so the current value is used");
        fromOrders["action"].Should().Be("Index");
    }

    [Fact]
    public void Page_link_has_page_and_no_controller()
    {
        var values = RouteBreadcrumbMatcher.GetLinkValues(new RouteBreadcrumb { Page = "/Reports/Index" }, Request(Mvc("Orders", "Index")));

        values["page"].Should().Be("/Reports/Index");
        values.Should().NotContainKey("controller").And.NotContainKey("action");
    }

    [Fact]
    public void ActivateByRoute_uses_the_current_request()
    {
        var accessor = new HttpContextAccessor();
        var services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).BuildServiceProvider();
        var options = new NavigationOptions<RouteBreadcrumb>().ActivateByRoute(services);
        var breadcrumb = new RouteBreadcrumb { Controller = "Orders", Action = "Index" };

        options.ItemActivator!(breadcrumb).Should().BeFalse("there is no request");
        accessor.HttpContext = Request(Mvc("Orders", "Index"));
        options.ItemActivator!(breadcrumb).Should().BeTrue();
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var context = new DefaultHttpContext();
        var breadcrumb = new RouteBreadcrumb();
        var actions = new Action[]
        {
            () => RouteBreadcrumbMatcher.IsMatch(null!, context),
            () => RouteBreadcrumbMatcher.IsMatch(breadcrumb, null!),
            () => RouteBreadcrumbMatcher.GetLinkValues(null!, context),
            () => RouteBreadcrumbMatcher.GetLinkValues(breadcrumb, null!),
            () => RouteBreadcrumbMatcher.HasLink(null!),
            () => ((NavigationOptions<RouteBreadcrumb>)null!).ActivateByRoute(new ServiceCollection().BuildServiceProvider()),
            () => new NavigationOptions<RouteBreadcrumb>().ActivateByRoute(null!),
            () => _ = new BreadcrumbsTagHelper(null!),
            () => new BreadcrumbsTagHelper(new FakeUrlHelperFactory()).Process(Context(), null!),
        };

        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentNullException>();
        }
    }

    [Fact]
    public void Tag_helper_renders_text_for_items_without_link_and_keeps_aria_label()
    {
        var parent = new RouteBreadcrumb { Title = "Catalog" };
        var helper = new BreadcrumbsTagHelper(new FakeUrlHelperFactory())
        {
            Items = [parent, new RouteBreadcrumb { Title = "Orders", Controller = "Orders", Action = "Index" }],
            ViewContext = TestContextFactory.ViewContext(Request(Mvc("Home", "Index")), Mvc("Home", "Index")),
        };
        var output = new TagHelperOutput("breadcrumbs", [new TagHelperAttribute("aria-label", "Path")],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(Context("breadcrumbs"), output);

        TestUtils.TagHelperRunner.Render(output).Should().Be(
            """<nav aria-label="Path"><ol class="breadcrumb"><li class="breadcrumb-item">Catalog</li>"""
            + """<li class="breadcrumb-item"><a href="/Orders/Index?area=">Orders</a></li></ol></nav>""");
    }

    [Fact]
    public void Tag_helper_renders_nothing_without_items()
    {
        var output = Output("breadcrumbs");
        new BreadcrumbsTagHelper(new FakeUrlHelperFactory()) { Items = null }.Process(Context("breadcrumbs"), output);

        TestUtils.TagHelperRunner.Render(output).Should().BeEmpty();
    }
}
