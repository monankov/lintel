using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace Lintel.Primitives;

/// <summary>
/// Human-readable names of enum values from <see cref="DisplayAttribute"/> and <see cref="DescriptionAttribute"/>.
/// </summary>
/// <remarks>
/// Values without the attribute, combined flags and undefined values fall back to <see cref="Enum.ToString()"/>.
/// The generic overloads are used for typed enum values and are safe for trimming and AOT.
/// </remarks>
public static class EnumExtensions
{
    private const string TrimmingMessage = "Reads attributes of enum fields; the fields of the runtime enum type may be trimmed. Use the generic overload with a typed enum value.";

    private static readonly ConcurrentDictionary<Type, ResourceManager> _resourceManagers = new();

    /// <summary>
    /// Returns <see cref="DescriptionAttribute.Description"/> of the value, or its name.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The description.</returns>
    public static string GetDescription<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(this TEnum value)
        where TEnum : struct, Enum
        => Describe(typeof(TEnum).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static), value);

    /// <summary>
    /// Returns <see cref="DescriptionAttribute.Description"/> of the value, or its name.
    /// </summary>
    /// <param name="value">The enum value.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode(TrimmingMessage)]
    public static string GetDescription(this Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Describe(value.GetType().GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static), value);
    }

    /// <summary>
    /// Returns <see cref="DisplayAttribute.Name"/> of the value (localized when <see cref="DisplayAttribute.ResourceType"/> is set), or its name.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The display name.</returns>
    public static string GetDisplayName<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(this TEnum value)
        where TEnum : struct, Enum
        => Display(typeof(TEnum).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static), value);

    /// <summary>
    /// Returns <see cref="DisplayAttribute.Name"/> of the value (localized when <see cref="DisplayAttribute.ResourceType"/> is set), or its name.
    /// </summary>
    /// <param name="value">The enum value.</param>
    /// <returns>The display name.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode(TrimmingMessage)]
    public static string GetDisplayName(this Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Display(value.GetType().GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static), value);
    }

    private static string Describe(FieldInfo? field, Enum value)
        => field?.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description ?? value.ToString();

    private static string Display(FieldInfo? field, Enum value)
    {
        var attribute = field?.GetCustomAttribute<DisplayAttribute>(inherit: false);
        if (attribute?.Name is null)
        {
            return value.ToString();
        }

        if (attribute.ResourceType is null)
        {
            return attribute.Name;
        }

        // ResourceManager works with internal resource classes (the .resx default), unlike DisplayAttribute.GetName().
        var resourceManager = _resourceManagers.GetOrAdd(attribute.ResourceType,
            static type => new ResourceManager(type.FullName!, type.Assembly));

        return resourceManager.GetString(attribute.Name) ?? attribute.Name;
    }
}
