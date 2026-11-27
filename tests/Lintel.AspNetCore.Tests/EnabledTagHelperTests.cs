using Lintel.AspNetCore.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class EnabledTagHelperTests
{
    private static string Render(string tagName, bool enabled, params TagHelperAttribute[] attributes)
    {
        var output = new TagHelperOutput(tagName, new TagHelperAttributeList(attributes),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        output.Content.SetContent("x");
        new EnabledTagHelper { Enabled = enabled }.Process(Context(tagName), output);
        return TestUtils.TagHelperRunner.Render(output);
    }

    [Fact]
    public void Disabled_link_loses_href_and_focus()
        => Render("a", false, new TagHelperAttribute("href", "/orders/edit/1"), new TagHelperAttribute("class", "btn btn-link"))
            .Should().Be("""<a class="btn btn-link disabled" aria-disabled="true" tabindex="-1">x</a>""");

    [Theory]
    [InlineData("button")]
    [InlineData("INPUT")]
    [InlineData("select")]
    [InlineData("textarea")]
    [InlineData("fieldset")]
    [InlineData("option")]
    [InlineData("optgroup")]
    public void Disabled_form_element_gets_disabled_attribute(string tagName)
        => Render(tagName, false, new TagHelperAttribute("type", "button"))
            .Should().Contain("""type="button" class="disabled" disabled="disabled">""");

    [Fact]
    public void Disabled_other_element_gets_aria_disabled()
        => Render("li", false).Should().Be("""<li class="disabled" aria-disabled="true">x</li>""");

    [Fact]
    public void Enabled_element_is_unchanged()
        => Render("a", true, new TagHelperAttribute("href", "/orders")).Should().Be("""<a href="/orders">x</a>""");

    [Fact]
    public void Runs_after_other_tag_helpers_and_checks_output()
    {
        new EnabledTagHelper().Order.Should().Be(1000);
        new EnabledTagHelper().Enabled.Should().BeTrue();

        var withoutOutput = () => new EnabledTagHelper().Process(Context(), null!);
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
