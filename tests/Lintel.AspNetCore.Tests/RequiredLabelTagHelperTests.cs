using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Lintel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class RequiredLabelTagHelperTests
{
    public sealed class Form
    {
        [Required]
        public string? Name { get; set; }

        public int Count { get; set; }

        public bool HasPets { get; set; }

        [RequiredIfTrue(nameof(HasPets))]
        public string? PetName { get; set; }

        public string? Kind { get; set; }

        [RequiredIf(nameof(Kind), "company")]
        public string? CompanyName { get; set; }

        [RegularExpressionIf("^[0-9]+$", nameof(HasPets), true)]
        public string? ChipNumber { get; set; }
    }

    private const string Marker = """<span class="text-danger ms-1" aria-hidden="true">*</span>""";

    private static readonly IServiceProvider _services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();

    private static string Render<TValue>(Form? model, Expression<Func<Form, TValue>> expression,
        Action<ModelStateDictionary>? posted = null, string prefix = "", bool? required = null, RequiredLabelOptions? options = null)
    {
        var metadataProvider = _services.GetRequiredService<IModelMetadataProvider>();
        var modelState = new ModelStateDictionary();
        posted?.Invoke(modelState);
        var viewData = new ViewDataDictionary<Form>(metadataProvider, modelState) { Model = model! };
        viewData.TemplateInfo.HtmlFieldPrefix = prefix;
        var viewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext(services: _services));
        viewContext.ViewData = viewData;

        var helper = new RequiredLabelTagHelper(Options.Create(options ?? new RequiredLabelOptions()))
        {
            For = _services.GetRequiredService<ModelExpressionProvider>().CreateModelExpression(viewData, expression),
            Required = required,
            ViewContext = viewContext,
        };
        var output = Output("label");
        helper.Process(Context("label"), output);
        return TestUtils.TagHelperRunner.Render(output);
    }

    private static Action<ModelStateDictionary> Posted(string key, object? rawValue)
        => modelState => modelState.SetModelValue(key, rawValue, rawValue?.ToString());

    [Fact]
    public void Required_property_is_marked_and_implicitly_required_is_not()
    {
        Render(new Form(), f => f.Name).Should().Be("<label>" + Marker + "</label>");
        Render(new Form(), f => f.Count).Should().Be("<label></label>");
    }

    [Fact]
    public void Asp_required_overrides_the_check_and_marker_is_configurable()
    {
        Render(new Form(), f => f.Name, required: false).Should().Be("<label></label>");
        Render(new Form(), f => f.Count, required: true, options: new RequiredLabelOptions { MarkerHtml = "<b>!</b>" })
            .Should().Be("<label><b>!</b></label>");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Required_if_uses_model_value(bool hasPets, bool marked)
        => Render(new Form { HasPets = hasPets }, f => f.PetName).Should().Be(marked ? "<label>" + Marker + "</label>" : "<label></label>");

    [Fact]
    public void Regular_expression_if_does_not_make_a_property_required()
        => Render(new Form { HasPets = true }, f => f.ChipNumber).Should().NotContain(Marker);

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("not a bool", false)]
    public void Without_model_required_if_converts_posted_value(string posted, bool marked)
        => Render(null, f => f.PetName, Posted("HasPets", posted)).Contains(Marker, StringComparison.Ordinal).Should().Be(marked);

    [Fact]
    public void Without_model_checkbox_values_use_the_first_one()
    {
        Render(null, f => f.PetName, Posted("HasPets", new[] { "true", "false" })).Should().Contain(Marker);
        Render(null, f => f.PetName, Posted("HasPets", new[] { "false" })).Should().NotContain(Marker);
    }

    [Fact]
    public void Without_model_string_related_property_is_compared_as_posted()
    {
        Render(null, f => f.CompanyName, Posted("Kind", "company")).Should().Contain(Marker);
        Render(null, f => f.CompanyName, Posted("Kind", "person")).Should().NotContain(Marker);
    }

    [Fact]
    public void Without_model_html_field_prefix_is_part_of_the_key()
    {
        Render(null, f => f.PetName, Posted("Booking.HasPets", "true"), prefix: "Booking").Should().Contain(Marker);
        Render(null, f => f.PetName, Posted("HasPets", "true"), prefix: "Booking").Should().NotContain(Marker);
    }

    [Fact]
    public void Without_model_and_posted_value_condition_is_not_met()
        => Render(null, f => f.PetName).Should().NotContain(Marker);

    [Fact]
    public void Arguments_are_checked()
    {
        var withoutOptions = () => new RequiredLabelTagHelper(null!);
        var withoutOutput = () => new RequiredLabelTagHelper(Options.Create(new RequiredLabelOptions())).Process(Context(), null!);

        withoutOptions.Should().Throw<ArgumentNullException>();
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
