using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Lintel.DataAnnotations;

/// <summary>
/// The comparison logic and message fragment of an <see cref="Operator"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Numbers of different types (<see cref="int"/> and <see cref="long"/>, <see cref="decimal"/> and <see cref="double"/>, ...)
/// are compared by value.</item>
/// <item>Regular expressions are evaluated with a timeout of <see cref="RegexTimeout"/>; a timeout counts as no match.</item>
/// <item>For <see cref="Operator.In"/> and <see cref="Operator.NotIn"/> a string is a single value, not a list of characters.</item>
/// </list>
/// </remarks>
public sealed class OperatorMetadata
{
    /// <summary>The maximum time a regular expression may run.</summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly FrozenDictionary<Operator, OperatorMetadata> _metadata = new Dictionary<Operator, OperatorMetadata>
    {
        [Operator.EqualTo] = new("equal to", AreEqual),
        [Operator.NotEqualTo] = new("not equal to", (value, other) => !AreEqual(value, other)),
        [Operator.GreaterThan] = new("greater than", (value, other) => Compare(value, other) is > 0),
        [Operator.LessThan] = new("less than", (value, other) => Compare(value, other) is < 0),
        [Operator.GreaterThanOrEqualTo] = new("greater than or equal to",
            (value, other) => (value is null && other is null) || AreEqual(value, other) || Compare(value, other) is > 0),
        [Operator.LessThanOrEqualTo] = new("less than or equal to",
            (value, other) => (value is null && other is null) || AreEqual(value, other) || Compare(value, other) is < 0),
        [Operator.RegExMatch] = new("a match to", IsMatch),
        [Operator.NotRegExMatch] = new("not a match to", (value, pattern) => pattern is not null && !IsMatch(value, pattern)),
        [Operator.In] = new("in", IsIn),
        [Operator.NotIn] = new("not in", (value, items) => !IsIn(value, items)),
    }.ToFrozenDictionary();

    private OperatorMetadata(string errorMessage, Func<object?, object?, bool> isValid)
    {
        ErrorMessage = errorMessage;
        IsValid = isValid;
    }

    /// <summary>The fragment used in default error messages, for example <c>greater than</c>.</summary>
    public string ErrorMessage { get; }

    /// <summary>Checks a value against the other value (a related property value, a pattern or a list).</summary>
    public Func<object?, object?, bool> IsValid { get; }

    /// <summary>
    /// Returns the metadata of an operator.
    /// </summary>
    /// <param name="operator">The operator.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="operator"/> is not a defined value.</exception>
    public static OperatorMetadata Get(Operator @operator)
        => _metadata.TryGetValue(@operator, out var metadata)
            ? metadata
            : throw new ArgumentOutOfRangeException(nameof(@operator), @operator, "Unknown operator.");

    private static bool AreEqual(object? value, object? other)
    {
        if (value is null || other is null)
        {
            return value is null && other is null;
        }
        if (TryToDecimal(value, out var left) && TryToDecimal(other, out var right))
        {
            return left == right;
        }
        return value.Equals(other);
    }

    private static int? Compare(object? value, object? other)
    {
        if (value is null || other is null)
        {
            return null;
        }
        if (TryToDecimal(value, out var left) && TryToDecimal(other, out var right))
        {
            return left.CompareTo(right);
        }
        if (value.GetType() == other.GetType() || value is IComparable)
        {
            try
            {
                return Comparer<object>.Default.Compare(value, other);
            }
            catch (ArgumentException)
            {
                return null; // values of incomparable types
            }
        }
        return null;
    }

    private static bool IsMatch(object? value, object? pattern)
    {
        if (pattern is null)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(pattern, CultureInfo.InvariantCulture)!,
                RegexOptions.None,
                RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool IsIn(object? value, object? items)
    {
        if (items is IEnumerable list and not string)
        {
            foreach (var item in list)
            {
                if (AreEqual(value, item))
                {
                    return true;
                }
            }
            return false;
        }
        return AreEqual(value, items);
    }

    private static bool TryToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case float or double:
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsFinite(number) && Math.Abs(number) < (double)decimal.MaxValue)
                {
                    result = (decimal)number;
                    return true;
                }
                break;
        }
        result = default;
        return false;
    }
}
