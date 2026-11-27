using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class FormCheckAndBadgeTagHelperTests
{
    public sealed class Form
    {
        [Required]
        [Display(Name = "Agree")]
        public bool Agree { get; set; }

        public string? Kind { get; set; } = "a";
    }

    private static readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddSingleton(new System.Diagnostics.DiagnosticListener("tests"))
        .AddSingleton<System.Diagnostics.DiagnosticSource>(sp => sp.GetRequiredService<System.Diagnostics.DiagnosticListener>())
        .AddMvc().Services.BuildServiceProvider();

    private static ViewContext ViewContext(CheckBoxHiddenInputRenderMode mode = CheckBoxHiddenInputRenderMode.Inline)
    {
        var viewData = new ViewDataDictionary<Form>(_services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary())
        {
            Model = new Form(),
        };
        var viewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext(services: _services));
        viewContext.ViewData = viewData;
        viewContext.CheckBoxHiddenInputRenderMode = mode;
        return viewContext;
    }

    private static ModelExpression For<TValue>(ViewContext viewContext, Expression<Func<Form, TValue>> expression)
        => _services.GetRequiredService<ModelExpressionProvider>().CreateModelExpression((ViewDataDictionary<Form>)viewContext.ViewData, expression);

    private static string Run(FormCheckTagHelper helper, string childContent = "")
    {
        var output = Output("checkbox", childContent);
        helper.ProcessAsync(Context("checkbox"), output).GetAwaiter().GetResult();
        return Render(output);
    }

    private static CheckboxTagHelper Checkbox(ViewContext viewContext, FormCheckOptions? options = null)
        => new(_services.GetRequiredService<IHtmlGenerator>(), Options.Create(options ?? new FormCheckOptions()), Options.Create(new RequiredLabelOptions()))
        {
            ViewContext = viewContext,
            For = For(viewContext, f => f.Agree),
        };

    [Fact]
    public void Checkbox_puts_hidden_value_inline_or_omits_it()
    {
        var inline = ViewContext();
        Run(Checkbox(inline)).Should().EndWith("""<input name="Agree" type="hidden" value="false" /></div>""");

        Run(Checkbox(ViewContext(CheckBoxHiddenInputRenderMode.None))).Should().NotContain("type=\"hidden\"");
    }

    [Fact]
    public void Checkbox_without_form_context_renders_hidden_value_after_the_container()
    {
        var html = Run(Checkbox(ViewContext(CheckBoxHiddenInputRenderMode.EndOfForm)));

        html.Should().EndWith("""<input name="Agree" type="hidden" value="false" /></div>""", "outside a form it stays inside the container");
    }

    [Fact]
    public void Required_marker_can_be_turned_off_and_empty_label_is_not_rendered()
    {
        var context = ViewContext();
        var withoutMarker = Checkbox(context);
        withoutMarker.Required = false;
        var withoutLabel = Checkbox(context);
        withoutLabel.Label = "";

        Run(withoutMarker).Should().Contain("""<label class="form-check-label" for="Agree">Agree</label>""");
        Run(withoutLabel).Should().NotContain("<label");
    }

    [Fact]
    public void Child_content_without_input_id_gets_a_label_without_for_and_classes_are_configurable()
    {
        var helper = Checkbox(ViewContext(), new FormCheckOptions { ContainerCssClass = "check", LabelCssClass = null, InputCssClass = null });

        Run(helper, """<input type="checkbox" />""").Should().Be(
            """<div class="check"><input type="checkbox" /><label>Agree<span class="text-danger ms-1" aria-hidden="true">*</span></label></div>""");
    }

    [Fact]
    public void Radio_without_value_and_without_input_class()
    {
        var context = ViewContext();
        var radio = new RadioTagHelper(_services.GetRequiredService<IHtmlGenerator>(), Options.Create(new FormCheckOptions { InputCssClass = " " }),
            Options.Create(new RequiredLabelOptions()))
        {
            ViewContext = context,
            For = For(context, f => f.Kind),
        };

        var html = Run(radio);

        html.Should().Contain("id=\"Kind_\"").And.Contain("value=\"\"").And.NotContain("form-check-input");
    }

    [Fact]
    public void Form_check_arguments_are_checked()
    {
        var generator = _services.GetRequiredService<IHtmlGenerator>();
        var options = Options.Create(new FormCheckOptions());
        var marker = Options.Create(new RequiredLabelOptions());
        var actions = new Action[]
        {
            () => _ = new CheckboxTagHelper(null!, options, marker),
            () => _ = new CheckboxTagHelper(generator, null!, marker),
            () => _ = new RadioTagHelper(generator, options, null!),
            () => Checkbox(ViewContext()).ProcessAsync(Context(), null!).GetAwaiter().GetResult(),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentNullException>();
        }
    }

    private static string Badge(object? value, bool? hidden = null, params TagHelperAttribute[] attributes)
    {
        var output = new TagHelperOutput("tab-badge", [.. attributes],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        new TabBadgeTagHelper { Value = value, Hidden = hidden }.Process(Context("tab-badge"), output);
        return Render(output);
    }

    [Fact]
    public void Badge_shows_compact_number_with_full_title_and_other_values_as_is()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Badge(12_345L).Should().Be("""<span class="badge text-bg-secondary ms-1" title="12,345">12K</span>""");
        Badge("7").Should().Be("""<span class="badge text-bg-secondary ms-1" title="7">7</span>""");
        Badge("new").Should().Be("""<span class="badge text-bg-secondary ms-1">new</span>""");
        Badge(null).Should().Be("""<span class="badge text-bg-secondary ms-1"></span>""");
        Badge((short)-3).Should().Contain("d-none");
    }

    [Fact]
    public void Badge_hidden_overrides_the_value_and_own_classes_are_kept()
    {
        Badge(0, hidden: false).Should().NotContain("d-none");
        Badge(5, hidden: true).Should().Contain("d-none");
        Badge(5, attributes: new TagHelperAttribute("class", "float-end")).Should().StartWith("""<span class="float-end badge text-bg-secondary ms-1" """);

        var withoutOutput = () => new TabBadgeTagHelper().Process(Context(), null!);
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
