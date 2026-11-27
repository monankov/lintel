using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Lintel.DataAnnotations;

/// <summary>
/// Base class for validation attributes that need the whole model (the container of the validated property).
/// </summary>
/// <remarks>
/// The attribute is not modified during validation, so a single instance can safely validate concurrently.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public abstract class ModelAwareValidationAttribute : ValidationAttribute
{
    /// <summary>The message template used when neither <see cref="ValidationAttribute.ErrorMessage"/> nor a resource is set.</summary>
    public virtual string DefaultErrorMessage => "{0} is invalid.";

    /// <summary>The rule name for client-side validation (<c>data-val-{name}</c>).</summary>
    public virtual string ClientTypeName => GetType().Name.Replace("Attribute", "", StringComparison.Ordinal);

    /// <summary>The parameters for client-side validation, with lower-case names.</summary>
    public IReadOnlyDictionary<string, object?> ClientValidationParameters
        => GetClientValidationParameters().ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => kv.Value);

    /// <inheritdoc />
    public override bool RequiresValidationContext => true;

    /// <summary>
    /// Validates <paramref name="value"/> in the context of <paramref name="container"/>.
    /// </summary>
    /// <param name="value">The value of the validated property.</param>
    /// <param name="container">The object that declares the property.</param>
    /// <returns><see langword="true"/> if the value is valid.</returns>
    public abstract bool IsValid(object? value, object container);

    /// <inheritdoc />
    public override string FormatErrorMessage(string name) => FormatErrorMessage(name, relatedPropertyDisplayName: null);

    /// <summary>
    /// Formats the error message with an explicit display name of the related property
    /// (for example from ASP.NET Core model metadata).
    /// </summary>
    /// <param name="name">The display name of the validated property.</param>
    /// <param name="relatedPropertyDisplayName">The display name of the related property, if any.</param>
    /// <returns>The error message.</returns>
    public string FormatErrorMessage(string name, string? relatedPropertyDisplayName)
        => string.Format(CultureInfo.CurrentCulture, ErrorMessageTemplate, GetErrorMessageArguments(name, relatedPropertyDisplayName));

    /// <summary>
    /// Returns the arguments of the message template: <c>{0}</c> is the display name of the validated property.
    /// </summary>
    /// <param name="name">The display name of the validated property.</param>
    /// <param name="relatedPropertyDisplayName">The display name of the related property, if any.</param>
    /// <returns>The arguments.</returns>
    public virtual object?[] GetErrorMessageArguments(string name, string? relatedPropertyDisplayName) => [name];

    /// <summary>
    /// The message template: <see cref="ValidationAttribute.ErrorMessage"/> or the resource when set,
    /// otherwise <see cref="DefaultErrorMessage"/>.
    /// </summary>
    protected string ErrorMessageTemplate
        => string.IsNullOrEmpty(ErrorMessage) && string.IsNullOrEmpty(ErrorMessageResourceName)
            ? DefaultErrorMessage
            : ErrorMessageString;

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (IsValid(value, validationContext.ObjectInstance))
        {
            return ValidationResult.Success;
        }

        var message = FormatErrorMessage(validationContext.DisplayName, ResolveRelatedPropertyDisplayName(validationContext.ObjectInstance));
        return new ValidationResult(message, validationContext.MemberName is { } member ? [member] : null);
    }

    /// <summary>
    /// Resolves the display name of the related property from the model during server-side validation.
    /// </summary>
    /// <param name="container">The model.</param>
    /// <returns>The display name, or <see langword="null"/>.</returns>
    protected virtual string? ResolveRelatedPropertyDisplayName(object container) => null;

    /// <summary>
    /// Returns the parameters for client-side validation.
    /// </summary>
    /// <returns>The parameters.</returns>
    protected virtual IEnumerable<KeyValuePair<string, object?>> GetClientValidationParameters() => [];
}
