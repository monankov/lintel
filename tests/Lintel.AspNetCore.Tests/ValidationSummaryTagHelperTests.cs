using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class ValidationSummaryTagHelperTests
{
    private static string Render(Action<ModelStateDictionary> errors, bool includePropertyErrors = false, string prefix = "",
        ValidationSummaryOptions? options = null, params TagHelperAttribute[] attributes)
    {
        var modelState = new ModelStateDictionary();
        errors(modelState);
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState);
        viewData.TemplateInfo.HtmlFieldPrefix = prefix;
        var viewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext());
        viewContext.ViewData = viewData;

        var output = new TagHelperOutput("validation-summary", [.. attributes],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        new ValidationSummaryTagHelper(Options.Create(options ?? new ValidationSummaryOptions()))
        {
            IncludePropertyErrors = includePropertyErrors,
            ViewContext = viewContext,
        }.Process(Context("validation-summary"), output);
        return TestUtils.TagHelperRunner.Render(output);
    }

    private static void FormAndFieldErrors(ModelStateDictionary modelState)
    {
        modelState.AddModelError("Name", "Name is required.");
        modelState.AddModelError("", "The booking cannot be saved.");
    }

    [Fact]
    public void Shows_form_errors_in_bootstrap_alert_and_keeps_attributes()
        => Render(FormAndFieldErrors, attributes: [new TagHelperAttribute("id", "errors"), new TagHelperAttribute("class", "mt-3")])
            .Should().Be("""<div id="errors" class="mt-3 alert alert-danger" role="alert"><ul class="mb-0"><li>The booking cannot be saved.</li></ul></div>""");

    [Fact]
    public void Property_errors_follow_form_errors_when_included()
        => Render(FormAndFieldErrors, includePropertyErrors: true)
            .Should().Contain("<li>The booking cannot be saved.</li><li>Name is required.</li>");

    [Fact]
    public void Renders_nothing_without_errors_to_show()
    {
        Render(modelState => modelState.AddModelError("Name", "Name is required.")).Should().BeEmpty();
        Render(_ => { }, includePropertyErrors: true).Should().BeEmpty();
        Render(modelState => modelState.AddModelError("", " ")).Should().BeEmpty();
    }

    [Fact]
    public void Messages_are_encoded_and_line_breaks_kept()
        => Render(modelState => modelState.AddModelError("", "The value '<script>x</script>' is invalid.\nTry again.\r\nOr later."))
            .Should().Contain("<li>The value &#x27;&lt;script&gt;x&lt;/script&gt;&#x27; is invalid.<br>Try again.<br>Or later.</li>")
            .And.NotContain("<script>");

    [Fact]
    public void Form_errors_of_partial_view_use_the_html_field_prefix()
    {
        Render(modelState => modelState.AddModelError("Booking", "Booking error."), prefix: "Booking").Should().Contain("Booking error.");
        Render(modelState => modelState.AddModelError("", "Page error."), prefix: "Booking").Should().BeEmpty();
    }

    [Fact]
    public void Classes_are_configurable()
        => Render(FormAndFieldErrors, options: new ValidationSummaryOptions { CssClass = "errors", ListCssClass = null })
            .Should().Be("""<div class="errors" role="alert"><ul><li>The booking cannot be saved.</li></ul></div>""");

    [Fact]
    public void Arguments_are_checked()
    {
        var withoutOptions = () => new ValidationSummaryTagHelper(null!);
        var withoutOutput = () => new ValidationSummaryTagHelper(Options.Create(new ValidationSummaryOptions())).Process(Context(), null!);

        withoutOptions.Should().Throw<ArgumentNullException>();
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
