namespace Lintel.DataAnnotations;

/// <summary>
/// Makes the property required when the related property compares to <see cref="RelatedPropertyExpectedValue"/>
/// with <see cref="Operator"/>.
/// </summary>
/// <remarks>Empty means <see langword="null"/>, a white-space string or an empty collection.</remarks>
/// <example><c>[RequiredIf(nameof(Country), "RU")] public string? Region { get; set; }</c></example>
public class RequiredIfAttribute : ContingentValidationAttribute
{
    /// <summary>
    /// Initializes the attribute.
    /// </summary>
    /// <param name="relatedProperty">The name of the related property, possibly nested.</param>
    /// <param name="operator">How the related property is compared to <paramref name="relatedPropertyExpectedValue"/>.</param>
    /// <param name="relatedPropertyExpectedValue">The value (pattern, list) the related property is compared to.</param>
    public RequiredIfAttribute(string relatedProperty, Operator @operator, object? relatedPropertyExpectedValue)
        : base(relatedProperty)
    {
        Operator = @operator;
        RelatedPropertyExpectedValue = relatedPropertyExpectedValue;
        Metadata = OperatorMetadata.Get(@operator);
    }

    /// <summary>
    /// Initializes the attribute that requires the property when the related property equals <paramref name="relatedPropertyExpectedValue"/>.
    /// </summary>
    /// <param name="relatedProperty">The name of the related property, possibly nested.</param>
    /// <param name="relatedPropertyExpectedValue">The value the related property is compared to.</param>
    public RequiredIfAttribute(string relatedProperty, object? relatedPropertyExpectedValue)
        : this(relatedProperty, Operator.EqualTo, relatedPropertyExpectedValue)
    {
    }

    /// <summary>How the related property is compared to <see cref="RelatedPropertyExpectedValue"/>.</summary>
    public Operator Operator { get; }

    /// <summary>The value (pattern, list) the related property is compared to.</summary>
    public object? RelatedPropertyExpectedValue { get; }

    /// <summary>The metadata of <see cref="Operator"/>.</summary>
    protected OperatorMetadata Metadata { get; }

    /// <inheritdoc />
    public override string ClientTypeName => "RequiredIf";

    /// <inheritdoc />
    public override string DefaultErrorMessage => "{0} is required due to {1} being " + Metadata.ErrorMessage + " {2}.";

    /// <inheritdoc />
    public override bool IsValid(object? value, object? relatedPropertyValue, object container)
        => !IsConditionMet(relatedPropertyValue) || !PropertyIsEmpty(value);

    /// <summary>
    /// Whether the related property value meets the condition that triggers the validation.
    /// </summary>
    /// <param name="relatedPropertyValue">The value of the related property.</param>
    /// <returns><see langword="true"/> if the condition is met.</returns>
    public bool IsConditionMet(object? relatedPropertyValue) => Metadata.IsValid(relatedPropertyValue, RelatedPropertyExpectedValue);

    /// <summary>
    /// Arguments: <c>{0}</c> — the validated property, <c>{1}</c> — the related property, <c>{2}</c> — the expected value.
    /// </summary>
    /// <inheritdoc />
    public override object?[] GetErrorMessageArguments(string name, string? relatedPropertyDisplayName)
        => [.. base.GetErrorMessageArguments(name, relatedPropertyDisplayName), RelatedPropertyExpectedValue];

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> GetClientValidationParameters()
        => base.GetClientValidationParameters().Concat(
        [
            new("Operator", Operator.ToString()),
            new("RelatedPropertyValue", RelatedPropertyExpectedValue),
        ]);
}

/// <summary>Makes the property required when the related property does not equal the expected value.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
/// <param name="relatedPropertyExpectedValue">The value the related property is compared to.</param>
public class RequiredIfNotAttribute(string relatedProperty, object? relatedPropertyExpectedValue)
    : RequiredIfAttribute(relatedProperty, Operator.NotEqualTo, relatedPropertyExpectedValue);

/// <summary>Makes the property required when the related property is <see langword="true"/>.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class RequiredIfTrueAttribute(string relatedProperty) : RequiredIfAttribute(relatedProperty, Operator.EqualTo, true);

/// <summary>Makes the property required when the related property is <see langword="false"/>.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class RequiredIfFalseAttribute(string relatedProperty) : RequiredIfAttribute(relatedProperty, Operator.EqualTo, false);

