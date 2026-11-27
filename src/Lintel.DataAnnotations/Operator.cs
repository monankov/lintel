namespace Lintel.DataAnnotations;

/// <summary>
/// The comparison applied by <see cref="IsAttribute"/> and <see cref="RequiredIfAttribute"/>.
/// </summary>
public enum Operator
{
    /// <summary>Values are equal.</summary>
    EqualTo,

    /// <summary>Values are not equal.</summary>
    NotEqualTo,

    /// <summary>The value is greater.</summary>
    GreaterThan,

    /// <summary>The value is less.</summary>
    LessThan,

    /// <summary>The value is greater or equal.</summary>
    GreaterThanOrEqualTo,

    /// <summary>The value is less or equal.</summary>
    LessThanOrEqualTo,

    /// <summary>The value matches the regular expression.</summary>
    RegExMatch,

    /// <summary>The value does not match the regular expression.</summary>
    NotRegExMatch,

    /// <summary>The value equals one of the items (or the single value).</summary>
    In,

    /// <summary>The value equals none of the items (nor the single value).</summary>
    NotIn,
}
