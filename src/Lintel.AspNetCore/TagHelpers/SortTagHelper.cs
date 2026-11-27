using Lintel.AspNetCore.Http;
using Lintel.AspNetCore.TagHelpers.Internal;
using Lintel.Querying;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// Wraps the content of an element (usually a table header) in a link that sorts by <see cref="Sort"/>,
/// toggling the order: <c>&lt;th asp-sort="Name"&gt;Name&lt;/th&gt;</c>.
/// </summary>
/// <remarks>
/// The link has the classes <c>sort</c>, the current order (<c>asc</c>/<c>desc</c>) and <c>active</c> when the list is
/// sorted by this column. The current query string is kept. Pair with <c>query.OrderBy(sort, order)</c> from Lintel.Querying.
/// </remarks>
[HtmlTargetElement(Attributes = "asp-sort")]
public class SortTagHelper : TagHelper
{
    private readonly IUrlHelperFactory _urlHelperFactory;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="urlHelperFactory">The URL helper factory.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="urlHelperFactory"/> is <see langword="null"/>.</exception>
    public SortTagHelper(IUrlHelperFactory urlHelperFactory)
    {
        ArgumentNullException.ThrowIfNull(urlHelperFactory);

        _urlHelperFactory = urlHelperFactory;
    }

    /// <summary>The property to sort by.</summary>
    [HtmlAttributeName("asp-sort")]
    public string? Sort { get; set; }

    /// <summary>The sort applied when the request has none; overrides the query string.</summary>
    [HtmlAttributeName("asp-default-sort")]
    public string? DefaultSort { get; set; }

    /// <summary>The order applied with <see cref="DefaultSort"/>.</summary>
    [HtmlAttributeName("asp-default-order")]
    public SortOrder? DefaultOrder { get; set; }

    /// <summary>The options.</summary>
    [HtmlAttributeName("asp-options")]
    public SortOptions? Options { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">If <see cref="Sort"/> is empty.</exception>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (string.IsNullOrWhiteSpace(Sort))
        {
            throw new InvalidOperationException("Sort must be not null.");
        }

        var options = Options ?? new SortOptions();
        var sort = Sort.Trim();
        var defaultSort = DefaultSort?.Trim();
        var routeValues = ViewContext.HttpContext.Request.Query.ToRouteValueDictionary();

        var active = string.IsNullOrEmpty(defaultSort)
            ? SortEquals(Find(routeValues, options.SortRouteParamName), sort)
            : SortEquals(defaultSort, sort);

        SortOrder? order = null;
        if (DefaultOrder is null)
        {
            if (active && Enum.TryParse<SortOrder>(Find(routeValues, options.OrderRouteParamName), ignoreCase: true, out var routeOrder))
            {
                order = routeOrder;
            }
        }
        else if (SortEquals(defaultSort, sort))
        {
            order = DefaultOrder;
        }

        Remove(routeValues, options.SortRouteParamName);
        Remove(routeValues, options.OrderRouteParamName);
        routeValues[options.SortRouteParamName] = sort;
        routeValues[options.OrderRouteParamName] = (order == SortOrder.Asc ? SortOrder.Desc : SortOrder.Asc).ToString();

        var content = await output.GetChildContentAsync().ConfigureAwait(false);

        var a = new TagBuilder("a");
        AddClass(a, options.LinkCssCLass);
        if (order is not null)
        {
            a.AddCssClass(order.Value.ToString().ToLowerInvariant());
        }
        if (active)
        {
            AddClass(a, options.ActiveCssClass);
        }
        a.Attributes["href"] = CurrentPageUrl.Build(ViewContext, _urlHelperFactory, routeValues);
        a.InnerHtml.AppendHtml(content);

        output.Content.SetHtmlContent(a);
    }

    private static string? Find(RouteValueDictionary values, string key)
        => values.FirstOrDefault(value => string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    private static void Remove(RouteValueDictionary values, string key)
    {
        foreach (var existing in values.Keys.Where(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            values.Remove(existing);
        }
    }

    private static void AddClass(TagBuilder builder, string? cssClass)
    {
        if (!string.IsNullOrWhiteSpace(cssClass))
        {
            builder.AddCssClass(cssClass);
        }
    }

    private static bool SortEquals(string? left, string? right)
        => left is null || right is null
            ? left is null && right is null
            : string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Options of <see cref="SortTagHelper"/>.</summary>
public class SortOptions
{
    /// <summary>The class of the link. Defaults to <c>sort</c>.</summary>
    public string? LinkCssCLass { get; set; } = "sort";

    /// <summary>The class of the link when the list is sorted by it. Defaults to <c>active</c>.</summary>
    public string? ActiveCssClass { get; set; } = "active";

    /// <summary>The query parameter of the sort property. Defaults to <c>sort</c>.</summary>
    public string SortRouteParamName { get; set; } = "sort";

    /// <summary>The query parameter of the order. Defaults to <c>order</c>.</summary>
    public string OrderRouteParamName { get; set; } = "order";
}
