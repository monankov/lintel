using System.Globalization;

namespace Lintel.Primitives;

/// <summary>
/// Formatting numbers.
/// </summary>
public static class NumberExtensions
{
    private static readonly (long Divisor, string Suffix)[] _units = [(1_000, "K"), (1_000_000, "M"), (1_000_000_000, "B"), (1_000_000_000_000, "T")];

    /// <summary>
    /// Formats a number compactly for counters and badges: <c>999</c>, <c>1.2K</c>, <c>12K</c>, <c>123K</c>, <c>1.2M</c>.
    /// </summary>
    /// <remarks>
    /// Values from a thousand get the suffix <c>K</c>, <c>M</c>, <c>B</c> or <c>T</c> with one decimal below 10 units
    /// and none from 10 units. The value is rounded, and a rounded value that reaches the next unit uses it
    /// (<c>999 950</c> is <c>1M</c>, not <c>1000K</c>). Negative values keep their sign. The decimal separator and
    /// the group separator of values below a thousand come from <paramref name="provider"/>.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="provider">The culture; by default <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The formatted value.</returns>
    public static string ToCompactString(this long value, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        if (value is > -1000 and < 1000)
        {
            return value.ToString("#,0", provider);
        }

        // decimal keeps long.MinValue and exact tenths. Units go up while the rounded value reaches 1000 of the unit.
        var magnitude = Math.Abs((decimal)value);
        var unit = 0;
        var rounded = Round(magnitude / _units[unit].Divisor);
        while (rounded >= 1000 && unit < _units.Length - 1)
        {
            unit++;
            rounded = Round(magnitude / _units[unit].Divisor);
        }

        return (value < 0 ? "-" : "") + rounded.ToString(rounded < 10 ? "0.#" : "#,0", provider) + _units[unit].Suffix;
    }

    private static decimal Round(decimal scaled) => Math.Round(scaled, scaled < 10 ? 1 : 0, MidpointRounding.AwayFromZero);

    /// <summary>Formats a number compactly; see <see cref="ToCompactString(long, IFormatProvider?)"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="provider">The culture; by default <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The formatted value.</returns>
    public static string ToCompactString(this int value, IFormatProvider? provider = null) => ((long)value).ToCompactString(provider);
}
