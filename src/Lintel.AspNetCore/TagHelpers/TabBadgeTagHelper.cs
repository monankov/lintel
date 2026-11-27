using System.Globalization;
using Lintel.Primitives;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// A counter badge, for example next to the title of a tab (Bootstrap 5):
/// <c>&lt;tab-badge value="Model.TotalOrders" title="Orders" /&gt;</c> becomes
/// <c>&lt;span class="badge text-bg-secondary ms-1" title="Orders"&gt;1.2K&lt;/span&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// A whole number is shown compactly (<see cref="NumberExtensions.ToCompactString(long, IFormatProvider?)"/>); without
/// a <c>title</c> of its own the badge gets the full number as its title. Other values are shown as they are.
/// </para>
/// <para>
/// A badge with zero or a negative number is hidden (<c>d-none</c>), but still rendered, so that a script can update it;
/// <c>hidden="true"</c> or <c>"false"</c> overrides this. Other attributes and classes of the element are kept.
/// </para>
/// </remarks>
[HtmlTargetElement("tab-badge", TagStructure = TagStructure.WithoutEndTag)]
public class TabBadgeTagHelper : TagHelper
{
    /// <summary>The value.</summary>
    [HtmlAttributeName("value")]
    public object? Value { get; set; }

    /// <summary>Hides (<see langword="true"/>) or shows (<see langword="false"/>) the badge regardless of the value.</summary>
    [HtmlAttributeName("hidden")]
    public bool? Hidden { get; set; }

    /// <summary>The classes of the badge. Default: <c>badge text-bg-secondary ms-1</c>.</summary>
    [HtmlAttributeName("asp-badge-class")]
    public string BadgeCssClass { get; set; } = "badge text-bg-secondary ms-1";

    /// <summary>The class that hides the badge. Default: <c>d-none</c>.</summary>
    [HtmlAttributeName("asp-hidden-class")]
    public string HiddenCssClass { get; set; } = "d-none";

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass(BadgeCssClass);

        long? number = Value switch
        {
            byte or sbyte or short or ushort or int or uint or long => Convert.ToInt64(Value, CultureInfo.InvariantCulture),
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };

        if (Hidden ?? number <= 0)
        {
            output.AddClass(HiddenCssClass);
        }

        if (number is { } value)
        {
            if (!output.Attributes.ContainsName("title"))
            {
                output.Attributes.SetAttribute("title", value.ToString("#,0", CultureInfo.CurrentCulture));
            }
            output.Content.SetContent(value.ToCompactString());
        }
        else
        {
            output.Content.SetContent(Convert.ToString(Value, CultureInfo.CurrentCulture) ?? "");
        }
    }
}
