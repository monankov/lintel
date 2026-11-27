using System.Net;
using Lintel.AspNetCore.Maintenance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.IntegrationTests;

/// <summary>The maintenance mode in a real pipeline with reloadable configuration.</summary>
public sealed class MaintenanceModeIntegrationTests
{
    [Fact]
    public async Task Switches_with_configuration_without_restart()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection([new("MaintenanceMode:Enabled", "false"), new("MaintenanceMode:ExcludedPaths:0", "/health")]);
        builder.Services.AddMaintenanceMode(builder.Configuration.GetSection("MaintenanceMode"), o => o.Message = "Maintenance");
        await using var app = builder.Build();
        app.UseMaintenanceMode();
        app.MapGet("/", () => "ok");
        app.MapGet("/health", () => "healthy");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();
        var token = TestContext.Current.CancellationToken;

        (await client.GetStringAsync("/", token)).Should().Be("ok");

        app.Configuration["MaintenanceMode:Enabled"] = "true";
        ((IConfigurationRoot)app.Configuration).Reload();

        using var closed = await client.GetAsync("/", token);
        closed.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await closed.Content.ReadAsStringAsync(token)).Should().Be("Maintenance");
        (await client.GetStringAsync("/health", token)).Should().Be("healthy");
    }
}
