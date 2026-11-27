using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Lintel.DataAnnotations;

/// <summary>
/// Base class for validation attributes that depend on another property of the model.
/// </summary>
/// <remarks>
/// <see cref="RelatedProperty"/> may be nested (<c>"Address.City"</c>); a <see langword="null"/> on the way gives a
/// <see langword="null"/> related value. In error messages the related property is named by
/// <see cref="RelatedPropertyDisplayName"/>, else by its <see cref="DisplayAttribute"/> or <see cref="DisplayNameAttribute"/>,
/// else by <see cref="RelatedProperty"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public abstract class ContingentValidationAttribute : ModelAwareValidationAttribute
{
    /// <summary>
    /// Initializes the attribute.
    /// </summary>
    /// <param name="relatedProperty">The name of the related property, possibly nested.</param>
    /// <exception cref="ArgumentException">If <paramref name="relatedProperty"/> is empty.</exception>
    protected ContingentValidationAttribute(string relatedProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relatedProperty);

        RelatedProperty = relatedProperty;
    }

    /// <summary>The name of the related property, possibly nested.</summary>
    public string RelatedProperty { get; }

    /// <summary>The name of the related property in error messages; overrides its display attributes.</summary>
    public string? RelatedPropertyDisplayName { get; set; }

    /// <inheritdoc />
    public override string DefaultErrorMessage => "{0} is invalid due to {1}.";

    /// <inheritdoc />
    public override bool IsValid(object? value, object container)
    {
        ArgumentNullException.ThrowIfNull(container);

        return IsValid(value, GetRelatedPropertyValue(container), container);
    }

    /// <summary>
    /// Validates <paramref name="value"/> against the value of the related property.
    /// </summary>
    /// <param name="value">The value of the validated property.</param>
    /// <param name="relatedPropertyValue">The value of the related property.</param>
    /// <param name="container">The object that declares the property.</param>
    /// <returns><see langword="true"/> if the value is valid.</returns>
    public abstract bool IsValid(object? value, object? relatedPropertyValue, object container);

    /// <summary>
    /// Arguments: <c>{0}</c> — the validated property, <c>{1}</c> — the related property
    /// (<see cref="RelatedPropertyDisplayName"/>, else <paramref name="relatedPropertyDisplayName"/>, else <see cref="RelatedProperty"/>).
    /// </summary>
    /// <inheritdoc />
    public override object?[] GetErrorMessageArguments(string name, string? relatedPropertyDisplayName)
        => [name, RelatedPropertyDisplayName ?? relatedPropertyDisplayName ?? RelatedProperty];

    /// <inheritdoc />
    protected override string? ResolveRelatedPropertyDisplayName(object container)
    {
        ArgumentNullException.ThrowIfNull(container);

        var property = GetPropertyPath(container.GetType())[^1];
        return property.GetCustomAttribute<DisplayAttribute>()?.GetName()
            ?? property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
    }

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> GetClientValidationParameters()
        => base.GetClientValidationParameters().Append(new("RelatedProperty", RelatedProperty));

    /// <summary>
    /// Whether a value is empty: <see langword="null"/>, a white-space string or an empty collection.
    /// </summary>
    /// <param name="propertyValue">The value.</param>
    /// <returns><see langword="true"/> if the value is empty.</returns>
    protected static bool PropertyIsEmpty(object? propertyValue) => propertyValue switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        IEnumerable items => !items.Cast<object>().Any(),
        _ => string.IsNullOrWhiteSpace(propertyValue.ToString()),
    };

    private object? GetRelatedPropertyValue(object container)
    {
        object? value = container;
        foreach (var property in GetPropertyPath(container.GetType()))
        {
            if (value is null)
            {
                return null;
            }
            value = property.GetValue(value);
        }
        return value;
    }

    private List<PropertyInfo> GetPropertyPath(Type containerType)
    {
        var path = new List<PropertyInfo>();
        var type = containerType;
        foreach (var name in RelatedProperty.Split('.'))
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Related property '{RelatedProperty}' was not found on type '{containerType.Name}'.");
            path.Add(property);
            type = property.PropertyType;
        }
        return path;
    }
}
