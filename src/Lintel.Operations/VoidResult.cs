namespace Lintel.Operations;

/// <summary>
/// Represents the absence of a result, since <see cref="Void"/> is not a valid generic argument.
/// </summary>
public readonly struct VoidResult : IEquatable<VoidResult>, IComparable<VoidResult>, IComparable
{
    private static readonly VoidResult _value;

    /// <summary>
    /// The default and only value of the <see cref="VoidResult"/> type.
    /// </summary>
    public static ref readonly VoidResult Value => ref _value;

    /// <summary>
    /// A completed task whose result is <see cref="Value"/>.
    /// </summary>
    public static readonly Task<VoidResult> TaskValue = Task.FromResult(_value);

    /// <inheritdoc />
    public bool Equals(VoidResult other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is VoidResult;

    /// <inheritdoc />
    public int CompareTo(VoidResult other) => 0;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">If <paramref name="obj"/> is not a <see cref="VoidResult"/>.</exception>
    public int CompareTo(object? obj) => obj is VoidResult ? 0 : throw new InvalidOperationException();

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <summary>Always <see langword="true"/>.</summary>
    public static bool operator ==(VoidResult left, VoidResult right) => true;

    /// <summary>Always <see langword="false"/>.</summary>
    public static bool operator !=(VoidResult left, VoidResult right) => false;

    /// <summary>Always <see langword="false"/>.</summary>
    public static bool operator <(VoidResult left, VoidResult right) => false;

    /// <summary>Always <see langword="true"/>.</summary>
    public static bool operator <=(VoidResult left, VoidResult right) => true;

    /// <summary>Always <see langword="false"/>.</summary>
    public static bool operator >(VoidResult left, VoidResult right) => false;

    /// <summary>Always <see langword="true"/>.</summary>
    public static bool operator >=(VoidResult left, VoidResult right) => true;

    /// <summary>Returns an empty string.</summary>
    public override string ToString() => "";
}
