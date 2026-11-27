using System.Net;
using Lintel.AspNetCore.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.IntegrationTests;

/// <summary>Security helpers in a real pipeline behind a simulated reverse proxy.</summary>
public sealed class SecurityIntegrationTests : IAsyncLifetime
{
    // TestServer has no remote address: the test sets it from this header before UseForwardedHeaders, as a connection would.
    private const string ConnectionHeader = "X-Test-Connection-Ip";

    private WebApplication _app = default!;
    private HttpClient _client = default!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("ForwardedHeaders:Enabled", "true"),
            new("ForwardedHeaders:ForwardedForHeaderName", "X-Real-IP"),
            new("ForwardedHeaders:KnownProxies:0", "10.0.0.5"),
            new("AllowedIp:0", "192.168.0.0/16"),
        ]);
        var allowed = IpNetworkList.Parse(builder.Configuration.GetSection("AllowedIp").Get<string[]>()!);
        builder.Services.AddForwardedHeaders(builder.Configuration.GetSection("ForwardedHeaders"));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        builder.Services.AddAuthorization(o => o.AddPolicy("Intranet", p => p.RequireAllowedNetwork(allowed)));

        _app = builder.Build();
        _app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[ConnectionHeader].ToString());
            return next(context);
        });
        _app.UseForwardedHeaders();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGet("/intranet", (HttpContext context) => $"client {context.GetClientIpAddress()}").RequireAuthorization("Intranet");
        await _app.StartAsync(TestContext.Current.CancellationToken);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(string connectionIp, string? realIp = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/intranet");
        request.Headers.Add(ConnectionHeader, connectionIp);
        if (realIp is not null)
        {
            request.Headers.Add("X-Real-IP", realIp);
        }
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Client_from_allowed_network_passes_directly_and_through_the_proxy()
    {
        using var direct = await GetAsync("192.168.1.10");
        using var proxied = await GetAsync("10.0.0.5", realIp: "192.168.7.7");

        direct.StatusCode.Should().Be(HttpStatusCode.OK);
        proxied.StatusCode.Should().Be(HttpStatusCode.OK);
        (await proxied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("client 192.168.7.7");
    }

    [Fact]
    public async Task Proxy_without_client_address_and_spoofed_header_are_rejected()
    {
        using var noClientAddress = await GetAsync("10.0.0.5");
        using var spoofed = await GetAsync("8.8.8.8", realIp: "192.168.1.10");

        noClientAddress.StatusCode.Should().Be(HttpStatusCode.Redirect, "the anonymous user is sent to sign in");
        spoofed.StatusCode.Should().Be(HttpStatusCode.Redirect, "headers of an unknown proxy are ignored");
    }
}
