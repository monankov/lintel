using Lintel.AspNetCore.Client.Alerts;
using Lintel.AspNetCore.Client.Options;
using Lintel.AspNetCore.Mvc;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Tests;

public class ClientTests
{
    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _data = new Dictionary<string, object>();

        public IDictionary<string, object> LoadTempData(HttpContext context) => _data;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _data = values;
    }

    private static (HttpContext Context, IServiceProvider Services) Request()
    {
        var services = new ServiceCollection()
            .AddAspNetCore()
            .AddSingleton<ITempDataProvider, MemoryTempDataProvider>()
            .AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>()
            .BuildServiceProvider();

        var context = TestContextFactory.HttpContext(services: services);
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        return (context, services);
    }

    [Fact]
    public void ClientAlert_requires_type_and_message()
    {
        var withoutType = () => new ClientAlert(" ", "Saved");
        var withoutMessage = () => new ClientAlert("success", "");

        withoutType.Should().Throw<ArgumentException>();
        withoutMessage.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Alerts_are_stored_in_temp_data_as_json()
    {
        var (_, services) = Request();
        var provider = services.GetRequiredService<IClientAlertProvider>();

        provider.AddClientAlert(new ClientAlert("success", "Saved"));
        provider.AddClientAlert(new ClientAlert("warning", "Check the dates"));

        provider.GetAllClientAlerts().Select(a => (a.Type, a.Message))
            .Should().Equal(("success", "Saved"), ("warning", "Check the dates"));
    }

    [Fact]
    public async Task ClientAlertResult_adds_alert_and_executes_inner_result()
    {
        var (context, services) = Request();
        var inner = Substitute.For<IActionResult>();
        var actionContext = new ActionContext(context, new RouteData(), new ActionDescriptor());

        await inner.AddClientAlert(new ClientAlert("success", "Saved")).ExecuteResultAsync(actionContext);

        await inner.Received(1).ExecuteResultAsync(actionContext);
        services.GetRequiredService<IClientAlertProvider>().GetAllClientAlerts().Should().ContainSingle();
    }

    [Fact]
    public void TempData_Put_and_Peek_round_trip_with_system_text_json()
    {
        var tempData = new TempDataDictionary(new DefaultHttpContext(), new MemoryTempDataProvider());

        tempData.Put("key", new List<ClientAlert> { new("info", "Привет") });

        tempData.Peek<List<ClientAlert>>("key")!.Single().Message.Should().Be("Привет");
        tempData.Peek<List<ClientAlert>>("missing").Should().BeNull();
    }

    [Theory]
    [InlineData(null, "OrderItems", "EditDraft", null, "order-items.edit-draft")]
    [InlineData("Admin", "Orders", "Index", null, "admin.orders.index")]
    [InlineData(null, null, null, "/Orders/Edit", "orders.edit")]
    [InlineData("Shop", null, null, "/Cart", "shop.cart")]
    public void BuildClientOptionsKey(string? area, string? controller, string? action, string? page, string expected)
    {
        var (_, services) = Request();
        var route = new RouteData();
        foreach (var (key, value) in new[] { ("area", area), ("controller", controller), ("action", action), ("page", page) })
        {
            if (value is not null)
            {
                route.Values[key] = value;
            }
        }

        services.GetRequiredService<IClientOptionProvider>().BuildClientOptionsKey(route).Should().Be(expected);
    }

    [Fact]
    public void BuildClientOptionsKey_throws_for_route_without_action_or_page()
    {
        var (_, services) = Request();

        var act = () => services.GetRequiredService<IClientOptionProvider>().BuildClientOptionsKey(new RouteData());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Client_options_are_merged_per_key()
    {
        var (_, services) = Request();
        var provider = services.GetRequiredService<IClientOptionProvider>();

        provider.AddClientOptions("orders.index", new Dictionary<string, object?> { ["pageSize"] = 10, ["mode"] = "list" });
        provider.AddClientOptions("orders.index", new Dictionary<string, object?> { ["pageSize"] = 20 });
        provider.AddClientOptions("layout", new Dictionary<string, object?> { ["theme"] = "dark" });

        provider.GetClientOptions("orders.index").Should().BeEquivalentTo(new Dictionary<string, object?> { ["pageSize"] = 20, ["mode"] = "list" });
        provider.GetAllClientOptions().Keys.Should().BeEquivalentTo(["orders.index", "layout"]);
    }

    [Fact]
    public void Empty_options_are_rejected()
    {
        var (_, services) = Request();

        var act = () => services.GetRequiredService<IClientOptionProvider>().AddClientOptions("k", []);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Controller_adds_options_from_anonymous_object_under_action_key()
    {
        var (context, services) = Request();
        var controller = new TestController
        {
            ControllerContext = new ControllerContext(new ActionContext(context, new RouteData(TestContextFactory.Mvc("Orders", "Index")),
                new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor())),
        };

        controller.AddClientOptions(new { pageSize = 20, showArchived = true });

        services.GetRequiredService<IClientOptionProvider>().GetClientOptions("orders.index")
            .Should().BeEquivalentTo(new Dictionary<string, object?> { ["pageSize"] = 20, ["showArchived"] = true });
    }

    private sealed class TestController : Controller;
}
