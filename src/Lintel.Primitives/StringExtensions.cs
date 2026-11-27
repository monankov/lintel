using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Lintel.Primitives;

/// <summary>
/// Conversion of strings to other types, for example values from configuration, query strings or claims.
/// </summary>
public static class StringExtensions
{
    private const string TrimmingMessage = "Types that are not IConvertible are converted with TypeDescriptor, whose converters may be trimmed. Prefer T.Parse for known types.";

    /// <summary>
    /// Converts a string to <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="string"/> is returned as is.</item>
    /// <item>An empty or white-space string converts to <see langword="null"/> for nullable value types and reference types.</item>
    /// <item>Enums are parsed by name or number (case-sensitive).</item>
    /// <item><see cref="IConvertible"/> types (numbers, <see cref="bool"/>, <see cref="DateTime"/>, ...) use <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.</item>
    /// <item>Other types (<see cref="Guid"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="TimeSpan"/>, <see cref="DateTimeOffset"/>, <see cref="Uri"/>, ...) use their <see cref="TypeConverter"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="value">The string to convert.</param>
    /// <param name="type">The target type.</param>
    /// <param name="throwIfInvalidCast"><see langword="true"/> to throw on failure; <see langword="false"/> to return <see langword="null"/>.</param>
    /// <param name="provider">The culture-specific format; <see cref="CultureInfo.CurrentCulture"/> by default.</param>
    /// <returns>The converted value, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidCastException">If the conversion fails and <paramref name="throwIfInvalidCast"/> is <see langword="true"/>.</exception>
    [RequiresUnreferencedCode(TrimmingMessage)]
    public static object? Cast(this string? value, Type type, bool throwIfInvalidCast = false, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type == typeof(string))
        {
            return value;
        }

        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (string.IsNullOrWhiteSpace(value) && (targetType != type || !type.IsValueType))
            {
                return null;
            }

            ArgumentNullException.ThrowIfNull(value);
            provider ??= CultureInfo.CurrentCulture;

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value);
            }
            if (typeof(IConvertible).IsAssignableFrom(targetType))
            {
                return Convert.ChangeType(value, targetType, provider);
            }

            return TypeDescriptor.GetConverter(targetType)
                .ConvertFromString(null, provider as CultureInfo ?? CultureInfo.CurrentCulture, value);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (throwIfInvalidCast)
            {
                throw new InvalidCastException($"Error with converting string \"{value}\" to type \"{targetType.Name}\"", ex);
            }

            return null;
        }
    }

    /// <summary>
    /// Converts a string to <typeparamref name="T"/>. See <see cref="Cast(string?, Type, bool, IFormatProvider?)"/> for the rules.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The string to convert.</param>
    /// <param name="throwIfInvalidCast"><see langword="true"/> to throw on failure; <see langword="false"/> to return <c>default</c>.</param>
    /// <param name="provider">The culture-specific format; <see cref="CultureInfo.CurrentCulture"/> by default.</param>
    /// <returns>The converted value, or <c>default</c>.</returns>
    /// <exception cref="InvalidCastException">If the conversion fails and <paramref name="throwIfInvalidCast"/> is <see langword="true"/>.</exception>
    [RequiresUnreferencedCode(TrimmingMessage)]
    public static T? Cast<T>(this string? value, bool throwIfInvalidCast = false, IFormatProvider? provider = null)
        => value.Cast(typeof(T), throwIfInvalidCast, provider) is T result ? result : default;
}
