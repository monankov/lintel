using System.Globalization;
using Lintel.AspNetCore.Http;
using Lintel.AspNetCore.TagHelpers.Internal;
using Lintel.Collections;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// Renders page links for an <see cref="IPagedEnumerable"/> (Bootstrap markup by default):
/// <c>&lt;pagination asp-options="new PaginationOptions { PagedEnumerable = Model }" /&gt;</c>.
/// </summary>
/// <remarks>Links keep the current query string and replace the page parameter.</remarks>
[HtmlTargetElement("pagination", TagStructure = TagStructure.NormalOrSelfClosing)]
public class PaginationTagHelper : TagHelper
{
    private readonly IUrlHelperFactory _urlHelperFactory;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="urlHelperFactory">The URL helper factory.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="urlHelperFactory"/> is <see langword="null"/>.</exception>
    public PaginationTagHelper(IUrlHelperFactory urlHelperFactory)
    {
        ArgumentNullException.ThrowIfNull(urlHelperFactory);

        _urlHelperFactory = urlHelperFactory;
    }

    /// <summary>The options.</summary>
    [HtmlAttributeName("asp-options")]
    public PaginationOptions? Options { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var options = Options ?? new PaginationOptions();
        var paged = options.PagedEnumerable;
        if (paged is null
            || options.Display == PaginationDisplayMode.Never
            || (options.Display == PaginationDisplayMode.IfNeeded && paged.TotalPages <= 1))
        {
            output.SuppressOutput();
            return;
        }

        var pageUrl = options.PageUrlGenerator ?? (pageNumber => PageUrl(options, pageNumber));

        var firstPageToDisplay = 1;
        var lastPageToDisplay = paged.TotalPages;
        var pageNumbersToDisplay = lastPageToDisplay;

        if (options.MaximumPageNumbersToDisplay is { } max && max > 0 && paged.TotalPages > max)
        {
            firstPageToDisplay = Math.Max(1, paged.PageNumber - (max / 2));
            pageNumbersToDisplay = max;
            lastPageToDisplay = firstPageToDisplay + pageNumbersToDisplay - 1;
            if (lastPageToDisplay > paged.TotalPages)
            {
                firstPageToDisplay = paged.TotalPages - max + 1;
                lastPageToDisplay = paged.TotalPages;
            }
        }

        var items = new List<TagBuilder>();

        if (Shows(options.DisplayLinkToFirstPage, firstPageToDisplay > 1))
        {
            items.Add(PaginationLinks.First(paged, pageUrl, options));
        }
        if (Shows(options.DisplayLinkToPreviousPage, !paged.IsFirstPage))
        {
            items.Add(PaginationLinks.Previous(paged, pageUrl, options));
        }

        if (options.DisplayLinkToIndividualPages)
        {
            if (options.DisplayEllipsesWhenNotShowingAllPageNumbers && firstPageToDisplay > 1)
            {
                items.Add(PaginationLinks.Ellipses(options));
            }

            for (var page = firstPageToDisplay; page <= lastPageToDisplay; page++)
            {
                if (page > firstPageToDisplay && !string.IsNullOrWhiteSpace(options.DelimiterBetweenPageNumbers))
                {
                    items.Add(PaginationLinks.Text(options.DelimiterBetweenPageNumbers));
                }
                items.Add(PaginationLinks.Page(page, paged, pageUrl, options));
            }

            if (options.DisplayEllipsesWhenNotShowingAllPageNumbers && lastPageToDisplay < paged.TotalPages)
            {
                items.Add(PaginationLinks.Ellipses(options));
            }
        }

        if (Shows(options.DisplayLinkToNextPage, !paged.IsLastPage))
        {
            items.Add(PaginationLinks.Next(paged, pageUrl, options));
        }
        if (Shows(options.DisplayLinkToLastPage, lastPageToDisplay < paged.TotalPages))
        {
            items.Add(PaginationLinks.Last(paged, pageUrl, options));
        }

        if (items.Count > 0)
        {
            AddClass(items[0], options.FirstListItemCssClass);
            AddClass(items[^1], options.LastListItemCssClass);
            foreach (var item in items)
            {
                foreach (var cssClass in options.LiElementCssClasses ?? [])
                {
                    AddClass(item, cssClass);
                }
            }
        }

        var ul = new TagBuilder("ul");
        foreach (var cssClass in options.UlElementCssClasses ?? [])
        {
            AddClass(ul, cssClass);
        }
        foreach (var item in items)
        {
            ul.InnerHtml.AppendHtml(item);
        }

        output.TagMode = TagMode.StartTagAndEndTag;
        output.TagName = "div";
        output.Attributes.SetAttribute("class", string.Join(' ', ["pagination-container", .. options.ContainerCssClasses ?? []]));
        output.Content.AppendHtml(ul);
    }

