using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.TagHelpers;

/// <summary>
/// The classes of <c>&lt;checkbox&gt;</c> and <c>&lt;radio&gt;</c>. The defaults are Bootstrap 5.
/// Configure with <c>services.Configure&lt;FormCheckOptions&gt;(o =&gt; ...)</c>.
/// </summary>
public class FormCheckOptions
{
    /// <summary>The container. Default: <c>form-check</c>.</summary>
    public string? ContainerCssClass { get; set; } = "form-check";

    /// <summary>Added to the container with <c>asp-inline="true"</c>. Default: <c>form-check-inline</c>.</summary>
    public string? InlineCssClass { get; set; } = "form-check-inline";

    /// <summary>Added to the container with <c>asp-switch="true"</c>. Default: <c>form-switch</c>.</summary>
    public string? SwitchCssClass { get; set; } = "form-switch";

    /// <summary>The input. Default: <c>form-check-input</c>.</summary>
    public string? InputCssClass { get; set; } = "form-check-input";

    /// <summary>The label. Default: <c>form-check-label</c>.</summary>
    public string? LabelCssClass { get; set; } = "form-check-label";
}

/// <summary>
/// The common part of <see cref="CheckboxTagHelper"/> and <see cref="RadioTagHelper"/>: a <c>div.form-check</c> with the
/// input and its label.
/// </summary>
public abstract class FormCheckTagHelper : TagHelper
{
    /// <summary>
    /// Initializes the tag helper.
    /// </summary>
    /// <param name="generator">The HTML generator of MVC.</param>
    /// <param name="options">The classes.</param>
    /// <param name="requiredLabelOptions">The required marker (see <see cref="RequiredLabelTagHelper"/>).</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    protected FormCheckTagHelper(IHtmlGenerator generator, IOptions<FormCheckOptions> options, IOptions<RequiredLabelOptions> requiredLabelOptions)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(requiredLabelOptions);

        Generator = generator;
        Options = options.Value;
        RequiredLabelOptions = requiredLabelOptions.Value;
    }

    /// <summary>The HTML generator.</summary>
    protected IHtmlGenerator Generator { get; }

    /// <summary>The classes.</summary>
    protected FormCheckOptions Options { get; }

    /// <summary>The required marker.</summary>
    protected RequiredLabelOptions RequiredLabelOptions { get; }

    /// <summary>The model property.</summary>
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = default!;

    /// <summary>The label; by default the display name of the property. An empty string renders no label.</summary>
    [HtmlAttributeName("asp-label")]
    public string? Label { get; set; }

    /// <summary>Whether the item is placed in a row with others (<see cref="FormCheckOptions.InlineCssClass"/>).</summary>
    [HtmlAttributeName("asp-inline")]
    public bool Inline { get; set; }

    /// <summary>
    /// The <c>id</c> of an input written as child content, for the <c>for</c> attribute of the label.
    /// </summary>
    [HtmlAttributeName("asp-input-id")]
    public string? InputId { get; set; }

    /// <summary>The view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass(Options.ContainerCssClass);
        if (Inline)
        {
            output.AddClass(Options.InlineCssClass);
        }
        AddContainerClasses(output);

        var child = await output.GetChildContentAsync();
        string? inputId;
        if (child.IsEmptyOrWhiteSpace)
        {
            var input = GenerateInput();
            if (!string.IsNullOrWhiteSpace(Options.InputCssClass))
            {
                input.AddCssClass(Options.InputCssClass);
            }
            inputId = input.Attributes.TryGetValue("id", out var id) ? id : null;
            output.Content.AppendHtml(input);
            AppendAfterInput(output);
        }
        else
        {
            inputId = InputId;
            output.Content.AppendHtml(child);
        }

        var text = Label ?? For.Metadata.GetDisplayName();
        if (text.Length == 0)
        {
            return;
        }

        var label = new TagBuilder("label");
        if (!string.IsNullOrWhiteSpace(Options.LabelCssClass))
        {
            label.Attributes["class"] = Options.LabelCssClass;
        }
        if (!string.IsNullOrEmpty(inputId))
        {
            label.Attributes["for"] = inputId;
        }
        label.InnerHtml.Append(text);
        if (IsRequired())
        {
            label.InnerHtml.AppendHtml(RequiredLabelOptions.MarkerHtml);
        }
        output.Content.AppendHtml(label);
    }

    /// <summary>Generates the input when there is no child content.</summary>
    /// <returns>The input.</returns>
    protected abstract TagBuilder GenerateInput();

    /// <summary>Adds classes of the container specific to the element.</summary>
    /// <param name="output">The output.</param>
    protected virtual void AddContainerClasses(TagHelperOutput output)
    {
    }

    /// <summary>Adds content after a generated input.</summary>
    /// <param name="output">The output.</param>
    protected virtual void AppendAfterInput(TagHelperOutput output)
    {
    }

    /// <summary>Whether the label gets the required marker.</summary>
    /// <returns><see langword="true"/> to add the marker.</returns>
    protected virtual bool IsRequired() => false;
}

