using Lintel.DataAnnotations.Tests.TestUtils;
using static Lintel.DataAnnotations.Tests.TestUtils.TestValidation;

namespace Lintel.DataAnnotations.Tests;

public class RequiredIfAttributeTests
{
    private const string Related = nameof(TestModel.Value2);

    [Theory]
    [InlineData(null, 2, true)]
    [InlineData(null, 1, false)]
    [InlineData("a", 2, true)]
    [InlineData("a", 1, true)]
    public void RequiredIf(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfAttribute(Related, 1), value, related) is null).Should().Be(isValid);

    public static TheoryData<object?, bool> EmptyValues => new()
    {
        { null, true },
        { 1, false },
        { "a", false },
        { "", true },
        { "   ", true },
        { Array.Empty<int>(), true },
        { new[] { 5, 6, 7 }, false },
        { new List<string>(), true },
    };

    [Theory]
    [MemberData(nameof(EmptyValues))]
    public void Identifies_empty_values(object? value, bool isEmpty)
        => (Validate(new RequiredIfAttribute(Related, 1), value, 1) is not null).Should().Be(isEmpty);

    [Theory]
    [InlineData(null, 1, true)]
    [InlineData(null, 2, false)]
    [InlineData("a", 2, true)]
    public void RequiredIfNot(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfNotAttribute(Related, 1), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(null, true, false)]
    [InlineData(null, false, true)]
    [InlineData("a", true, true)]
    public void RequiredIfTrue(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfTrueAttribute(Related), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData("a", false, true)]
    public void RequiredIfFalse(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfFalseAttribute(Related), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(null, "", false)]
    [InlineData(null, null, false)]
    [InlineData(null, "x", true)]
    [InlineData("a", "", true)]
    public void RequiredIfEmpty(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfEmptyAttribute(Related), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(null, "x", false)]
    [InlineData(null, "", true)]
    [InlineData("a", "x", true)]
    public void RequiredIfNotEmpty(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfNotEmptyAttribute(Related), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(null, "Ab1", false)]
    [InlineData(null, "ab1", true)]
    [InlineData("a", "Ab1", true)]
    public void RequiredIfRegExMatch(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfRegExMatchAttribute(Related, @"^[A-Z][a-z]\d$"), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(null, "ab1", false)]
    [InlineData(null, "Ab1", true)]
    [InlineData("a", "ab1", true)]
    public void RequiredIfNotRegExMatch(object? value, object? related, bool isValid)
        => (Validate(new RequiredIfNotRegExMatchAttribute(Related, @"^[A-Z][a-z]\d$"), value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData("Abc", 2, true)]
    [InlineData("Abc", 1, false)]
    [InlineData("Bb0", 2, true)]
    [InlineData("Bb0", 1, true)]
    public void RegularExpressionIf(object? value, object? related, bool isValid)
        => (Validate(new RegularExpressionIfAttribute(@"[A-Z][a-z]\d", Related, 1), value, related) is null).Should().Be(isValid);

    [Fact]
    public void Gives_standard_messages()
    {
        Validate(new RequiredIfAttribute(Related, 1), null, 1)!.ErrorMessage
            .Should().Be("Value1 is required due to Value2 being equal to 1.");
        Validate(new RequiredIfNotAttribute(Related, 1), null, 2)!.ErrorMessage
            .Should().Be("Value1 is required due to Value2 being not equal to 1.");
        Validate(new RequiredIfEmptyAttribute(Related), null, null)!.ErrorMessage
            .Should().Be("Value1 is required due to Value2 being empty.");
        Validate(new RequiredIfNotEmptyAttribute(Related), null, "x")!.ErrorMessage
            .Should().Be("Value1 is required due to Value2 not being empty.");
        Validate(new RegularExpressionIfAttribute(@"[A-Z][a-z]\d", Related, 1), null, 1)!.ErrorMessage
            .Should().Be(@"Value1 must be in the format of [A-Z][a-z]\d due to Value2 being equal to 1");
    }

    [Fact]
    public void Gives_display_name_custom_and_resource_messages()
    {
        Validate(new RequiredIfAttribute(Related, 1), null, 1, relatedPropertyDisplayName: "Value_2_display_name")!
            .ErrorMessage.Should().Be("Value1 is required due to Value_2_display_name being equal to 1.");
        Validate(new RequiredIfAttribute(Related, 1), null, 1, errorMessage: "Custom error message.")!
            .ErrorMessage.Should().Be("Custom error message.");
        Validate(new RequiredIfAttribute(Related, 1), null, 1, fromResource: true)!
            .ErrorMessage.Should().Be(ErrorMessages.ErrorMessageFromResource);
    }

    [Fact]
    public void Client_validation_parameters()
    {
        var requiredIf = new RequiredIfAttribute("Country", "RU");
        var regex = new RegularExpressionIfAttribute(@"\d+", "Country", Operator.NotEqualTo, "RU");

        requiredIf.ClientTypeName.Should().Be("RequiredIf");
        requiredIf.ClientValidationParameters.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["relatedproperty"] = "Country",
            ["operator"] = "EqualTo",
            ["relatedpropertyvalue"] = "RU",
        });
        regex.ClientTypeName.Should().Be("RequiredIf");
        regex.ClientValidationParameters.Should().ContainKey("pattern").WhoseValue.Should().Be(@"\d+");
        new RequiredIfEmptyAttribute("Country").ClientTypeName.Should().Be("RequiredIfEmpty");
    }
}
