using Lintel.AspNetCore.Navigation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// Renders the path to the current page (Bootstrap 5):
/// <c>@inject IBreadcrumbNavigation&lt;RouteBreadcrumb&gt; Breadcrumbs</c> and <c>&lt;breadcrumbs asp-items="Breadcrumbs" /&gt;</c> become
/// <c>&lt;nav aria-label="breadcrumb"&gt;&lt;ol class="breadcrumb"&gt;&lt;li class="breadcrumb-item"&gt;&lt;a href="..."&gt;...&lt;/a&gt;&lt;/li&gt;
/// ...&lt;li class="breadcrumb-item active" aria-current="page"&gt;...&lt;/li&gt;&lt;/ol&gt;&lt;/nav&gt;</c>.
/// Nothing is rendered when there are no items.
/// </summary>
/// <remarks>
/// Links are built by <see cref="RouteBreadcrumbMatcher.GetLinkValues"/>; an item without an action or a page, and the active
/// item, are rendered as text. Titles are HTML-encoded. Attributes of the element (for example <c>aria-label</c>) are kept.
/// </remarks>
[HtmlTargetElement("breadcrumbs", TagStructure = TagStructure.NormalOrSelfClosing)]
public class BreadcrumbsTagHelper : TagHelper
{
    private readonly IUrlHelperFactory _urlHelperFactory;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="urlHelperFactory">The URL helper factory.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="urlHelperFactory"/> is <see langword="null"/>.</exception>
    public BreadcrumbsTagHelper(IUrlHelperFactory urlHelperFactory)
    {
        ArgumentNullException.ThrowIfNull(urlHelperFactory);

        _urlHelperFactory = urlHelperFactory;
    }

    /// <summary>The breadcrumbs from the first to the current one (usually <c>IBreadcrumbNavigation&lt;T&gt;</c>).</summary>
    [HtmlAttributeName("asp-items")]
    public IEnumerable<IRouteBreadcrumb>? Items { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var items = Items?.ToList() ?? [];
        if (items.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "nav";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (!output.Attributes.ContainsName("aria-label"))
        {
            output.Attributes.SetAttribute("aria-label", "breadcrumb");
        }

        var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);
        var list = new TagBuilder("ol");
        list.AddCssClass("breadcrumb");
        foreach (var item in items)
        {
            list.InnerHtml.AppendHtml(CreateItem(item, urlHelper));
        }
        output.Content.SetHtmlContent(list);
    }

    private TagBuilder CreateItem(IRouteBreadcrumb item, IUrlHelper urlHelper)
    {
        var element = new TagBuilder("li");
        element.Attributes["class"] = item.IsActive ? "breadcrumb-item active" : "breadcrumb-item";
        if (item.IsActive)
        {
            element.Attributes["aria-current"] = "page";
        }

        var url = !item.IsActive && RouteBreadcrumbMatcher.HasLink(item) ? GetUrl(item, urlHelper) : null;
        if (url is null)
        {
            element.InnerHtml.Append(item.Title ?? "");
            return element;
        }

        var link = new TagBuilder("a");
        link.Attributes["href"] = url;
        link.InnerHtml.Append(item.Title ?? "");
        element.InnerHtml.AppendHtml(link);
        return element;
    }

    private string? GetUrl(IRouteBreadcrumb item, IUrlHelper urlHelper)
    {
        var values = RouteBreadcrumbMatcher.GetLinkValues(item, ViewContext.HttpContext);
        if (!string.IsNullOrEmpty(item.Page))
        {
            values.Remove("page");
            return urlHelper.Page(item.Page, values);
        }

        var action = values["action"]?.ToString();
        var controller = values["controller"]?.ToString();
        values.Remove("action");
        values.Remove("controller");
        return urlHelper.Action(new UrlActionContext { Action = action, Controller = controller, Values = values });
    }
}
