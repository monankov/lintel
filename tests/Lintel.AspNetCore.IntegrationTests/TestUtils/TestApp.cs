using System.Net;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Lintel.AspNetCore.IntegrationTests.TestUtils;

/// <summary>The test web application, shared by the tests of a class.</summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

    /// <summary>A client that does not follow redirects and keeps cookies (TempData).</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://shop.test"),
    });
}

internal static class HttpExtensions
{
    private static readonly IBrowsingContext _context = BrowsingContext.New(Configuration.Default);

    public static async Task<IDocument> GetDocumentAsync(this HttpClient client, string url, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        configure?.Invoke(request);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await ParseAsync(response);
    }

    public static async Task<IDocument> ParseAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return await _context.OpenAsync(request => request.Content(html).Address(response.RequestMessage!.RequestUri!.ToString()),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Posts the form with its hidden fields (antiforgery token, referrer) and the given values.</summary>
    public static Task<HttpResponseMessage> SubmitAsync(this HttpClient client, IDocument page, string formSelector,
        IDictionary<string, string>? values = null, Action<Dictionary<string, string>>? tamper = null)
    {
        var form = page.QuerySelector<IHtmlFormElement>(formSelector) ?? throw new InvalidOperationException($"Form '{formSelector}' not found.");

        var fields = form.Elements.OfType<IHtmlInputElement>()
            .Where(input => !string.IsNullOrEmpty(input.Name) && input.Type is not ("checkbox" or "submit"))
            .GroupBy(input => input.Name!)
            .ToDictionary(group => group.Key, group => group.First().Value);

        foreach (var (name, value) in values ?? new Dictionary<string, string>())
        {
            fields[name] = value;
        }
        tamper?.Invoke(fields);

        var action = form.GetAttribute("action") ?? page.Url;
        return client.PostAsync(action, new FormUrlEncodedContent(fields), TestContext.Current.CancellationToken);
    }

    public static IEnumerable<string> Hrefs(this IParentNode node, string selector)
        => node.QuerySelectorAll(selector).Select(element => element.GetAttribute("href") ?? "");
}
