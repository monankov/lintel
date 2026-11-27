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
/// Renders a summary of a page: <c>&lt;page-info asp-options="new PageInfoOptions { PagedEnumerable = Model }" /&gt;</c>.
/// </summary>
[HtmlTargetElement("page-info", TagStructure = TagStructure.NormalOrSelfClosing)]
public class PageInfoTagHelper : TagHelper
{
    /// <summary>The options.</summary>
    [HtmlAttributeName("asp-options")]
    public PageInfoOptions? Options { get; set; }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var options = Options ?? new PageInfoOptions();
        if (options.PagedEnumerable is not { } paged)
        {
            output.SuppressOutput();
            return;
        }

        var text = string.Format(CultureInfo.CurrentCulture, options.Format,
            paged.PageNumber,
            paged.TotalPages,
            paged.TotalItems > 0 ? paged.FirstItemOnPage : 0,
            paged.LastItemOnPage,
            paged.TotalItems);

        output.TagMode = TagMode.StartTagAndEndTag;
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "page-info");
        output.Content.Append(text);
    }
}

/// <summary>Options of <see cref="PageInfoTagHelper"/>.</summary>
public class PageInfoOptions
{
    /// <summary>The page.</summary>
    public IPagedEnumerable? PagedEnumerable { get; set; }

    /// <summary>
    /// The text: <c>{0}</c> page number, <c>{1}</c> total pages, <c>{2}</c> first item, <c>{3}</c> last item, <c>{4}</c> total items.
    /// </summary>
    public string Format { get; set; } = "Page {0} of {1}. Showing items {2} through {3} of {4}.";
}

/// <summary>
/// Renders a dropdown to choose the page size (Bootstrap 5 markup):
/// <c>&lt;page-size asp-options="new PageSizeOptions { ItemsPerPage = [10, 20, 0], DefaultPageSize = Model.PageSize }" /&gt;</c>.
/// </summary>
/// <remarks>
/// A value of 0 or less in <see cref="PageSizeOptions.ItemsPerPage"/> adds the "all items" link after a divider.
/// Links keep the query of the current page with the new size and without the page number
/// (<see cref="PageSizeOptions.PageNumberRouteParamName"/>), so the list starts again from the first page.
/// The item of <see cref="PageSizeOptions.DefaultPageSize"/> (the current size) is marked <c>active</c>.
/// </remarks>
[HtmlTargetElement("page-size", TagStructure = TagStructure.NormalOrSelfClosing)]
public class PageSizeTagHelper : TagHelper
{
    private readonly IUrlHelperFactory _urlHelperFactory;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="urlHelperFactory">The URL helper factory.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="urlHelperFactory"/> is <see langword="null"/>.</exception>
    public PageSizeTagHelper(IUrlHelperFactory urlHelperFactory)
    {
        ArgumentNullException.ThrowIfNull(urlHelperFactory);

        _urlHelperFactory = urlHelperFactory;
    }

    /// <summary>The options.</summary>
    [HtmlAttributeName("asp-options")]
    public PageSizeOptions? Options { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var options = Options ?? new PageSizeOptions();
        var sizes = options.ItemsPerPage?.ToList() ?? [];
        if (sizes.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        var parameter = string.IsNullOrEmpty(options.PageSizeRouteParamName) ? "pagesize" : options.PageSizeRouteParamName;
        var allItems = string.IsNullOrEmpty(options.AllItemsFormat) ? "All" : options.AllItemsFormat;

        var menu = new TagBuilder("div");
        menu.Attributes["class"] = options.DropdownMenuAlign == PageSizeDropdownMenuAlign.Left
            ? "dropdown-menu dropdown-menu-start"
            : "dropdown-menu dropdown-menu-end";

        foreach (var size in sizes.Where(size => size > 0).Distinct())
        {
            menu.InnerHtml.AppendHtml(Item(size.ToString(CultureInfo.CurrentCulture), Url(options, parameter, size), size == options.DefaultPageSize));
        }
        if (sizes.Any(size => size <= 0))
        {
            if (sizes.Any(size => size > 0))
            {
                var divider = new TagBuilder("div");
                divider.AddCssClass("dropdown-divider");
                menu.InnerHtml.AppendHtml(divider);
            }
            menu.InnerHtml.AppendHtml(Item(allItems, Url(options, parameter, 0), options.DefaultPageSize <= 0));
        }

        var toggle = new TagBuilder("button");
        toggle.AddCssClass("dropdown-toggle");
        if (!string.IsNullOrEmpty(options.DropdownToggleCssClass))
        {
            toggle.AddCssClass(options.DropdownToggleCssClass);
        }
        toggle.Attributes["type"] = "button";
        if (!string.IsNullOrEmpty(options.DropdownToggleAttribute))
        {
            toggle.Attributes[options.DropdownToggleAttribute] = "dropdown";
        }
        toggle.InnerHtml.Append(options.DefaultPageSize > 0 ? options.DefaultPageSize.ToString(CultureInfo.CurrentCulture) : allItems);

        output.TagMode = TagMode.StartTagAndEndTag;
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "page-size dropdown dropup" + (string.IsNullOrEmpty(options.DropdownCssClass) ? "" : " " + options.DropdownCssClass));
        output.Content.AppendHtml(toggle);
        output.Content.AppendHtml(menu);
    }

