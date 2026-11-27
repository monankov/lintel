using System.Security.Claims;
using Lintel.AspNetCore.Navigation;
using Lintel.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Tests;

public class MenuTests
{
    private sealed class ListProvider(params MenuItem[] items) : INavigationProvider<MenuItem>
    {
        public int Calls { get; private set; }

        public IEnumerable<MenuItem> CreateNavigation()
        {
            Calls++;
            return items;
        }
    }

    private static MenuItem Item(string id, string? url = null, string? policy = null, params MenuItem[] children)
    {
        var item = new MenuItem { Id = id, Title = id, Url = url, Policy = policy };
        foreach (var child in children)
        {
            item.AddChild(child);
        }
        return item;
    }

    private static MenuItem[] Tree() =>
    [
        Item("main", null, null,
            Item("home", "/"),
            Item("orders", "/orders/"),
            Item("drafts", "/orders/drafts?sort=date"),
            Item("external", "https://example.test/orders"),
            Item("protocol-relative", "//example.test/orders")),
        Item("admin", null, null, Item("users", "/admin/users", "Admins")),
    ];

    private static (IServiceProvider Services, HttpContextAccessor Accessor, ListProvider Provider) Services(
        MenuItem[]? tree = null, string path = "/", Endpoint? endpoint = null, bool authorization = true, ClaimsPrincipal? user = null)
    {
        var provider = new ListProvider(tree ?? Tree());
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Path = path;
        accessor.HttpContext.User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        if (endpoint is not null)
        {
            accessor.HttpContext.SetEndpoint(endpoint);
        }

        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton<INavigationProvider<MenuItem>>(provider)
            .AddLogging();
        services.AddNavigation();
        if (authorization)
        {
            services.AddAuthorization(o => o.AddPolicy("Admins", p => p.RequireRole("Admin")));
        }
        return (services.BuildServiceProvider(), accessor, provider);
    }

    private static Endpoint WithMenu(string id) => new(null, new EndpointMetadataCollection(new MenuAttribute(id)), "test");

    private static List<string?> Active(MenuItem[] tree, NavigationOptions<MenuItem> options)
        => tree.SelectMany(root => root.Descendants.Prepend(root)).Where(item => options.ItemActivator!(item)).Select(item => item.Id).ToList();

    [Theory]
    [InlineData("/", "home")]
    [InlineData("/orders", "orders")]
    [InlineData("/ORDERS/42", "orders")]
    [InlineData("/orders/drafts/7", "drafts")]
    [InlineData("/ordersarchive", null)]
    [InlineData("/about", null)]
    public void Url_activation_takes_the_longest_segment_prefix(string path, string? expected)
    {
        var tree = Tree();
        var (services, _, _) = Services(tree, path);

        var active = Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(services));

        active.Should().Equal(expected is null ? [] : [expected]);
    }

    [Fact]
    public void Menu_attribute_wins_over_url_and_disables_url_matching()
    {
        var tree = Tree();

        Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(Services(tree, "/orders", WithMenu("USERS")).Services))
            .Should().Equal("users");
        Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(Services(tree, "/orders", WithMenu("unknown")).Services))
            .Should().BeEmpty();
    }

    [Fact]
    public void Url_matching_can_be_turned_off_and_needs_a_request()
    {
        var tree = Tree();
        var (services, accessor, _) = Services(tree, "/orders");

        Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(services, matchUrl: false)).Should().BeEmpty();

        var options = new NavigationOptions<MenuItem>().ActivateMenu(services);
        accessor.HttpContext = null;
        Active(tree, options).Should().BeEmpty();
    }

    [Fact]
    public void Url_of_the_tree_is_computed_once_per_request_from_cache_or_provider()
    {
        var tree = Tree();
        var (services, _, provider) = Services(tree, "/orders");

        Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(services));
        Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(services));
        provider.Calls.Should().Be(1, "the tree is cached");

        Active(tree, new NavigationOptions<MenuItem> { EnableCaching = false }.ActivateMenu(services));
        provider.Calls.Should().Be(2);
    }

    [Fact]
    public void Without_registered_provider_the_tree_of_the_item_is_used()
    {
        var tree = Tree();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Path = "/orders";
        var services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).BuildServiceProvider();

        Active(tree, new NavigationOptions<MenuItem>().ActivateMenu(services)).Should().Equal("orders");

        var single = Item("alone", "/orders");
        new NavigationOptions<MenuItem>().ActivateMenu(services).ItemActivator!(single).Should().BeTrue();
    }

    [Fact]
    public void Policy_filter_hides_items_and_empty_groups_and_keeps_previous_filter()
    {
        var tree = Tree();
        var (services, _, _) = Services(tree);
        var options = new NavigationOptions<MenuItem> { ItemFilter = item => item.Id != "external" }.FilterMenuByPolicy(services);
        var visible = tree.SelectMany(root => root.Descendants.Prepend(root)).Where(item => options.ItemFilter!(item)).Select(item => item.Id);

        visible.Should().Equal("main", "home", "orders", "drafts", "protocol-relative");
        Item("empty-group").Should().Match<MenuItem>(group => !options.ItemFilter!(group));
    }

    [Fact]
    public void Policy_filter_shows_items_to_authorized_users_and_evaluates_each_policy_once()
    {
        var tree = Tree();
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));
        var (services, _, _) = Services(tree, user: admin);
        var options = new NavigationOptions<MenuItem>().FilterMenuByPolicy(services);

        options.ItemFilter!(tree[1]).Should().BeTrue();
        options.ItemFilter!(Item("other-users", "/admin/users", "Admins")).Should().BeTrue();
    }

    [Fact]
    public void Policy_requires_authorization_services()
    {
        var (services, _, _) = Services(authorization: false);
        var options = new NavigationOptions<MenuItem>().FilterMenuByPolicy(services);

        options.ItemFilter!(Item("home", "/")).Should().BeTrue("items without policy need no authorization");
        var withPolicy = () => options.ItemFilter!(Item("users", "/admin/users", "Admins"));
        withPolicy.Should().Throw<InvalidOperationException>().WithMessage("*AddAuthorization*");
    }

    [Fact]
    public void Policy_of_a_request_without_context_is_checked_for_anonymous_user()
    {
        var (services, accessor, _) = Services();
        accessor.HttpContext = null;

        new NavigationOptions<MenuItem>().FilterMenuByPolicy(services).ItemFilter!(Item("users", "/admin/users", "Admins")).Should().BeFalse();
    }

    [Fact]
    public void Menu_item_reports_url_icon_and_links_below()
    {
        var group = Item("group", null, null, Item("child", "/x"));

        group.HasUrl.Should().BeFalse();
        group.HasDescendantWithUrl.Should().BeTrue();
        new MenuItem { Icon = "bi bi-house" }.HasIcon.Should().BeTrue();
        new MenuItem().HasIcon.Should().BeFalse();
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var (services, _, _) = Services();
        var actions = new Action[]
        {
            () => _ = new MenuAttribute(" "),
            () => ((NavigationOptions<MenuItem>)null!).ActivateMenu(services),
            () => new NavigationOptions<MenuItem>().ActivateMenu(null!),
            () => ((NavigationOptions<MenuItem>)null!).FilterMenuByPolicy(services),
            () => new NavigationOptions<MenuItem>().FilterMenuByPolicy(null!),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentException>();
        }
        new MenuAttribute("orders").Id.Should().Be("orders");
    }
}