    private static bool Shows(PaginationDisplayMode mode, bool needed)
        => mode == PaginationDisplayMode.Always || (mode == PaginationDisplayMode.IfNeeded && needed);

    private static void AddClass(TagBuilder builder, string? cssClass)
    {
        if (!string.IsNullOrWhiteSpace(cssClass))
        {
            builder.AddCssClass(cssClass);
        }
    }

    private string? PageUrl(PaginationOptions options, int pageNumber)
    {
        var parameter = string.IsNullOrEmpty(options.PageNumberRouteParamName) ? "page" : options.PageNumberRouteParamName;
        var values = ViewContext.HttpContext.Request.Query.ToRouteValueDictionary(parameter, pageNumber);
        return CurrentPageUrl.Build(ViewContext, _urlHelperFactory, values);
    }
}

internal static class PaginationLinks
{
    public static TagBuilder Text(string text)
    {
        var li = new TagBuilder("li");
        li.InnerHtml.Append(text);
        return li;
    }

    public static TagBuilder First(IPagedEnumerable paged, Func<int, string?> pageUrl, PaginationOptions options)
        => Link(options.LinkToFirstPageFormat, 1, !paged.IsFirstPage, pageUrl, options, "pagination-first");

    public static TagBuilder Previous(IPagedEnumerable paged, Func<int, string?> pageUrl, PaginationOptions options)
        => Link(options.LinkToPreviousPageFormat, paged.PageNumber - 1, paged.HasPreviousPage, pageUrl, options, "pagination-previous", rel: "prev");

    public static TagBuilder Next(IPagedEnumerable paged, Func<int, string?> pageUrl, PaginationOptions options)
        => Link(options.LinkToNextPageFormat, paged.PageNumber + 1, paged.HasNextPage, pageUrl, options, "pagination-next", rel: "next");

    public static TagBuilder Last(IPagedEnumerable paged, Func<int, string?> pageUrl, PaginationOptions options)
        => Link(options.LinkToLastPageFormat, paged.TotalPages, !paged.IsLastPage, pageUrl, options, "pagination-last");

    public static TagBuilder Page(int pageNumber, IPagedEnumerable paged, Func<int, string?> pageUrl, PaginationOptions options)
    {
        var a = new TagBuilder("a");
        a.InnerHtml.Append(options.FunctionToDisplayEachPageNumber?.Invoke(pageNumber)
            ?? string.Format(CultureInfo.CurrentCulture, options.LinkToIndividualPageFormat, pageNumber));

        if (pageNumber == paged.PageNumber)
        {
            a.Attributes["aria-current"] = "page";
            return Wrap(a, options, options.LiElementActiveCssClass);
        }

        a.Attributes["href"] = pageUrl(pageNumber);
        return Wrap(a, options);
    }

    public static TagBuilder Ellipses(PaginationOptions options)
    {
        var a = new TagBuilder("a");
        a.InnerHtml.AppendHtml(options.EllipsesFormat);
        return Wrap(a, options, "pagination-ellipses", options.LiElementDisabledCssClass);
    }

    private static TagBuilder Link(string format, int pageNumber, bool enabled, Func<int, string?> pageUrl,
        PaginationOptions options, string cssClass, string? rel = null)
    {
        var a = new TagBuilder("a");
        a.InnerHtml.Append(string.Format(CultureInfo.CurrentCulture, format, pageNumber));
        if (rel is not null)
        {
            a.Attributes["rel"] = rel;
        }

        if (!enabled)
        {
            return Wrap(a, options, cssClass, options.LiElementDisabledCssClass);
        }

        a.Attributes["href"] = pageUrl(pageNumber);
        return Wrap(a, options, cssClass);
    }

    private static TagBuilder Wrap(TagBuilder link, PaginationOptions options, params string?[] cssClasses)
    {
        var li = new TagBuilder("li");
        foreach (var cssClass in cssClasses.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            li.AddCssClass(cssClass!);
        }

        if (options.FunctionToTransformEachPageLink is { } transform)
        {
            return transform(li, link);
        }

        li.InnerHtml.AppendHtml(link);
        return li;
    }
}

/// <summary>When an element of <see cref="PaginationTagHelper"/> is rendered.</summary>
public enum PaginationDisplayMode
{
    /// <summary>Always.</summary>
    Always,

    /// <summary>Never.</summary>
    Never,

    /// <summary>Only when it is useful (for example a link to the first page when it is not visible).</summary>
    IfNeeded,
}

/// <summary>
/// Options of <see cref="PaginationTagHelper"/>. The defaults produce Bootstrap 4/5 markup.
/// </summary>
public class PaginationOptions
{
    /// <summary>The page to render links for.</summary>
    public IPagedEnumerable? PagedEnumerable { get; set; }

    /// <summary>Builds the URL of a page; by default the current URL with the page parameter replaced.</summary>
    public Func<int, string?>? PageUrlGenerator { get; set; }

