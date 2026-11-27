using System.ComponentModel.DataAnnotations;

namespace Lintel.DataAnnotations.Tests.TestUtils;

internal static class ErrorMessages
{
    internal static string ErrorMessageFromResource => "Some test error message from resource.";
}

public sealed class TestModel
{
    public object? Value1 { get; set; }
    public object? Value2 { get; set; }
}

internal static class TestValidation
{
    /// <summary>Validates <see cref="TestModel.Value1"/> with <paramref name="attribute"/>, Value2 being the related property.</summary>
    public static ValidationResult? Validate(ContingentValidationAttribute attribute,
        object? value1,
        object? value2,
        string? relatedPropertyDisplayName = null,
        string? errorMessage = null,
        bool fromResource = false)
    {
        if (relatedPropertyDisplayName is not null)
        {
            attribute.RelatedPropertyDisplayName = relatedPropertyDisplayName;
        }
        if (errorMessage is not null)
        {
            attribute.ErrorMessage = errorMessage;
        }
        if (fromResource)
        {
            attribute.ErrorMessageResourceName = nameof(ErrorMessages.ErrorMessageFromResource);
            attribute.ErrorMessageResourceType = typeof(ErrorMessages);
        }

        var model = new TestModel { Value1 = value1, Value2 = value2 };
        var context = new ValidationContext(model) { MemberName = nameof(TestModel.Value1) };
        return attribute.GetValidationResult(model.Value1, context);
    }

    public static ValidationResult? Validate<TAttribute>(object? value1, object? value2, bool passOnNull = false)
        where TAttribute : IsAttribute
    {
        var attribute = (TAttribute)Activator.CreateInstance(typeof(TAttribute), nameof(TestModel.Value2))!;
        attribute.PassOnNull = passOnNull;
        return Validate(attribute, value1, value2);
    }
}