    private string? Url(PageSizeOptions options, string parameter, int size)
    {
        var values = ViewContext.HttpContext.Request.Query.ToRouteValueDictionary(parameter, size);
        if (!string.IsNullOrEmpty(options.PageNumberRouteParamName))
        {
            values.Remove(options.PageNumberRouteParamName);
        }
        return CurrentPageUrl.Build(ViewContext, _urlHelperFactory, values);
    }

    private static TagBuilder Item(string text, string? url, bool active)
    {
        var item = new TagBuilder("a");
        item.Attributes["class"] = active ? "dropdown-item active" : "dropdown-item";
        item.Attributes["href"] = url;
        if (active)
        {
            item.Attributes["aria-current"] = "true";
        }
        item.InnerHtml.Append(text);
        return item;
    }
}

/// <summary>Alignment of the dropdown menu of <see cref="PageSizeTagHelper"/>.</summary>
public enum PageSizeDropdownMenuAlign
{
    /// <summary>Aligned to the start of the button (<c>dropdown-menu-start</c>).</summary>
    Left,

    /// <summary>Aligned to the end of the button (<c>dropdown-menu-end</c>).</summary>
    Right,
}

/// <summary>Options of <see cref="PageSizeTagHelper"/>.</summary>
public class PageSizeOptions
{
    /// <summary>The text of the "all items" link. Defaults to <c>All</c>.</summary>
    public string AllItemsFormat { get; set; } = "All";

    /// <summary>The alignment of the menu. Defaults to <see cref="PageSizeDropdownMenuAlign.Right"/>.</summary>
    public PageSizeDropdownMenuAlign DropdownMenuAlign { get; set; } = PageSizeDropdownMenuAlign.Right;

    /// <summary>Not used; kept for compatibility with Structr.</summary>
    public string? ContainerCssClass { get; set; }

    /// <summary>An extra class of the dropdown.</summary>
    public string? DropdownCssClass { get; set; }

    /// <summary>The class of the toggle button. Defaults to <c>btn btn-secondary</c>.</summary>
    public string? DropdownToggleCssClass { get; set; } = "btn btn-secondary";

    /// <summary>
    /// The attribute that makes the button a dropdown toggle. Defaults to <c>data-bs-toggle</c> (Bootstrap 5);
    /// use <c>data-toggle</c> for Bootstrap 4.
    /// </summary>
    public string? DropdownToggleAttribute { get; set; } = "data-bs-toggle";

    /// <summary>The page sizes; 0 or less for "all items".</summary>
    public IEnumerable<int>? ItemsPerPage { get; set; }

    /// <summary>The query parameter of the page size. Defaults to <c>pagesize</c>.</summary>
    public string PageSizeRouteParamName { get; set; } = "pagesize";

    /// <summary>
    /// The query parameter of the page number, removed from the links so that a new size starts from the first page.
    /// Defaults to <c>page</c>; <see langword="null"/> keeps it.
    /// </summary>
    public string? PageNumberRouteParamName { get; set; } = "page";

    /// <summary>The page size shown on the button; 0 or less for "all items".</summary>
    public int DefaultPageSize { get; set; }
}
