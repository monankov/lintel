namespace Lintel.Domain;

/// <summary>
/// Base class for entities identified by several values (a composite key).
/// </summary>
/// <remarks>
/// Return the key from <see cref="GetCompositeId"/> as an object with value equality:
/// an anonymous object (<c>new { UserId, RoleId }</c>), a tuple or a record.
/// A composite entity is never transient.
/// </remarks>
/// <example>
/// <code>
/// public class UserRole : CompositeEntity&lt;UserRole&gt;
/// {
///     public int UserId { get; private set; }
///     public int RoleId { get; private set; }
///
///     // Reusable in EF Core: builder.HasKey(UserRole.CompositeId)
///     public static Expression&lt;Func&lt;UserRole, object?&gt;&gt; CompositeId =&gt; x =&gt; new { x.UserId, x.RoleId };
///
///     private static readonly Func&lt;UserRole, object?&gt; _compositeId = CompositeId.Compile();
///
///     protected override object? GetCompositeId() =&gt; _compositeId(this);
/// }
/// </code>
/// </example>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
public abstract class CompositeEntity<TEntity> : Entity<TEntity>
    where TEntity : CompositeEntity<TEntity>
{
    /// <summary>
    /// Initializes a new instance of the entity.
    /// </summary>
    protected CompositeEntity()
    {
    }

    /// <summary>
    /// Returns the composite identifier as an object with value equality (anonymous object, tuple or record).
    /// </summary>
    /// <returns>The composite identifier.</returns>
    protected abstract object? GetCompositeId();

    /// <inheritdoc />
    public override bool Equals(TEntity? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        var compositeId = GetCompositeId();
        var otherCompositeId = other.GetCompositeId();

        return compositeId is not null && compositeId.Equals(otherCompositeId);
    }

    /// <inheritdoc />
    protected override int GenerateHashCode() => GetCompositeId()?.GetHashCode() ?? 0;

    /// <inheritdoc />
    public override bool IsTransient() => false;

    /// <inheritdoc />
    public override string ToString()
    {
        var compositeId = GetCompositeId();
        return compositeId is null ? GetType().Name : $"{GetType().Name} {compositeId}";
    }
}
