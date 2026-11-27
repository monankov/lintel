using System.ComponentModel.DataAnnotations;

namespace Lintel.DataAnnotations.Tests;

public class ModelAwareValidationAttributeTests
{
    /// <summary>A model-aware attribute without a related property: valid when the model allows it.</summary>
    private sealed class AllowedByModelAttribute : ModelAwareValidationAttribute
    {
        public override bool IsValid(object? value, object container) => ((Model)container).Allowed;
    }

    private sealed class Model
    {
        public bool Allowed { get; init; }

        [AllowedByModel]
        public string? Value { get; init; }
    }

    private static ValidationResult? Validate(Model model, string? memberName = nameof(Model.Value))
        => new AllowedByModelAttribute().GetValidationResult(model.Value,
            new ValidationContext(model) { MemberName = memberName, DisplayName = "Value" });

    [Fact]
    public void Uses_base_default_message_and_arguments()
    {
        var result = Validate(new Model { Allowed = false });

        result!.ErrorMessage.Should().Be("Value is invalid.");
        result.MemberNames.Should().Equal("Value");
        new AllowedByModelAttribute().GetErrorMessageArguments("Value", "ignored").Should().Equal("Value");
    }

    [Fact]
    public void Valid_model_passes()
        => Validate(new Model { Allowed = true }).Should().Be(ValidationResult.Success);

    [Fact]
    public void Validation_without_member_name_has_no_member_names()
    {
        var model = new Model { Allowed = false };
        var results = new List<ValidationResult>();

        Validator.TryValidateValue(model.Value, new ValidationContext(model), results, [new AllowedByModelAttribute()]);

        results.Should().ContainSingle().Which.MemberNames.Should().BeEmpty();
    }

    [Fact]
    public void Client_type_name_is_class_name_without_suffix()
        => new AllowedByModelAttribute().ClientTypeName.Should().Be("AllowedByModel");
}
