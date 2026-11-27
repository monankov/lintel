using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class FormControlTagHelperTests
{
    public sealed class Form
    {
        public string? Name { get; set; }
    }

    private static readonly IServiceProvider _services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();

    private static string Render(string tagName, string? type = null, string? error = null, string prefix = "",
        bool enabled = true, FormControlOptions? options = null, string? cssClass = null)
    {
        var modelState = new ModelStateDictionary();
        if (error is not null)
        {
            modelState.AddModelError(error, "Invalid");
        }
        var viewData = new ViewDataDictionary<Form>(_services.GetRequiredService<IModelMetadataProvider>(), modelState);
        viewData.TemplateInfo.HtmlFieldPrefix = prefix;
        var viewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext(services: _services));
        viewContext.ViewData = viewData;

        var attributes = new TagHelperAttributeList();
        if (type is not null)
        {
            attributes.Add("type", type);
        }
        if (cssClass is not null)
        {
            attributes.Add("class", cssClass);
        }
        var output = new TagHelperOutput(tagName, attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()))
        {
            TagMode = TagMode.SelfClosing,
        };

        new FormControlTagHelper(Options.Create(options ?? new FormControlOptions()))
        {
            For = _services.GetRequiredService<ModelExpressionProvider>().CreateModelExpression(viewData, f => f.Name),
            Enabled = enabled,
            ViewContext = viewContext,
        }.Process(Context(tagName), output);
        return TestUtils.TagHelperRunner.Render(output);
    }

    [Theory]
    [InlineData("input", "text", "form-control")]
    [InlineData("input", "date", "form-control")]
    [InlineData("input", "FILE", "form-control")]
    [InlineData("input", null, "form-control")]
    [InlineData("textarea", null, "form-control")]
    [InlineData("select", null, "form-select")]
    [InlineData("input", "checkbox", "form-check-input")]
    [InlineData("input", "radio", "form-check-input")]
    [InlineData("input", "range", "form-range")]
    public void Adds_bootstrap_class_by_element_and_type(string tagName, string? type, string expected)
        => Render(tagName, type).Should().Contain($"""class="{expected}" """.TrimEnd());

    [Theory]
    [InlineData("hidden")]
    [InlineData("submit")]
    [InlineData("reset")]
    [InlineData("button")]
    [InlineData("image")]
    public void Hidden_inputs_and_buttons_get_no_class(string type)
        => Render("input", type, error: "Name").Should().NotContain("class=");

    [Fact]
    public void Field_with_error_gets_invalid_class_and_keeps_own_classes()
    {
        Render("input", "text", error: "Name", cssClass: "w-50").Should().Be("""<input type="text" class="w-50 form-control is-invalid" />""");
        Render("input", "text", error: "Other").Should().Be("""<input type="text" class="form-control" />""");
    }

    [Fact]
    public void Error_is_looked_up_by_full_field_name()
    {
        Render("input", "text", error: "Booking.Name", prefix: "Booking").Should().Contain("is-invalid");
        Render("input", "text", error: "Name", prefix: "Booking").Should().NotContain("is-invalid");
    }

    [Fact]
    public void Can_be_turned_off_and_configured()
    {
        Render("input", "text", error: "Name", enabled: false).Should().Be("""<input type="text" />""");

        var options = new FormControlOptions { ControlCssClass = "input", InvalidCssClass = "input--error", SelectCssClass = null };
        Render("input", "text", error: "Name", options: options).Should().Be("""<input type="text" class="input input--error" />""");
        Render("select", options: options).Should().Be("<select />");
    }

    [Fact]
    public void Invalid_class_can_be_several_classes()
        => Render("input", "text", error: "Name", options: new FormControlOptions { InvalidCssClass = "input-validation-error text-danger" })
            .Should().Be("""<input type="text" class="form-control input-validation-error text-danger" />""");

    [Fact]
    public void Arguments_are_checked()
    {
        new FormControlTagHelper(Options.Create(new FormControlOptions())).Order.Should().Be(1000);

        var withoutOptions = () => new FormControlTagHelper(null!);
        var withoutOutput = () => new FormControlTagHelper(Options.Create(new FormControlOptions())).Process(Context(), null!);

        withoutOptions.Should().Throw<ArgumentNullException>();
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
