using System.Text.Json;
using Lintel.AspNetCore.Client.Alerts;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// How <c>&lt;client-alerts&gt;</c> outputs the alerts.
/// </summary>
public enum ClientAlertsFormat
{
    /// <summary>HTML alert elements (Bootstrap 5 by default, see <see cref="ClientAlertOptions"/>).</summary>
    Html,

    /// <summary>
    /// <c>&lt;script type="application/json"&gt;</c> with <c>[{"type":"success","message":"Saved"}]</c>,
    /// for an application that shows alerts with its own script (for example as toasts).
    /// </summary>
    Json,
}

/// <summary>
/// Shows the alerts of <see cref="IClientAlertProvider"/> once: <c>&lt;client-alerts class="mt-3" /&gt;</c>.
/// Nothing is rendered when there are no alerts.
/// </summary>
/// <remarks>
/// In <see cref="ClientAlertsFormat.Html"/> the element becomes a <c>div</c> (other attributes are kept) with
/// one <c>&lt;div class="alert alert-success alert-dismissible fade show" role="alert"&gt;</c> per alert.
/// Messages are always encoded: as HTML text, or as JSON in which <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> are escaped,
/// so a message cannot close the script element.
/// </remarks>
[HtmlTargetElement("client-alerts", TagStructure = TagStructure.NormalOrSelfClosing)]
public class ClientAlertsTagHelper : TagHelper
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IClientAlertProvider _alerts;
    private readonly ClientAlertOptions _options;

    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="alerts">The alert provider.</param>
    /// <param name="options">The rendering options.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public ClientAlertsTagHelper(IClientAlertProvider alerts, IOptions<ClientAlertOptions> options)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(options);

        _alerts = alerts;
        _options = options.Value;
    }

    /// <summary>The output format. Default: <see cref="ClientAlertsFormat.Html"/>.</summary>
    public ClientAlertsFormat Format { get; set; } = ClientAlertsFormat.Html;

    /// <summary>Whether HTML alerts get a close button. Default: <see langword="true"/>.</summary>
    public bool Dismissible { get; set; } = true;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var alerts = _alerts.GetAllClientAlerts().ToList();
        if (alerts.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagMode = TagMode.StartTagAndEndTag;
        if (Format == ClientAlertsFormat.Json)
        {
            output.TagName = "script";
            output.Attributes.SetAttribute("type", "application/json");
            output.Content.SetHtmlContent(JsonSerializer.Serialize(alerts.Select(a => new { a.Type, a.Message }), _json));
            return;
        }

        output.TagName = "div";
        output.Content.Clear();
        foreach (var alert in alerts)
        {
            output.Content.AppendHtml(CreateAlert(alert));
        }
    }

    private TagBuilder CreateAlert(ClientAlert alert)
    {
        string?[] classes = [_options.CssClass, _options.GetTypeCssClass(alert.Type), Dismissible ? _options.DismissibleCssClass : null];

        var element = new TagBuilder("div");
        element.Attributes["class"] = string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)));
        element.Attributes["role"] = "alert";
        element.InnerHtml.Append(alert.Message);

        if (Dismissible)
        {
            var close = new TagBuilder("button");
            close.AddCssClass("btn-close");
            close.Attributes["type"] = "button";
            close.Attributes["data-bs-dismiss"] = "alert";
            close.Attributes["aria-label"] = _options.CloseButtonLabel;
            element.InnerHtml.AppendHtml(close);
        }

        return element;
    }
}
