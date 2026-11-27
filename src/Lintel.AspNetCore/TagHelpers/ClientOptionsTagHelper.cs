using System.Text.Json;
using Lintel.AspNetCore.Client.Options;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// Renders the options of <see cref="IClientOptionProvider"/> as JSON for client-side scripts:
/// <c>&lt;client-options id="client-options" /&gt;</c> becomes
/// <c>&lt;script id="client-options" type="application/json"&gt;{"orders.index":{"pageSize":20}}&lt;/script&gt;</c>.
/// Nothing is rendered when there are no options.
/// </summary>
/// <remarks>
/// <para>
/// A script reads the data with <c>JSON.parse(document.getElementById("client-options").textContent)</c>; no inline
/// code is executed, so the page works with a Content Security Policy that forbids inline scripts.
/// </para>
/// <para>
/// Keys and option names are written as they were added; properties of nested objects use camelCase.
/// <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> are escaped, so a value cannot close the script element.
/// </para>
/// </remarks>
[HtmlTargetElement("client-options", TagStructure = TagStructure.NormalOrSelfClosing)]
public class ClientOptionsTagHelper : TagHelper
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IClientOptionProvider _options;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="options">The client option provider.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public ClientOptionsTagHelper(IClientOptionProvider options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var options = _options.GetAllClientOptions();
        if (options.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "script";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "application/json");
        output.Content.SetHtmlContent(JsonSerializer.Serialize(options, _json));
    }
}
