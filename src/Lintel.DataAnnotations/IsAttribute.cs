namespace Lintel.DataAnnotations;

/// <summary>
/// Validates that the property compares to the related property with <see cref="Operator"/>.
/// </summary>
/// <example><c>[Is(Operator.GreaterThan, nameof(StartDate))] public DateTime EndDate { get; set; }</c></example>
public class IsAttribute : ContingentValidationAttribute
{
    private readonly OperatorMetadata _metadata;

    /// <summary>
    /// Initializes the attribute.
    /// </summary>
    /// <param name="operator">The comparison.</param>
    /// <param name="relatedProperty">The name of the related property, possibly nested.</param>
    public IsAttribute(Operator @operator, string relatedProperty)
        : base(relatedProperty)
    {
        Operator = @operator;
        _metadata = OperatorMetadata.Get(@operator);
    }

    /// <summary>The comparison.</summary>
    public Operator Operator { get; }

    /// <summary>
    /// When <see langword="true"/>, the value is valid if exactly one of the two values is <see langword="null"/>.
    /// </summary>
    public bool PassOnNull { get; set; }

    /// <inheritdoc />
    public override string ClientTypeName => "Is";

    /// <inheritdoc />
    public override string DefaultErrorMessage => "{0} must be " + _metadata.ErrorMessage + " {1}.";

    /// <inheritdoc />
    public override bool IsValid(object? value, object? relatedPropertyValue, object container)
    {
        if (PassOnNull && (value is null) != (relatedPropertyValue is null))
        {
            return true;
        }

        return _metadata.IsValid(value, relatedPropertyValue);
    }

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> GetClientValidationParameters()
        => base.GetClientValidationParameters().Concat(
        [
            new("Operator", Operator.ToString()),
            new("PassOnNull", PassOnNull),
        ]);
}

/// <summary>Validates that the property equals the related property.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class EqualToAttribute(string relatedProperty) : IsAttribute(Operator.EqualTo, relatedProperty);

/// <summary>Validates that the property does not equal the related property.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class NotEqualToAttribute(string relatedProperty) : IsAttribute(Operator.NotEqualTo, relatedProperty);

/// <summary>Validates that the property is greater than the related property.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class GreaterThanAttribute(string relatedProperty) : IsAttribute(Operator.GreaterThan, relatedProperty);

/// <summary>Validates that the property is greater than or equal to the related property.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class GreaterThanOrEqualToAttribute(string relatedProperty) : IsAttribute(Operator.GreaterThanOrEqualTo, relatedProperty);

/// <summary>Validates that the property is less than the related property.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class LessThanAttribute(string relatedProperty) : IsAttribute(Operator.LessThan, relatedProperty);

/// <summary>Validates that the property is less than or equal to the related property.</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class LessThanOrEqualToAttribute(string relatedProperty) : IsAttribute(Operator.LessThanOrEqualTo, relatedProperty);

/// <summary>Validates that the property equals one of the items of the related property (or its single value).</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class InAttribute(string relatedProperty) : IsAttribute(Operator.In, relatedProperty);

/// <summary>Validates that the property equals none of the items of the related property (nor its single value).</summary>
/// <param name="relatedProperty">The name of the related property, possibly nested.</param>
public class NotInAttribute(string relatedProperty) : IsAttribute(Operator.NotIn, relatedProperty);
