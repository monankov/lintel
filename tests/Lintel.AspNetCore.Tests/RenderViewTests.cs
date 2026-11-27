using Lintel.AspNetCore.Mvc;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Tests;

public class RenderViewTests
{
    private sealed class TestController : Controller;

    /// <summary>Writes "{view name}:{model}" so that tests can see which view rendered which model.</summary>
    private sealed class EchoView(string path) : IView
    {
        public string Path { get; } = path;

        public Task RenderAsync(ViewContext context) => context.Writer.WriteAsync($"{Path}:{context.ViewData.Model}");
    }

    private static (TestController Controller, ICompositeViewEngine Engine) Create(string actionName = "Details")
    {
        var engine = Substitute.For<ICompositeViewEngine>();
        engine.FindView(Arg.Any<ActionContext>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(call => ViewEngineResult.Found(call.ArgAt<string>(1), new EchoView(call.ArgAt<string>(1))));
        engine.GetView(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(call => ViewEngineResult.Found(call.ArgAt<string>(1), new EchoView(call.ArgAt<string>(1))));

        var tempDataFactory = Substitute.For<ITempDataDictionaryFactory>();
        tempDataFactory.GetTempData(Arg.Any<HttpContext>()).Returns(call => new TempDataDictionary(call.Arg<HttpContext>(), Substitute.For<ITempDataProvider>()));

        var services = new ServiceCollection()
            .AddSingleton(engine)
            .AddSingleton(tempDataFactory)
            .BuildServiceProvider();

        var httpContext = TestContextFactory.HttpContext(services: services);
        var controller = new TestController
        {
            ControllerContext = new ControllerContext(new ActionContext(httpContext, new RouteData(),
                new ControllerActionDescriptor { ActionName = actionName })),
        };
        return (controller, engine);
    }

    [Fact]
    public async Task RenderPartialViewAsync_finds_view_without_layout_and_renders_model()
    {
        var (controller, engine) = Create();

        var html = await controller.RenderPartialViewAsync("_OrderRow", 42);

        html.Should().Be("_OrderRow:42");
        controller.ViewData.Model.Should().Be(42);
        engine.Received(1).FindView(controller.ControllerContext, "_OrderRow", false);
    }

    [Fact]
    public async Task RenderViewAsync_renders_main_page_and_defaults_to_action_name()
    {
        var (controller, engine) = Create(actionName: "Details");

        var html = await controller.RenderViewAsync(null, "model");

        html.Should().Be("Details:model");
        engine.Received(1).FindView(controller.ControllerContext, "Details", true);
    }

    [Theory]
    [InlineData("~/Views/Shared/Card.cshtml")]
    [InlineData("/Views/Shared/Card.cshtml")]
    public async Task RenderViewAsync_uses_view_path_directly(string path)
    {
        var (controller, engine) = Create();

        var html = await controller.RenderPartialViewAsync(path, 1);

        html.Should().Be($"{path}:1");
        engine.Received(1).GetView(null, path, false);
        engine.DidNotReceiveWithAnyArgs().FindView(default!, default!, default);
    }

    [Fact]
    public async Task RenderViewAsync_throws_with_searched_locations_when_view_is_missing()
    {
        var (controller, engine) = Create();
        engine.FindView(Arg.Any<ActionContext>(), "Missing", Arg.Any<bool>())
            .Returns(ViewEngineResult.NotFound("Missing", ["/Views/Test/Missing.cshtml", "/Views/Shared/Missing.cshtml"]));

        var act = () => controller.RenderViewAsync("Missing", 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A view with the name \"Missing\" could not be found. Searched locations: /Views/Test/Missing.cshtml, /Views/Shared/Missing.cshtml.");
    }

    [Fact]
    public async Task RenderViewAsync_requires_controller()
    {
        var act = () => ((Controller)null!).RenderViewAsync("View", 1);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
