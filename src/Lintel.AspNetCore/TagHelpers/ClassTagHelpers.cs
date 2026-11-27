using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using MvcTagHelperOutputExtensions = Microsoft.AspNetCore.Mvc.TagHelpers.TagHelperOutputExtensions;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// Adding and removing CSS classes with the default HTML encoder.
/// </summary>
public static class TagHelperOutputExtensions
{
    private static readonly HtmlEncoder _encoder = HtmlEncoder.Default;

    /// <summary>Adds CSS classes separated by spaces (<c>"alert alert-danger"</c>); an empty value is ignored.</summary>
    /// <param name="output">The tag helper output.</param>
    /// <param name="classValue">The class or classes.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="output"/> is <see langword="null"/>.</exception>
    public static void AddClass(this TagHelperOutput output, string? classValue)
    {
        ArgumentNullException.ThrowIfNull(output);

        foreach (var name in Split(classValue))
        {
            MvcTagHelperOutputExtensions.AddClass(output, name, _encoder);
        }
    }

    /// <summary>Removes CSS classes separated by spaces; an empty value is ignored.</summary>
    /// <param name="output">The tag helper output.</param>
    /// <param name="classValue">The class or classes.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="output"/> is <see langword="null"/>.</exception>
    public static void RemoveClass(this TagHelperOutput output, string? classValue)
    {
        ArgumentNullException.ThrowIfNull(output);

        foreach (var name in Split(classValue))
        {
            MvcTagHelperOutputExtensions.RemoveClass(output, name, _encoder);
        }
    }

    // MVC accepts one class per call and throws on a value with spaces.
    private static string[] Split(string? classValue)
        => classValue?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
}

/// <summary>
/// Adds a CSS class to the element when the condition is true:
/// <c>&lt;li asp-append-if="Model.IsActive" asp-append-class="active"&gt;</c>.
/// </summary>
[HtmlTargetElement(Attributes = "asp-append-if, asp-append-class")]
public class AppendClassTagHelper : TagHelper
{
    /// <summary>The condition.</summary>
    [HtmlAttributeName("asp-append-if")]
    public bool Append { get; set; }

    /// <summary>The class to add.</summary>
    [HtmlAttributeName("asp-append-class")]
    public string? Class { get; set; }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Append)
        {
            output.AddClass(Class);
        }
    }
}

/// <summary>
/// Adds <c>asp-match-class</c> to a link that points to the current page (menus):
/// <c>&lt;a asp-controller="Orders" asp-action="Index" asp-match-class="active"&gt;</c>.
/// </summary>
/// <remarks>
/// Compares area, controller, action, Razor page and the given <c>asp-route-*</c> values (with route and query values
/// of the request) case-insensitively. Attributes that are not set are not compared.
/// </remarks>
[HtmlTargetElement("a", Attributes = "asp-match-class")]
public class AnchorMatchTagHelper : AnchorTagHelper
{
    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="generator">The HTML generator.</param>
    public AnchorMatchTagHelper(IHtmlGenerator generator)
        : base(generator)
    {
    }

    /// <summary>The class to add when the link matches the current page.</summary>
    [HtmlAttributeName("asp-match-class")]
    public string? MatchClass { get; set; }

    /// <inheritdoc />
    /// <remarks>Runs after <see cref="AnchorTagHelper"/>, which generates the <c>href</c>.</remarks>
    public override int Order => base.Order + 1;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Matches())
        {
            output.AddClass(MatchClass);
        }
    }

    private bool Matches()
    {
        var route = ViewContext.RouteData.Values;
        return Same(Area, route["area"])
            && Same(Controller, route["controller"])
            && Same(Action, route["action"])
            && Same(Page, route["page"])
            && RouteValuesMatch();
    }

    private static bool Same(string? expected, object? actual)
        => string.IsNullOrEmpty(expected) || string.Equals(expected, actual?.ToString(), StringComparison.OrdinalIgnoreCase);

    private bool RouteValuesMatch()
    {
        if (RouteValues.Count == 0)
        {
            return true;
        }

        var route = ViewContext.RouteData.Values;
        var query = ViewContext.HttpContext.Request.Query;

        return RouteValues.All(expected =>
        {
            var actual = route.TryGetValue(expected.Key, out var routeValue) ? routeValue?.ToString()
                : query.TryGetValue(expected.Key, out var queryValue) ? queryValue.ToString()
                : null;
            return actual is not null && string.Equals(actual, expected.Value, StringComparison.OrdinalIgnoreCase);
        });
    }
}
