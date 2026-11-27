using System.Globalization;
using Lintel.Navigation.Providers;
using Lintel.Navigation.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Navigation.Tests;

public enum Area
{
    Public,
    Admin,
}

public sealed class MenuItem : NavigationItem<MenuItem>
{
    public string? Url { get; set; }
    public Area Area { get; set; }
    public int Order { get; set; }
    public bool Hidden { get; set; }
    public string? Icon { get; private set; }
}

public sealed class CountingProvider(params MenuItem[] items) : INavigationProvider<MenuItem>
{
    public int Calls { get; private set; }

    public IEnumerable<MenuItem> CreateNavigation()
    {
        Calls++;
        return items;
    }
}

public sealed class NavigationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lintel-navigation-" + Guid.NewGuid().ToString("N"));

    public NavigationTests() => Directory.CreateDirectory(_directory);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private const string Json = """
        [
          // top level
          { "id": "home", "title": "Home", "url": "/" },
          { "Id": "orders", "Title": "Orders", "Url": "/orders", "Icon": "box", "Order": 2, "Area": "Admin",
            "children": [
              { "Id": "new-order", "Title": "New", "Url": "/orders/new", "ResourceName": "NewOrderTitle",
                "Children": [ { "Id": "new-order-step2", "Title": "Step 2", "Url": "/orders/new/2" } ] },
              { "Id": "archive", "Title": "Archive", "Url": "/orders/archive", "Hidden": true },
            ] }
        ]
        """;

    private IServiceProvider Services(Action<IServiceProvider, NavigationOptions<MenuItem>>? configure = null, string? currentUrl = null)
    {
        var path = WriteFile("navigation.json", Json);
        var services = new ServiceCollection();
        services.AddNavigation().AddJson<MenuItem>(path, (sp, options) =>
        {
            options.ItemActivator = item => item.Url == currentUrl;
            configure?.Invoke(sp, options);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Json_provider_reads_tree_with_custom_and_private_properties()
    {
        var items = new JsonNavigationProvider<MenuItem>(WriteFile("n.json", Json)).CreateNavigation().ToList();

        items.Select(i => i.Id).Should().Equal("home", "orders");
        var orders = items[1];
        orders.Icon.Should().Be("box");
        orders.Area.Should().Be(Area.Admin);
        orders.Order.Should().Be(2);
        orders.Children.Select(c => c.Id).Should().Equal("new-order", "archive");
        orders.Descendants.Select(c => c.Id).Should().Equal("new-order", "new-order-step2", "archive");
        orders.Children.First().Children.Single().Ancestors.Select(a => a.Id).Should().Equal("orders", "new-order");
    }

    [Fact]
    public void Xml_provider_reads_attributes_and_nested_items()
    {
        var path = WriteFile("navigation.xml", """
            <navigation>
              <item id="home" title="Home" url="/" />
              <item ID="orders" Title="Orders" url="/orders" order="2" area="admin" hidden="false" icon="box" unknown="ignored">
                <item id="new-order" title="New" url="/orders/new" />
              </item>
            </navigation>
            """);

        var items = new XmlNavigationProvider<MenuItem>(path).CreateNavigation().ToList();

        items.Should().HaveCount(2);
        items[1].Should().Match<MenuItem>(i => i.Id == "orders" && i.Order == 2 && i.Area == Area.Admin && i.Icon == "box");
        items[1].Children.Single().Parent.Should().BeSameAs(items[1]);
    }

    [Fact]
    public void AddXml_registers_navigation_and_breadcrumbs()
    {
        var path = WriteFile("menu.xml", """
            <navigation>
              <item id="home" title="Home" url="/" />
              <item id="orders" title="Orders" url="/orders">
                <item id="new-order" title="New" url="/orders/new" hidden="true" />
                <item id="archive" title="Archive" url="/orders/archive" />
              </item>
            </navigation>
            """);
        var services = new ServiceCollection();
        services.AddNavigation().AddXml<MenuItem>(path, (_, options) =>
        {
            options.ItemFilter = item => !item.Hidden;
            options.ItemActivator = item => item.Url == "/orders/archive";
        });
        using var scope = services.BuildServiceProvider().CreateScope();

        var navigation = scope.ServiceProvider.GetRequiredService<INavigation<MenuItem>>();
        var breadcrumbs = scope.ServiceProvider.GetRequiredService<IBreadcrumbNavigation<MenuItem>>();

        navigation.Select(i => i.Id).Should().Equal("home", "orders");
        navigation.Single(i => i.Id == "orders").Children.Select(c => c.Id).Should().Equal("archive");
        breadcrumbs.Select(b => b.Id).Should().Equal("orders", "archive");
    }

    [Fact]
    public void Xml_provider_reports_bad_values()
    {
        var path = WriteFile("bad.xml", """<navigation><item id="x" order="many" /></navigation>""");

        var act = () => new XmlNavigationProvider<MenuItem>(path).CreateNavigation();

        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot convert 'many' to Int32 for MenuItem.Order.");
    }

    [Fact]
    public void Providers_require_existing_file()
    {
        var json = () => new JsonNavigationProvider<MenuItem>(Path.Combine(_directory, "missing.json")).CreateNavigation();
        var xml = () => new XmlNavigationProvider<MenuItem>(Path.Combine(_directory, "missing.xml")).CreateNavigation();

        json.Should().Throw<FileNotFoundException>();
        xml.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Filter_hides_items_with_their_children()
    {
        var navigation = Services((_, o) => o.ItemFilter = item => !item.Hidden && item.Id != "new-order")
            .CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>();

        navigation.Single(i => i.Id == "orders").Descendants.Should().BeEmpty();
    }

    [Fact]
    public void Navigation_and_breadcrumbs_in_the_same_scope_both_see_the_active_item()
    {
        using var scope = Services(currentUrl: "/orders/new/2").CreateScope();

        var navigation = scope.ServiceProvider.GetRequiredService<INavigation<MenuItem>>();
        var breadcrumbs = scope.ServiceProvider.GetRequiredService<IBreadcrumbNavigation<MenuItem>>();

        navigation.Active!.Id.Should().Be("new-order-step2");
        navigation.Single(i => i.Id == "orders").HasActiveDescendant.Should().BeTrue();
        breadcrumbs.Select(b => b.Id).Should().Equal("orders", "new-order", "new-order-step2");
        breadcrumbs.Active!.Id.Should().Be("new-order-step2");
    }

    [Fact]
    public void Only_the_first_matching_item_is_active()
    {
        var navigation = Services((_, o) => o.ItemActivator = item => item.Url!.StartsWith("/orders", StringComparison.Ordinal))
            .CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>();

        navigation.Active!.Id.Should().Be("orders");
        navigation.SelectMany(i => i.Descendants.Prepend(i)).Count(i => i.IsActive).Should().Be(1);
    }

    [Fact]
    public void Without_active_item_breadcrumbs_are_empty()
        => Services(currentUrl: "/nowhere").CreateScope().ServiceProvider.GetRequiredService<IBreadcrumbNavigation<MenuItem>>()
            .Should().BeEmpty();

    [Fact]
    public void Titles_are_localized_in_the_culture_of_each_request_even_with_cache()
    {
        var services = Services((_, o) => o.ResourceType = typeof(Menu));
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
            var russian = services.CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>().Single(i => i.Id == "orders");

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var english = services.CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>().Single(i => i.Id == "orders");

            russian.Title.Should().Be("Заказы");
            russian.Children.First().Title.Should().Be("Новый заказ");
            english.Title.Should().Be("Orders");
            english.Children.First().Title.Should().Be("New order");
            english.Children.Last().Title.Should().Be("Archive", "items without resources keep their titles");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Source_tree_is_cached_and_never_modified()
    {
        var provider = new CountingProvider(new MenuItem { Id = "home", Title = "Home", Url = "/" });
        var services = new ServiceCollection();
        services.AddNavigation().AddProvider(provider, o => o.ItemActivator = _ => true);
        var serviceProvider = services.BuildServiceProvider();

        var first = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>();
        var second = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>();

        provider.Calls.Should().Be(1);
        first.Single().Should().NotBeSameAs(second.Single());
        first.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public void Caching_can_be_disabled()
    {
        var provider = new CountingProvider(new MenuItem { Id = "home" });
        var services = new ServiceCollection();
        services.AddNavigation().AddProvider(provider, o => o.EnableCaching = false);
        var serviceProvider = services.BuildServiceProvider();

        _ = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>();
        _ = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>();

        provider.Calls.Should().Be(2);
    }

    [Fact]
    public void Items_are_equal_by_id_case_insensitively()
    {
        var a = new MenuItem { Id = "Orders" };

        a.Should().Be(new MenuItem { Id = "orders" });
        a.GetHashCode().Should().Be(new MenuItem { Id = "ORDERS" }.GetHashCode());
        new MenuItem().Should().NotBe(new MenuItem());
        var noId = new MenuItem();
        noId.Equals(noId).Should().BeTrue();
    }

    [Fact]
    public void AddChild_moves_item_and_rejects_cycles()
    {
        var root = new MenuItem { Id = "root" };
        var a = new MenuItem { Id = "a" };
        var b = new MenuItem { Id = "b" };
        root.AddChild(a);
        root.AddChild(b);
        var leaf = new MenuItem { Id = "leaf" };
        a.AddChild(leaf);

        b.AddChild(leaf);

        a.Children.Should().BeEmpty();
        leaf.Ancestors.Select(x => x.Id).Should().Equal("root", "b");
        root.Descendants.Select(x => x.Id).Should().Equal("a", "b", "leaf");

        var cycle = () => leaf.AddChild(root);
        cycle.Should().Throw<InvalidOperationException>();

        b.RemoveChild(leaf);
        leaf.Parent.Should().BeNull();
        root.Descendants.Should().NotContain(leaf);
    }

    [Fact]
    public void Active_flags_describe_the_position_of_the_active_item()
    {
        using var scope = Services(currentUrl: "/orders/new").CreateScope();
        var navigation = scope.ServiceProvider.GetRequiredService<INavigation<MenuItem>>();

        var orders = navigation.Single(i => i.Id == "orders");
        var newOrder = orders.Children.First();
        var step2 = newOrder.Children.Single();

        orders.HasChildren.Should().BeTrue();
        orders.HasActiveChild.Should().BeTrue();
        newOrder.IsActive.Should().BeTrue();
        newOrder.HasActiveChild.Should().BeFalse();
        step2.HasChildren.Should().BeFalse();
        step2.HasActiveAncestor.Should().BeTrue();
        orders.HasActiveAncestor.Should().BeFalse();
        NonGeneric(navigation).Select(i => i.Id).Should().Equal("home", "orders");
        NonGeneric(scope.ServiceProvider.GetRequiredService<IBreadcrumbNavigation<MenuItem>>())
            .Select(i => i.Id).Should().Equal("orders", "new-order");
    }

    private static List<MenuItem> NonGeneric(System.Collections.IEnumerable items)
    {
        var result = new List<MenuItem>();
        var enumerator = items.GetEnumerator();
        while (enumerator.MoveNext())
        {
            result.Add((MenuItem)enumerator.Current!);
        }
        return result;
    }

    private sealed class NullProvider : INavigationProvider<MenuItem>
    {
        public IEnumerable<MenuItem> CreateNavigation() => null!;
    }

    [Fact]
    public void Empty_sources_give_empty_navigation()
    {
        var emptyJson = new JsonNavigationProvider<MenuItem>(WriteFile("empty.json", "  ")).CreateNavigation();
        var services = new ServiceCollection();
        services.AddNavigation().AddProvider(new NullProvider(), o =>
        {
            o.ItemFilter = null;
            o.ItemActivator = null;
        });

        emptyJson.Should().BeEmpty();
        services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<INavigation<MenuItem>>()
            .Should().BeEmpty();
    }

    [Fact]
    public void Equality_handles_null_other_types_and_missing_ids()
    {
        var item = new MenuItem { Id = "orders" };
        var withoutId = new MenuItem();

        item.Equals("orders").Should().BeFalse();
        item.Equals((object)new MenuItem { Id = "ORDERS" }).Should().BeTrue();
        withoutId.Equals(new MenuItem()).Should().BeFalse();
        withoutId.GetHashCode().Should().Be(withoutId.GetHashCode());
        new HashSet<MenuItem> { withoutId, new() }.Should().HaveCount(2);

        // Last: the compiler treats x.Equals(null) as a null check of x.
        item.Equals(null).Should().BeFalse();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