/// <summary>
/// A checkbox with its label (Bootstrap 5 <c>form-check</c>): <c>&lt;checkbox asp-for="Confirmed" /&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Without child content the input is generated by MVC as for <c>&lt;input asp-for&gt;</c> (with validation attributes and
/// the hidden <c>false</c> value, placed where MVC places it). With child content (an input of your own, for example
/// "select all") the content is wrapped and the label points to <c>asp-input-id</c>.
/// </para>
/// <para>
/// <c>asp-switch="true"</c> renders a switch. The label gets the required marker for <see cref="RequiredAttribute"/>
/// or <c>asp-required="true"</c>.
/// </para>
/// </remarks>
[HtmlTargetElement("checkbox", Attributes = "asp-for")]
public class CheckboxTagHelper(IHtmlGenerator generator, IOptions<FormCheckOptions> options, IOptions<RequiredLabelOptions> requiredLabelOptions)
    : FormCheckTagHelper(generator, options, requiredLabelOptions)
{
    /// <summary>Whether the checkbox is rendered as a switch (<see cref="FormCheckOptions.SwitchCssClass"/>).</summary>
    [HtmlAttributeName("asp-switch")]
    public bool Switch { get; set; }

    /// <summary>Forces the required marker (<see langword="true"/>) or suppresses it (<see langword="false"/>).</summary>
    [HtmlAttributeName("asp-required")]
    public bool? Required { get; set; }

    /// <inheritdoc />
    protected override void AddContainerClasses(TagHelperOutput output)
    {
        if (Switch)
        {
            output.AddClass(Options.SwitchCssClass);
        }
    }

    /// <inheritdoc />
    protected override TagBuilder GenerateInput()
    {
        var input = Generator.GenerateCheckBox(ViewContext, For.ModelExplorer, For.Name, isChecked: null, htmlAttributes: null);
        if (Switch)
        {
            input.Attributes["role"] = "switch";
        }
        return input;
    }

    /// <inheritdoc />
    protected override void AppendAfterInput(TagHelperOutput output)
    {
        var mode = ViewContext.CheckBoxHiddenInputRenderMode;
        if (mode == CheckBoxHiddenInputRenderMode.None)
        {
            return;
        }

        var hidden = Generator.GenerateHiddenForCheckbox(ViewContext, For.ModelExplorer, For.Name);
        if (mode == CheckBoxHiddenInputRenderMode.EndOfForm && ViewContext.FormContext?.CanRenderAtEndOfForm == true)
        {
            ViewContext.FormContext.EndOfFormContent.Add(hidden);
        }
        else
        {
            output.PostContent.AppendHtml(hidden);
        }
    }

    /// <inheritdoc />
    protected override bool IsRequired() => Required ?? For.Metadata.ValidatorMetadata.OfType<RequiredAttribute>().Any();
}

/// <summary>
/// A radio button with its label (Bootstrap 5 <c>form-check</c>):
/// <c>&lt;radio asp-for="Kind" value="Company" asp-label="Company" /&gt;</c>.
/// </summary>
/// <remarks>
/// The input is checked when the value of the property equals <see cref="Value"/>. Its <c>id</c> is the field <c>id</c>
/// and the value (<c>Kind_Company</c>), so that radio buttons of one property have different ids. With child content the
/// content is wrapped as by <see cref="CheckboxTagHelper"/>.
/// </remarks>
[HtmlTargetElement("radio", Attributes = "asp-for")]
public class RadioTagHelper(IHtmlGenerator generator, IOptions<FormCheckOptions> options, IOptions<RequiredLabelOptions> requiredLabelOptions)
    : FormCheckTagHelper(generator, options, requiredLabelOptions)
{
    /// <summary>The value of this radio button.</summary>
    [HtmlAttributeName("value")]
    public string? Value { get; set; }

    /// <inheritdoc />
    protected override TagBuilder GenerateInput()
    {
        var value = Value ?? "";
        var input = Generator.GenerateRadioButton(ViewContext, For.ModelExplorer, For.Name, value, isChecked: null, htmlAttributes: null);
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        input.Attributes["id"] = TagBuilder.CreateSanitizedId($"{name}_{value}", "_");
        return input;
    }
}
