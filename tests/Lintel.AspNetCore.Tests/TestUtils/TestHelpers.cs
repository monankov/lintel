using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace Lintel.AspNetCore.Tests.TestUtils;

/// <summary>Builds URLs as "/{controller}/{action}?k=v" or "{page}?k=v" so that tests can check them.</summary>
public sealed class FakeUrlHelper(ActionContext actionContext) : IUrlHelper
{
    public ActionContext ActionContext { get; } = actionContext;

    public string? Action(UrlActionContext actionContext)
        => $"/{actionContext.Controller}/{actionContext.Action}" + Query(new RouteValueDictionary(actionContext.Values));

    public string? RouteUrl(UrlRouteContext routeContext)
    {
        var values = new RouteValueDictionary(routeContext.Values);
        var page = values["page"];
        values.Remove("page");
        return $"{page}" + Query(values);
    }

    public string? Content(string? contentPath) => contentPath;

    public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true;

    public string? Link(string? routeName, object? values) => null;

    private static string Query(RouteValueDictionary values)
        => QueryString.Create(values.OrderBy(v => v.Key, StringComparer.Ordinal)
            .Select(v => KeyValuePair.Create(v.Key, v.Value?.ToString()))).ToUriComponent();
}

public sealed class FakeUrlHelperFactory : IUrlHelperFactory
{
    public IUrlHelper GetUrlHelper(ActionContext context) => new FakeUrlHelper(context);
}

internal static class TestContextFactory
{
    public static DefaultHttpContext HttpContext(string path = "/", string query = "", IServiceProvider? services = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("shop.test");
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        if (services is not null)
        {
            context.RequestServices = services;
        }
        return context;
    }

    public static ViewContext ViewContext(HttpContext httpContext, RouteValueDictionary? routeValues = null)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(routeValues ?? new RouteValueDictionary()), new ActionDescriptor());
        return new ViewContext { HttpContext = httpContext, RouteData = actionContext.RouteData, ActionDescriptor = actionContext.ActionDescriptor };
    }

    public static RouteValueDictionary Mvc(string controller = "Orders", string action = "Index", string? area = null)
    {
        var values = new RouteValueDictionary { ["controller"] = controller, ["action"] = action };
        if (area is not null)
        {
            values["area"] = area;
        }
        return values;
    }
}

internal static class TagHelperRunner
{
    public static TagHelperContext Context(string tagName = "div")
        => new(tagName, [], new Dictionary<object, object>(), Guid.NewGuid().ToString("N"));

    public static TagHelperOutput Output(string tagName = "div", string childContent = "")
        => new(tagName, [], (_, _) =>
        {
            var content = new DefaultTagHelperContent();
            content.SetHtmlContent(childContent);
            return Task.FromResult<TagHelperContent>(content);
        });

    public static string Render(TagHelperOutput output)
    {
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    public static string Html(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
