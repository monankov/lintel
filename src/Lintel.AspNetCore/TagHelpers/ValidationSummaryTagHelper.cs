using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// The classes of <c>&lt;validation-summary&gt;</c>. The defaults are Bootstrap 5.
/// Configure with <c>services.Configure&lt;ValidationSummaryOptions&gt;(o =&gt; ...)</c>.
/// </summary>
public class ValidationSummaryOptions
{
    /// <summary>The class of the container. Default: <c>alert alert-danger</c>.</summary>
    public string? CssClass { get; set; } = "alert alert-danger";

    /// <summary>The class of the error list. Default: <c>mb-0</c> (no margin below the list inside the alert).</summary>
    public string? ListCssClass { get; set; } = "mb-0";
}

/// <summary>
/// Lists the errors of <c>ModelState</c>: <c>&lt;validation-summary /&gt;</c> becomes
/// <c>&lt;div class="alert alert-danger" role="alert"&gt;&lt;ul class="mb-0"&gt;&lt;li&gt;...&lt;/li&gt;&lt;/ul&gt;&lt;/div&gt;</c>.
/// Nothing is rendered when there are no errors to show.
/// </summary>
/// <remarks>
/// <para>
/// By default only errors of the form itself are shown (added with an empty key, or with the
/// <c>HtmlFieldPrefix</c> of a partial view); <c>include-property-errors="true"</c> adds the errors of fields after them.
/// Unlike <c>asp-validation-summary</c>, an empty box is never rendered, and the summary is not updated by client-side
/// validation.
/// </para>
/// <para>Messages are HTML-encoded; line breaks in a message become <c>&lt;br&gt;</c>. Other attributes are kept.</para>
/// </remarks>
[HtmlTargetElement("validation-summary", TagStructure = TagStructure.NormalOrSelfClosing)]
public class ValidationSummaryTagHelper : TagHelper
{
    private static readonly string[] _lineBreaks = ["\r\n", "\n", "\r"];

    private readonly ValidationSummaryOptions _options;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="options">The classes.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public ValidationSummaryTagHelper(IOptions<ValidationSummaryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <summary>Whether errors of fields are shown too. Default: <see langword="false"/>.</summary>
    public bool IncludePropertyErrors { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var messages = GetMessages().ToList();
        if (messages.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass(_options.CssClass);
        output.Attributes.SetAttribute("role", "alert");

        var list = new TagBuilder("ul");
        if (!string.IsNullOrWhiteSpace(_options.ListCssClass))
        {
            list.AddCssClass(_options.ListCssClass);
        }
        foreach (var message in messages)
        {
            var item = new TagBuilder("li");
            var lines = message.Split(_lineBreaks, StringSplitOptions.None);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    item.InnerHtml.AppendHtml("<br>");
                }
                item.InnerHtml.Append(lines[i]);
            }
            list.InnerHtml.AppendHtml(item);
        }
        output.Content.SetHtmlContent(list);
    }

    private IEnumerable<string> GetMessages()
    {
        var modelState = ViewContext.ViewData.ModelState;
        var formKey = ViewContext.ViewData.TemplateInfo.HtmlFieldPrefix;

        // Errors of the form itself first, then (when requested) errors of fields in ModelState order.
        var entries = modelState
            .Where(entry => IncludePropertyErrors || entry.Key == formKey)
            .OrderBy(entry => entry.Key == formKey ? 0 : 1);

        return entries
            .SelectMany(entry => entry.Value?.Errors ?? [])
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message));
    }
}