/// <summary>Makes the property required when the related property matches a regular expression.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
/// <param name="relatedPropertyPattern">The regular expression.</param>
public class RequiredIfRegExMatchAttribute(string relatedProperty, string relatedPropertyPattern)
    : RequiredIfAttribute(relatedProperty, Operator.RegExMatch, relatedPropertyPattern);

/// <summary>Makes the property required when the related property does not match a regular expression.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
/// <param name="relatedPropertyPattern">The regular expression.</param>
public class RequiredIfNotRegExMatchAttribute(string relatedProperty, string relatedPropertyPattern)
    : RequiredIfAttribute(relatedProperty, Operator.NotRegExMatch, relatedPropertyPattern);

/// <summary>
/// Requires the property to match <see cref="Pattern"/> when the related property compares to the expected value.
/// </summary>
public class RegularExpressionIfAttribute : RequiredIfAttribute
{
    /// <summary>
    /// Initializes the attribute.
    /// </summary>
    /// <param name="pattern">The regular expression the property must match.</param>
    /// <param name="relatedProperty">The name of the related property, possibly nested.</param>
    /// <param name="operator">How the related property is compared to <paramref name="relatedPropertyExpectedValue"/>.</param>
    /// <param name="relatedPropertyExpectedValue">The value the related property is compared to.</param>
    public RegularExpressionIfAttribute(string pattern, string relatedProperty, Operator @operator, object? relatedPropertyExpectedValue)
        : base(relatedProperty, @operator, relatedPropertyExpectedValue)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// Initializes the attribute that applies when the related property equals <paramref name="relatedPropertyExpectedValue"/>.
    /// </summary>
    /// <param name="pattern">The regular expression the property must match.</param>
    /// <param name="relatedProperty">The name of the related property, possibly nested.</param>
    /// <param name="relatedPropertyExpectedValue">The value the related property is compared to.</param>
    public RegularExpressionIfAttribute(string pattern, string relatedProperty, object? relatedPropertyExpectedValue)
        : this(pattern, relatedProperty, Operator.EqualTo, relatedPropertyExpectedValue)
    {
    }

    /// <summary>The regular expression the property must match.</summary>
    /// <remarks>
    /// The client rule name stays <c>requiredif</c> (inherited, as in Structr) with an extra <c>pattern</c> parameter.
    /// </remarks>
    public string Pattern { get; set; }

    /// <inheritdoc />
    public override string DefaultErrorMessage =>
        "{0} must be in the format of {3} due to {1} being " + Metadata.ErrorMessage + " {2}";

    /// <inheritdoc />
    public override bool IsValid(object? value, object? relatedPropertyValue, object container)
        => !IsConditionMet(relatedPropertyValue) || OperatorMetadata.Get(Operator.RegExMatch).IsValid(value, Pattern);

    /// <summary>
    /// Arguments: <c>{0}</c> — the validated property, <c>{1}</c> — the related property, <c>{2}</c> — the expected value,
    /// <c>{3}</c> — the pattern.
    /// </summary>
    /// <inheritdoc />
    public override object?[] GetErrorMessageArguments(string name, string? relatedPropertyDisplayName)
        => [.. base.GetErrorMessageArguments(name, relatedPropertyDisplayName), Pattern];

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> GetClientValidationParameters()
        => base.GetClientValidationParameters().Append(new("Pattern", Pattern));
}

/// <summary>Makes the property required when the related property is empty.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class RequiredIfEmptyAttribute(string relatedProperty) : ContingentValidationAttribute(relatedProperty)
{
    /// <inheritdoc />
    public override string DefaultErrorMessage => "{0} is required due to {1} being empty.";

    /// <inheritdoc />
    public override bool IsValid(object? value, object? relatedPropertyValue, object container)
        => !PropertyIsEmpty(relatedPropertyValue) || !PropertyIsEmpty(value);
}

/// <summary>Makes the property required when the related property is not empty.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class RequiredIfNotEmptyAttribute(string relatedProperty) : ContingentValidationAttribute(relatedProperty)
{
    /// <inheritdoc />
    public override string DefaultErrorMessage => "{0} is required due to {1} not being empty.";

    /// <inheritdoc />
    public override bool IsValid(object? value, object? relatedPropertyValue, object container)
        => PropertyIsEmpty(relatedPropertyValue) || !PropertyIsEmpty(value);
}