    /// <summary>Extra classes of the container <c>div</c> (it always has <c>pagination-container</c>).</summary>
    public IEnumerable<string>? ContainerCssClasses { get; set; }

    /// <summary>Classes of the <c>ul</c>. Defaults to <c>pagination</c>.</summary>
    public IEnumerable<string>? UlElementCssClasses { get; set; } = ["pagination"];

    /// <summary>Classes of every <c>li</c>. Defaults to <c>page-item</c>.</summary>
    public IEnumerable<string>? LiElementCssClasses { get; set; } = ["page-item"];

    /// <summary>The class of the first <c>li</c>. Defaults to <c>first</c>.</summary>
    public string? FirstListItemCssClass { get; set; } = "first";

    /// <summary>The class of the last <c>li</c>. Defaults to <c>last</c>.</summary>
    public string? LastListItemCssClass { get; set; } = "last";

    /// <summary>The class of the current page <c>li</c>. Defaults to <c>active</c>.</summary>
    public string? LiElementActiveCssClass { get; set; } = "active";

    /// <summary>The class of disabled links. Defaults to <c>disabled</c>.</summary>
    public string? LiElementDisabledCssClass { get; set; } = "disabled";

    /// <summary>When the whole pagination is rendered. Defaults to <see cref="PaginationDisplayMode.Always"/>.</summary>
    public PaginationDisplayMode Display { get; set; }

    /// <summary>When the link to the first page is rendered. Defaults to <see cref="PaginationDisplayMode.IfNeeded"/>.</summary>
    public PaginationDisplayMode DisplayLinkToFirstPage { get; set; } = PaginationDisplayMode.IfNeeded;

    /// <summary>When the link to the last page is rendered. Defaults to <see cref="PaginationDisplayMode.IfNeeded"/>.</summary>
    public PaginationDisplayMode DisplayLinkToLastPage { get; set; } = PaginationDisplayMode.IfNeeded;

    /// <summary>When the link to the previous page is rendered. Defaults to <see cref="PaginationDisplayMode.Never"/>.</summary>
    public PaginationDisplayMode DisplayLinkToPreviousPage { get; set; } = PaginationDisplayMode.Never;

    /// <summary>When the link to the next page is rendered. Defaults to <see cref="PaginationDisplayMode.Never"/>.</summary>
    public PaginationDisplayMode DisplayLinkToNextPage { get; set; } = PaginationDisplayMode.Never;

    /// <summary>Whether page numbers are rendered. Defaults to <see langword="true"/>.</summary>
    public bool DisplayLinkToIndividualPages { get; set; } = true;

    /// <summary>The maximum number of page numbers; <see langword="null"/> for all. Defaults to 3.</summary>
    public int? MaximumPageNumbersToDisplay { get; set; } = 3;

    /// <summary>Whether ellipses replace hidden page numbers. Defaults to <see langword="true"/>.</summary>
    public bool DisplayEllipsesWhenNotShowingAllPageNumbers { get; set; } = true;

    /// <summary>The HTML of the ellipses (not encoded). Defaults to <c>…</c>.</summary>
    public string EllipsesFormat { get; set; } = "…";

    /// <summary>The text of the first page link; <c>{0}</c> is the page number. Defaults to <c>««</c>.</summary>
    public string LinkToFirstPageFormat { get; set; } = "««";

    /// <summary>The text of the previous page link. Defaults to <c>«</c>.</summary>
    public string LinkToPreviousPageFormat { get; set; } = "«";

    /// <summary>The text of page number links. Defaults to <c>{0}</c>.</summary>
    public string LinkToIndividualPageFormat { get; set; } = "{0}";

    /// <summary>The text of the next page link. Defaults to <c>»</c>.</summary>
    public string LinkToNextPageFormat { get; set; } = "»";

    /// <summary>The text of the last page link. Defaults to <c>»»</c>.</summary>
    public string LinkToLastPageFormat { get; set; } = "»»";

    /// <summary>Formats page numbers instead of <see cref="LinkToIndividualPageFormat"/>.</summary>
    public Func<int, string>? FunctionToDisplayEachPageNumber { get; set; }

    /// <summary>Text between page numbers.</summary>
    public string? DelimiterBetweenPageNumbers { get; set; }

    /// <summary>
    /// Combines a <c>li</c> and its link. By default adds <c>page-link</c> to the link (Bootstrap).
    /// </summary>
    public Func<TagBuilder, TagBuilder, TagBuilder>? FunctionToTransformEachPageLink { get; set; } = (li, link) =>
    {
        link.AddCssClass("page-link");
        li.InnerHtml.AppendHtml(link);
        return li;
    };

    /// <summary>The query parameter of the page number. Defaults to <c>page</c>.</summary>
    public string PageNumberRouteParamName { get; set; } = "page";
}
