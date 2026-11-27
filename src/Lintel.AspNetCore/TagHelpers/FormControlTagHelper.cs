using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// The classes that <see cref="FormControlTagHelper"/> adds. The defaults are Bootstrap 5.
/// Configure with <c>services.Configure&lt;FormControlOptions&gt;(o =&gt; ...)</c>; an empty value adds nothing.
/// </summary>
public class FormControlOptions
{
    /// <summary>Text-like inputs (text, email, date, number, file, ...) and textarea. Default: <c>form-control</c>.</summary>
    public string? ControlCssClass { get; set; } = "form-control";

    /// <summary><c>select</c>. Default: <c>form-select</c>.</summary>
    public string? SelectCssClass { get; set; } = "form-select";

    /// <summary>Checkboxes and radio buttons. Default: <c>form-check-input</c>.</summary>
    public string? CheckCssClass { get; set; } = "form-check-input";

    /// <summary><c>input type="range"</c>. Default: <c>form-range</c>.</summary>
    public string? RangeCssClass { get; set; } = "form-range";

    /// <summary>Added when the field has a validation error. Default: <c>is-invalid</c>.</summary>
    public string? InvalidCssClass { get; set; } = "is-invalid";
}

/// <summary>
/// Adds form classes to <c>input</c>, <c>textarea</c> and <c>select</c> with <c>asp-for</c>, by element and input type,
/// and the invalid class when <c>ModelState</c> has an error for the field. <c>asp-form-control="false"</c> turns it off.
/// </summary>
/// <remarks>
/// <para>
/// Hidden inputs and buttons (<c>submit</c>, <c>reset</c>, <c>button</c>, <c>image</c>) get no classes. The input type is
/// the one written by <c>InputTagHelper</c> (for example <c>checkbox</c> for a <see cref="bool"/>), because this tag
/// helper runs after it (<see cref="Order"/> is 1000).
/// </para>
/// <para>
/// The error is looked up by the full field name, so fields of partial views and editor templates with
/// a <c>HtmlFieldPrefix</c> are highlighted too. Client-side validation marks fields with its own class
/// (<c>input-validation-error</c> for jQuery Validation Unobtrusive); style it or configure the validator accordingly.
/// </para>
/// </remarks>
[HtmlTargetElement("input", Attributes = ForAttributeName)]
[HtmlTargetElement("textarea", Attributes = ForAttributeName)]
[HtmlTargetElement("select", Attributes = ForAttributeName)]
public class FormControlTagHelper : TagHelper
{
    private const string ForAttributeName = "asp-for";

    private static readonly HashSet<string> _noClassTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "hidden", "submit", "reset", "button", "image",
    };

    private readonly FormControlOptions _options;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="options">The classes.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public FormControlTagHelper(IOptions<FormControlOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <inheritdoc />
    public override int Order => 1000;

    /// <summary>The model property of the field.</summary>
    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    /// <summary>Whether classes are added. Default: <see langword="true"/>.</summary>
    [HtmlAttributeName("asp-form-control")]
    public bool Enabled { get; set; } = true;

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!Enabled)
        {
            return;
        }

        var type = output.Attributes.TryGetAttribute("type", out var attribute) ? attribute.Value?.ToString() : null;
        if (type is not null && _noClassTypes.Contains(type))
        {
            return;
        }

        output.AddClass(GetCssClass(output.TagName, type));
        if (HasError())
        {
            output.AddClass(_options.InvalidCssClass);
        }
    }

    private string? GetCssClass(string? tagName, string? type)
    {
        if (string.Equals(tagName, "select", StringComparison.OrdinalIgnoreCase))
        {
            return _options.SelectCssClass;
        }

        return type?.ToLowerInvariant() switch
        {
            "checkbox" or "radio" => _options.CheckCssClass,
            "range" => _options.RangeCssClass,
            _ => _options.ControlCssClass,
        };
    }

    private bool HasError()
    {
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        return ViewContext.ViewData.ModelState.TryGetValue(name, out var entry) && entry.Errors.Count > 0;
    }
}
