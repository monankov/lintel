using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Lintel.DataAnnotations.Tests.TestUtils;
using static Lintel.DataAnnotations.Tests.TestUtils.TestValidation;

namespace Lintel.DataAnnotations.Tests;

public class ValidationBehaviorTests
{
    public sealed class Address
    {
        public string? Country { get; set; }
    }

    public sealed class Booking
    {
        [Display(Name = "Arrival date")]
        public DateTime From { get; set; }

        [GreaterThan(nameof(From))]
        [Display(Name = "Departure date")]
        public DateTime To { get; set; }

        [DisplayName("Has pets")]
        public bool HasPets { get; set; }

        [RequiredIfTrue(nameof(HasPets))]
        public string? PetName { get; set; }

        public Address? Address { get; set; }

        [RequiredIf("Address.Country", "RU")]
        public string? Region { get; set; }
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Works_with_Validator_and_uses_display_names_of_both_properties()
    {
        var booking = new Booking { From = new DateTime(2026, 5, 2), To = new DateTime(2026, 5, 1), HasPets = true };

        var results = ValidateModel(booking);

        results.Select(r => r.ErrorMessage).Should().BeEquivalentTo(
            "Departure date must be greater than Arrival date.",
            "PetName is required due to Has pets being equal to True.");
        results.Should().Contain(r => r.MemberNames.Single() == nameof(Booking.To));
    }

    [Fact]
    public void Valid_model_has_no_errors()
    {
        var booking = new Booking { From = new DateTime(2026, 5, 1), To = new DateTime(2026, 5, 2), PetName = null };

        ValidateModel(booking).Should().BeEmpty();
    }

    [Fact]
    public void Nested_related_property_is_supported_and_null_on_the_way_gives_null()
    {
        var valid = new Booking { From = DateTime.MinValue, To = DateTime.MaxValue };
        var invalid = new Booking { From = DateTime.MinValue, To = DateTime.MaxValue, Address = new() { Country = "RU" } };

        ValidateModel(valid).Should().BeEmpty();
        // Without a display attribute the related property is named by its path, as in Structr.
        ValidateModel(invalid).Select(r => r.ErrorMessage).Should().Equal("Region is required due to Address.Country being equal to RU.");
    }

    [Fact]
    public void Message_is_formatted_on_every_validation()
    {
        // In Structr the first FormatErrorMessage stored the raw template in ErrorMessage,
        // and later validations returned "{0} must be greater than {1}.".
        var attribute = new GreaterThanAttribute(nameof(TestModel.Value2));
        attribute.FormatErrorMessage("Value1").Should().Be("Value1 must be greater than Value2.");

        var first = Validate(attribute, 1, 1);
        var second = Validate(attribute, 0, 1);

        first!.ErrorMessage.Should().Be("Value1 must be greater than Value2.");
        second!.ErrorMessage.Should().Be("Value1 must be greater than Value2.");
        attribute.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Attribute_can_be_used_concurrently()
    {
        var attribute = new RequiredIfAttribute(nameof(TestModel.Value2), 1);

        var messages = new System.Collections.Concurrent.ConcurrentBag<string?>();
        Parallel.For(0, 200, i => messages.Add(Validate(attribute, null, 1)?.ErrorMessage));

        messages.Should().AllBe("Value1 is required due to Value2 being equal to 1.");
    }

    [Fact]
    public void Missing_related_property_gives_clear_error()
    {
        var act = () => Validate(new GreaterThanAttribute("Missing"), 1, 1);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Related property 'Missing' was not found on type 'TestModel'.");
    }

    [Fact]
    public void Empty_related_property_name_is_rejected()
    {
        var act = () => new GreaterThanAttribute(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Regular_expression_timeout_counts_as_no_match()
    {
        var attribute = new RegularExpressionIfAttribute("^(a+)+$", nameof(TestModel.Value2), 1);
        var input = new string('a', 40) + "!";

        var result = Validate(attribute, input, 1);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Requires_validation_context()
        => new GreaterThanAttribute("X").RequiresValidationContext.Should().BeTrue();

    [Fact]
    public void Unknown_operator_is_rejected()
    {
        var act = () => OperatorMetadata.Get((Operator)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
