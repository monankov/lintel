using Lintel.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Lintel.AspNetCore.Referrer;

/// <summary>
/// Names used by the "back" link pattern: <see cref="ReferrerTagHelper"/> stores the page to return to in a hidden field,
/// and <see cref="ReferrerControllerExtensions.RedirectToReferrer"/> redirects there after the form is posted.
/// </summary>
public static class ReferrerDefaults
{
    /// <summary>The name of the hidden form field.</summary>
    public const string Key = "__Referrer";
}

/// <summary>
/// Reading the referrer stored by <see cref="ReferrerTagHelper"/>.
/// </summary>
public static class ReferrerHttpRequestExtensions
{
    /// <summary>
    /// Returns the referrer posted in the <see cref="ReferrerDefaults.Key"/> form field if it is a local URL,
    /// otherwise <paramref name="defaultReferrer"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="defaultReferrer">The URL to use without a valid posted referrer.</param>
    /// <returns>The referrer.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="request"/> is <see langword="null"/>.</exception>
    public static string? GetReferrer(this HttpRequest request, string? defaultReferrer)
    {
        ArgumentNullException.ThrowIfNull(request);

        var posted = request.HasFormContentType ? request.Form[ReferrerDefaults.Key].ToString() : null;
        return request.IsLocalUrl(posted) ? posted : defaultReferrer;
    }
}

/// <summary>
/// Redirects back to the referrer stored by <see cref="ReferrerTagHelper"/>.
/// </summary>
public static class ReferrerControllerExtensions
{
    /// <summary>
    /// Redirects to the posted referrer if it is a local URL, otherwise to <paramref name="url"/>.
    /// </summary>
    /// <param name="controller">The controller.</param>
    /// <param name="url">The fallback URL.</param>
    /// <returns>The redirect.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="controller"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="url"/> is empty.</exception>
    public static RedirectResult RedirectToReferrer(this Controller controller, string url)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentException.ThrowIfNullOrEmpty(url);

        return new RedirectResult(controller.HttpContext.Request.GetReferrer(url)!);
    }
}

/// <summary>
/// Turns <c>&lt;a asp-referrer="/orders"&gt;Back&lt;/a&gt;</c> into a link to the previous page of this application
/// (the posted referrer, the <c>Referer</c> header, or the given fallback) and stores it in a hidden field,
/// so that the form keeps it across post-backs.
/// </summary>
/// <remarks>Only local URLs are used; external referrers are ignored.</remarks>
[HtmlTargetElement("a", Attributes = "asp-referrer")]
public class ReferrerTagHelper : TagHelper
{
    /// <summary>The fallback URL.</summary>
    [HtmlAttributeName("asp-referrer")]
    public string? Referrer { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var request = ViewContext.HttpContext.Request;

        var httpReferrer = request.Headers.Referer.ToString();
        if (!request.IsLocalUrl(httpReferrer)
            || string.Equals(httpReferrer, request.GetAbsoluteUri(), StringComparison.OrdinalIgnoreCase))
        {
            httpReferrer = null;
        }

        var referrer = request.GetReferrer(httpReferrer ?? Referrer) ?? "";

        output.Attributes.SetAttribute("href", referrer);

        var hidden = new TagBuilder("input") { TagRenderMode = TagRenderMode.StartTag };
        hidden.Attributes["type"] = "hidden";
        hidden.Attributes["name"] = ReferrerDefaults.Key;
        hidden.Attributes["value"] = referrer;
        output.PostElement.AppendHtml(hidden);
    }
}
