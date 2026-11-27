using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Lintel.Domain;

/// <summary>
/// Base class for value objects: objects defined by their attributes rather than by an identity.
/// Two value objects are equal when all their public instance properties are equal.
/// </summary>
/// <remarks>
/// <para>
/// <typeparamref name="TValueObject"/> must be the deriving type itself: <c>class Address : ValueObject&lt;Address&gt;</c>.
/// Properties are taken from <typeparamref name="TValueObject"/>; indexers are ignored.
/// A property that holds another <typeparamref name="TValueObject"/> is compared by reference to avoid infinite recursion.
/// </para>
/// <para>For new code, a C# <c>record</c> gives the same value semantics without reflection.</para>
/// </remarks>
/// <typeparam name="TValueObject">The deriving value object type.</typeparam>
public class ValueObject<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TValueObject>
    : IEquatable<TValueObject>
    where TValueObject : ValueObject<TValueObject>
{
    private static readonly PropertyInfo[] _properties = typeof(TValueObject)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
        .ToArray();

    /// <inheritdoc />
    public virtual bool Equals(TValueObject? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        foreach (var property in _properties)
        {
            var left = property.GetValue(this);
            var right = property.GetValue(other);

            var equal = left is TValueObject
                ? ReferenceEquals(left, right)
                : Equals(left, right);

            if (!equal)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TValueObject other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var property in _properties)
        {
            var value = property.GetValue(this);
            hash.Add(value is TValueObject ? RuntimeHelpers.GetHashCode(value) : value?.GetHashCode() ?? 0);
        }
        return hash.ToHashCode();
    }

    /// <summary>Compares value objects by their properties.</summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or all properties are equal.</returns>
    public static bool operator ==(ValueObject<TValueObject>? left, ValueObject<TValueObject>? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>Compares value objects by their properties.</summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns><see langword="true"/> if any property differs.</returns>
    public static bool operator !=(ValueObject<TValueObject>? left, ValueObject<TValueObject>? right) => !(left == right);
}
