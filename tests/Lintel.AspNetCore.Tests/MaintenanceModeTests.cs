using Lintel.AspNetCore.Maintenance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Lintel.AspNetCore.Tests;

public sealed class MaintenanceModeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("lintel-maintenance-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FixedProvider(bool enabled) : IMaintenanceModeProvider
    {
        public ValueTask<bool> IsEnabledAsync(HttpContext context) => ValueTask.FromResult(enabled);
    }

    private async Task<(DefaultHttpContext Context, bool NextCalled, string Body)> InvokeAsync(
        MaintenanceModeOptions options, bool enabled = true, string path = "/orders")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        var monitor = Substitute.For<IOptionsMonitor<MaintenanceModeOptions>>();
        monitor.CurrentValue.Returns(options);
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(_root);
        environment.ContentRootFileProvider.Returns(new PhysicalFileProvider(_root));

        var nextCalled = false;
        var middleware = new MaintenanceModeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor, new FixedProvider(enabled), environment);
        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        return (context, nextCalled, await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_maintenance_requests_pass()
    {
        var (_, nextCalled, body) = await InvokeAsync(new MaintenanceModeOptions(), enabled: false);

        nextCalled.Should().BeTrue();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task During_maintenance_returns_503_with_message()
    {
        var (context, nextCalled, body) = await InvokeAsync(new MaintenanceModeOptions());

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(503);
        context.Response.ContentType.Should().Be("text/plain; charset=utf-8");
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        context.Response.Headers.ContainsKey("Retry-After").Should().BeFalse();
        body.Should().Be("The application is temporarily unavailable. Please try again later.");
    }

    [Fact]
    public async Task Page_relative_to_content_root_and_retry_after_are_sent()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "maintenance.html"), "<h1>Работы</h1>", TestContext.Current.CancellationToken);

        var (context, _, body) = await InvokeAsync(new MaintenanceModeOptions
        {
            PageFilePath = "maintenance.html",
            RetryAfterSeconds = 600,
            StatusCode = 200,
        });

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        context.Response.Headers.RetryAfter.ToString().Should().Be("600");
        body.Should().Be("<h1>Работы</h1>");
    }

    [Fact]
    public async Task Missing_page_falls_back_to_message()
        => (await InvokeAsync(new MaintenanceModeOptions { PageFilePath = "missing.html", Message = "Закрыто." })).Body.Should().Be("Закрыто.");

    [Theory]
    [InlineData("/health", true)]
    [InlineData("/HEALTH/ready", true)]
    [InlineData("/healthy", false)]
    [InlineData("/orders", false)]
    public async Task Excluded_paths_stay_available(string path, bool available)
    {
        var options = new MaintenanceModeOptions();
        options.ExcludedPaths.Add("/health");

        (await InvokeAsync(options, path: path)).NextCalled.Should().Be(available);
    }

    [Fact]
    public async Task Bypass_lets_chosen_requests_through()
    {
        var options = new MaintenanceModeOptions { Bypass = context => context.Request.Path == "/admin" };

        (await InvokeAsync(options, path: "/admin")).NextCalled.Should().BeTrue();
        (await InvokeAsync(options, path: "/orders")).NextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Registration_keeps_own_provider_and_checks_arguments()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMaintenanceModeProvider>(new FixedProvider(true))
            .AddMaintenanceMode()
            .BuildServiceProvider();
        services.GetRequiredService<IMaintenanceModeProvider>().Should().BeOfType<FixedProvider>();

        var defaults = new ServiceCollection().AddMaintenanceMode(o => o.Enabled = true).BuildServiceProvider();
        (await defaults.GetRequiredService<IMaintenanceModeProvider>().IsEnabledAsync(new DefaultHttpContext())).Should().BeTrue();

        var monitor = Substitute.For<IOptionsMonitor<MaintenanceModeOptions>>();
        var provider = new FixedProvider(false);
        static Task next(HttpContext _) => Task.CompletedTask;
        var actions = new Action[]
        {
            () => ((IServiceCollection)null!).AddMaintenanceMode(),
            () => ((IServiceCollection)null!).AddMaintenanceMode(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            () => new ServiceCollection().AddMaintenanceMode((Microsoft.Extensions.Configuration.IConfiguration)null!),
            () => ((IApplicationBuilder)null!).UseMaintenanceMode(),
            () => _ = new OptionsMaintenanceModeProvider(null!),
            () => _ = new MaintenanceModeMiddleware(null!, monitor, provider),
            () => _ = new MaintenanceModeMiddleware(next, null!, provider),
            () => _ = new MaintenanceModeMiddleware(next, monitor, null!),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentNullException>();
        }

        var middleware = new MaintenanceModeMiddleware(next, monitor, provider);
        await middleware.Invoking(m => m.InvokeAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }
}
