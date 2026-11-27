using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Lintel.AspNetCore.TagHelpers.Internal;

internal static class CurrentPageUrl
{
    /// <summary>
    /// Builds a link to the current page with other route/query values: through the MVC action when the route has
    /// controller and action (so route templates such as <c>{page}</c> are respected, as in Structr), otherwise the
    /// current path with a new query string. Razor Pages use the path: their <c>page</c> route value would clash with
    /// the default <c>page</c> query parameter of the page number.
    /// </summary>
    public static string? Build(ViewContext viewContext, IUrlHelperFactory urlHelperFactory, RouteValueDictionary values)
    {
        var route = viewContext.RouteData.Values;

        if (route["controller"]?.ToString() is { Length: > 0 } controller && route["action"]?.ToString() is { Length: > 0 } action)
        {
            return urlHelperFactory.GetUrlHelper(viewContext).Action(action, controller, values);
        }

        var request = viewContext.HttpContext.Request;
        var query = QueryString.Create(values.SelectMany(Expand));
        return request.PathBase.Add(request.Path) + query.ToUriComponent();
    }

    private static IEnumerable<KeyValuePair<string, string?>> Expand(KeyValuePair<string, object?> value)
        => value.Value is StringValues many
            ? many.Select(item => KeyValuePair.Create(value.Key, item))
            : [KeyValuePair.Create(value.Key, value.Value?.ToString())];
}
