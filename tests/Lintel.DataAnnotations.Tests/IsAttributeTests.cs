using Lintel.DataAnnotations.Tests.TestUtils;
using static Lintel.DataAnnotations.Tests.TestUtils.TestValidation;

namespace Lintel.DataAnnotations.Tests;

public class IsAttributeTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData("a", "a", true)]
    [InlineData(null, null, true)]
    [InlineData(1, 2, false)]
    [InlineData(1, null, false)]
    [InlineData(null, 1, false)]
    public void EqualTo(object? value, object? related, bool isValid)
        => (Validate<EqualToAttribute>(value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(null, 1, true)]
    [InlineData(1, null, true)]
    [InlineData(1, 1, false)]
    [InlineData(null, null, false)]
    public void NotEqualTo(object? value, object? related, bool isValid)
        => (Validate<NotEqualToAttribute>(value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(0, 1, false)]
    [InlineData(null, 1, false)]
    [InlineData(null, null, false)]
    [InlineData("b", "a", true)]
    public void GreaterThan(object? value, object? related, bool isValid)
        => (Validate<GreaterThanAttribute>(value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 1, true)]
    [InlineData(null, null, true)]
    [InlineData(0, 1, false)]
    [InlineData(null, 1, false)]
    public void GreaterThanOrEqualTo(object? value, object? related, bool isValid)
        => (Validate<GreaterThanOrEqualToAttribute>(value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, null, false)]
    public void LessThan(object? value, object? related, bool isValid)
        => (Validate<LessThanAttribute>(value, related) is null).Should().Be(isValid);

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, true)]
    [InlineData(null, null, true)]
    [InlineData(2, 1, false)]
    public void LessThanOrEqualTo(object? value, object? related, bool isValid)
        => (Validate<LessThanOrEqualToAttribute>(value, related) is null).Should().Be(isValid);

    [Fact]
    public void In_and_NotIn_with_list()
    {
        int[] list = [1, 2, 3];

        Validate<InAttribute>(2, list).Should().BeNull();
        Validate<InAttribute>(5, list).Should().NotBeNull();
        Validate<NotInAttribute>(5, list).Should().BeNull();
        Validate<NotInAttribute>(2, list).Should().NotBeNull();
    }

    [Fact]
    public void In_and_NotIn_with_single_value()
    {
        Validate<InAttribute>(1, 1).Should().BeNull();
        Validate<InAttribute>(1, 2).Should().NotBeNull();
        Validate<NotInAttribute>(1, 2).Should().BeNull();
    }

    [Fact]
    public void In_treats_string_as_single_value_not_as_characters()
    {
        Validate<InAttribute>("abc", "abc").Should().BeNull();
        Validate<InAttribute>("a", "abc").Should().NotBeNull();
        Validate<InAttribute>("abc", new[] { "x", "abc" }).Should().BeNull();
    }

    [Fact]
    public void Numbers_of_different_types_are_compared_by_value()
    {
        Validate<EqualToAttribute>(1, 1L).Should().BeNull();
        Validate<EqualToAttribute>(1.5m, 1.5d).Should().BeNull();
        Validate<GreaterThanAttribute>(2, 1L).Should().BeNull();
        Validate<LessThanAttribute>((short)1, 2.5).Should().BeNull();
        Validate<InAttribute>(2L, new[] { 1, 2 }).Should().BeNull();
    }

    [Fact]
    public void Incomparable_values_are_invalid_instead_of_throwing()
    {
        var act = () => Validate<GreaterThanAttribute>("a", 1);

        act.Should().NotThrow().Which.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(1, null, true)]
    [InlineData(null, 2, true)]
    [InlineData(null, null, false)]
    public void PassOnNull_passes_when_exactly_one_value_is_null(object? value, object? related, bool isValid)
        => (Validate<GreaterThanAttribute>(value, related, passOnNull: true) is null).Should().Be(isValid);

    [Theory]
    [InlineData(typeof(EqualToAttribute), 1, 2, "Value1 must be equal to Value2.")]
    [InlineData(typeof(NotEqualToAttribute), 1, 1, "Value1 must be not equal to Value2.")]
    [InlineData(typeof(GreaterThanAttribute), 1, 1, "Value1 must be greater than Value2.")]
    [InlineData(typeof(GreaterThanOrEqualToAttribute), 0, 1, "Value1 must be greater than or equal to Value2.")]
    [InlineData(typeof(LessThanAttribute), 1, 1, "Value1 must be less than Value2.")]
    [InlineData(typeof(LessThanOrEqualToAttribute), 2, 1, "Value1 must be less than or equal to Value2.")]
    [InlineData(typeof(InAttribute), 1, 2, "Value1 must be in Value2.")]
    [InlineData(typeof(NotInAttribute), 1, 1, "Value1 must be not in Value2.")]
    public void Gives_standard_message(Type attributeType, object value, object related, string expected)
    {
        var attribute = (IsAttribute)Activator.CreateInstance(attributeType, nameof(TestModel.Value2))!;

        Validate(attribute, value, related)!.ErrorMessage.Should().Be(expected);
    }

    [Fact]
    public void Gives_display_name_custom_and_resource_messages()
    {
        Validate(new GreaterThanAttribute(nameof(TestModel.Value2)), 1, 1, relatedPropertyDisplayName: "Value_2_display_name")!
            .ErrorMessage.Should().Be("Value1 must be greater than Value_2_display_name.");
        Validate(new GreaterThanAttribute(nameof(TestModel.Value2)), 1, 1, errorMessage: "Custom error message.")!
            .ErrorMessage.Should().Be("Custom error message.");
        Validate(new GreaterThanAttribute(nameof(TestModel.Value2)), 1, 1, fromResource: true)!
            .ErrorMessage.Should().Be(ErrorMessages.ErrorMessageFromResource);
    }

    [Fact]
    public void Client_validation_parameters()
    {
        var attribute = new GreaterThanAttribute("Start") { PassOnNull = true };

        attribute.ClientTypeName.Should().Be("Is");
        attribute.ClientValidationParameters.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["relatedproperty"] = "Start",
            ["operator"] = "GreaterThan",
            ["passonnull"] = true,
        });
    }
}
