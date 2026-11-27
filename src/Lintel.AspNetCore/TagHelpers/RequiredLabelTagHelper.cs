using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Lintel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// The marker that <see cref="RequiredLabelTagHelper"/> appends to labels of required fields.
/// Configure with <c>services.Configure&lt;RequiredLabelOptions&gt;(o =&gt; ...)</c>.
/// </summary>
public class RequiredLabelOptions
{
    /// <summary>
    /// The HTML appended inside the label. Default (Bootstrap 5):
    /// <c>&lt;span class="text-danger ms-1" aria-hidden="true"&gt;*&lt;/span&gt;</c>.
    /// </summary>
    public string MarkerHtml { get; set; } = """<span class="text-danger ms-1" aria-hidden="true">*</span>""";
}

/// <summary>
/// Appends a required marker (an asterisk) to <c>&lt;label asp-for="..."&gt;</c> when the property is required:
/// it has <see cref="RequiredAttribute"/>, or a <see cref="RequiredIfAttribute"/> whose condition is met by the current
/// value of the related property. <c>asp-required="true"</c> or <c>"false"</c> overrides the check.
/// </summary>
/// <remarks>
/// <para>
/// Only explicit attributes count: non-nullable properties, which MVC treats as implicitly required, get no marker.
/// <see cref="RegularExpressionIfAttribute"/> checks a format and does not make a property required.
/// </para>
/// <para>
/// The related value is read from the model; when the view has no model (for example a form rendered again after
/// a failed POST without one) it is read from <c>ModelState</c> and converted to the type of the related property.
/// The marker shows the state on the server; it does not change when the user edits the related field.
/// </para>
/// </remarks>
[HtmlTargetElement("label", Attributes = ForAttributeName)]
public class RequiredLabelTagHelper : TagHelper
{
    private const string ForAttributeName = "asp-for";

    private readonly RequiredLabelOptions _options;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="options">The marker options.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public RequiredLabelTagHelper(IOptions<RequiredLabelOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <summary>The model property of the label.</summary>
    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    /// <summary>Forces the marker (<see langword="true"/>) or suppresses it (<see langword="false"/>).</summary>
    [HtmlAttributeName("asp-required")]
    public bool? Required { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (Required ?? IsRequired())
        {
            output.PostContent.AppendHtml(_options.MarkerHtml);
        }
    }

    private bool IsRequired()
    {
        var attributes = For.Metadata.ValidatorMetadata;
        return attributes.OfType<RequiredAttribute>().Any()
            || attributes.OfType<RequiredIfAttribute>()
                .Where(attribute => attribute is not RegularExpressionIfAttribute)
                .Any(attribute => attribute.IsConditionMet(GetRelatedValue(attribute.RelatedProperty)));
    }

    private object? GetRelatedValue(string relatedProperty)
    {
        var container = For.ModelExplorer.Container;
        if (container.Model is not null)
        {
            return container.GetExplorerForProperty(relatedProperty)?.Model;
        }

        var fullName = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        var separator = fullName.LastIndexOf('.');
        var key = separator < 0 ? relatedProperty : fullName[..(separator + 1)] + relatedProperty;
        if (!ViewContext.ViewData.ModelState.TryGetValue(key, out var entry))
        {
            return null;
        }

        var metadata = For.Metadata.ContainerMetadata?.Properties[relatedProperty];
        return Convert(entry.RawValue, metadata);
    }

    private static object? Convert(object? rawValue, ModelMetadata? metadata)
    {
        // A checkbox posts "true" and the hidden "false"; the first value is the one that counts.
        var value = rawValue is string[] values ? values.FirstOrDefault() : rawValue;
        if (value is not string text || metadata is null || metadata.UnderlyingOrModelType == typeof(string))
        {
            return value;
        }
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        try
        {
            return TypeDescriptor.GetConverter(metadata.UnderlyingOrModelType).ConvertFrom(null, CultureInfo.InvariantCulture, text);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
        {
            return text;
        }
    }
}
