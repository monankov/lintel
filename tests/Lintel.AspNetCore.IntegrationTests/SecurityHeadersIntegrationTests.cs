using System.Net;
using Lintel.AspNetCore.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Lintel.AspNetCore.IntegrationTests;

/// <summary>Security headers in a real pipeline with an exception handler.</summary>
public sealed class SecurityHeadersIntegrationTests
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(string environment = "Production",
        Action<SecurityHeadersOptions>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseExceptionHandler("/error");
        app.UseSecurityHeaders(configure);
        app.MapGet("/page", () => Results.Text("<p>ok</p>", "text/html"));
        app.MapGet("/report.pdf", () => Results.File("%PDF-1.7"u8.ToArray(), "application/pdf"));
        app.MapGet("/own", (HttpContext context) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
            return "own";
        });
        app.MapGet("/boom", string () => throw new InvalidOperationException("boom"));
        app.MapGet("/error", () => Results.Text("error page", "text/html", statusCode: 500));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return (app, app.GetTestClient());
    }

    private static string Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "";

    [Fact]
    public async Task Pages_get_strict_policy_nosniff_and_referrer_policy()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var page = await client.GetAsync("/page", TestContext.Current.CancellationToken);

        Header(page, "Content-Security-Policy").Should().Be(SecurityHeadersOptions.DefaultContentSecurityPolicy);
        Header(page, "X-Content-Type-Options").Should().Be("nosniff");
        Header(page, "Referrer-Policy").Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Pdf_gets_only_frame_ancestors_and_endpoint_policy_is_kept()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var pdf = await client.GetAsync("/report.pdf", TestContext.Current.CancellationToken);
        using var own = await client.GetAsync("/own", TestContext.Current.CancellationToken);

        Header(pdf, "Content-Security-Policy").Should().Be("frame-ancestors 'self'");
        Header(pdf, "X-Content-Type-Options").Should().Be("nosniff");
        Header(own, "Content-Security-Policy").Should().Be("default-src 'none'");
    }

    [Fact]
    public async Task Error_page_after_an_exception_gets_the_headers()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var error = await client.GetAsync("/boom", TestContext.Current.CancellationToken);

        error.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await error.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("error page");
        Header(error, "Content-Security-Policy").Should().Be(SecurityHeadersOptions.DefaultContentSecurityPolicy);
    }

    [Fact]
    public async Task Development_allows_hot_reload_and_options_turn_headers_off()
    {
        var (development, developmentClient) = await StartAsync("Development");
        await using var _ = development;
        var (custom, customClient) = await StartAsync(configure: o =>
        {
            o.ContentSecurityPolicy = null;
            o.ReferrerPolicy = null;
            o.NoSniff = false;
            o.PdfContentSecurityPolicy = null;
        });
        await using var __ = custom;

        using var page = await developmentClient.GetAsync("/page", TestContext.Current.CancellationToken);
        using var bare = await customClient.GetAsync("/report.pdf", TestContext.Current.CancellationToken);

        Header(page, "Content-Security-Policy").Should().Be(
            SecurityHeadersOptions.DefaultContentSecurityPolicy + "; " + SecurityHeadersOptions.DefaultDevelopmentAdditions);
        bare.Headers.Should().NotContain(h => h.Key == "Content-Security-Policy" || h.Key == "X-Content-Type-Options" || h.Key == "Referrer-Policy");
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var withoutApp = () => ((IApplicationBuilder)null!).UseSecurityHeaders();

        withoutApp.Should().Throw<ArgumentNullException>();
    }
}
