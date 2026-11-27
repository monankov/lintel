using System.Diagnostics.CodeAnalysis;

namespace Lintel.Domain;

/// <summary>
/// Base class for entities: objects defined by their identity rather than by their attributes.
/// </summary>
/// <remarks>
/// <para>
/// <typeparamref name="TEntity"/> must be the deriving type itself:
/// <c>class Order : Entity&lt;Order&gt;</c>. This is checked in the constructor.
/// </para>
/// <para>
/// The hash code is computed once and then cached for the lifetime of the object, so an entity
/// stays findable in a <see cref="HashSet{T}"/> even after it gets its identifier on save.
/// The consequence: a transient entity that was hashed before saving keeps a reference-based hash code,
/// which differs from the hash code of another instance loaded with the same identifier.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
public abstract class Entity<TEntity> : IEquatable<TEntity>
    where TEntity : Entity<TEntity>
{
    /// <summary>
    /// The cached hash code, or <see langword="null"/> while it has not been computed.
    /// </summary>
    protected int? CachedHashCode;

    /// <summary>
    /// Initializes a new instance of the entity.
    /// </summary>
    /// <exception cref="InvalidOperationException">If <typeparamref name="TEntity"/> is not the deriving type.</exception>
    [SuppressMessage("Major Code Smell", "S3060", Justification = "Checks the self-referencing generic argument, not a subtype.")]
    protected Entity()
    {
        if (this is not TEntity)
        {
            throw new InvalidOperationException(
                $"Entity \"{GetType()}\" specifies \"{typeof(TEntity).Name}\" as generic argument, but it should be its own type.");
        }
    }

    /// <summary>
    /// Whether the entity has no identity yet (usually: it has not been saved).
    /// </summary>
    /// <returns><see langword="true"/> if the entity is transient.</returns>
    public abstract bool IsTransient();

    /// <summary>
    /// Compares entities by identity.
    /// </summary>
    /// <param name="other">The entity to compare with.</param>
    /// <returns><see langword="true"/> if both entities have the same identity.</returns>
    public abstract bool Equals(TEntity? other);

    /// <summary>
    /// Computes the hash code from the identity. Called once, for a non-transient entity.
    /// </summary>
    /// <returns>The hash code.</returns>
    protected abstract int GenerateHashCode();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as TEntity);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        CachedHashCode ??= IsTransient() ? base.GetHashCode() : GenerateHashCode();
        return CachedHashCode.Value;
    }

    /// <summary>Compares entities by identity.</summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true"/> if both are <see langword="null"/> or have the same identity.</returns>
    public static bool operator ==(Entity<TEntity>? left, Entity<TEntity>? right) => Equals(left, right);

    /// <summary>Compares entities by identity.</summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true"/> if the entities have different identities.</returns>
    public static bool operator !=(Entity<TEntity>? left, Entity<TEntity>? right) => !Equals(left, right);
}

/// <summary>
/// Base class for entities identified by an <see cref="Id"/> of type <typeparamref name="TKey"/>.
/// </summary>
/// <remarks>
/// An entity whose <see cref="Id"/> equals <c>default(TKey)</c> is transient. Two transient entities are equal
/// only if they are the same instance. Entities of related types (for example an entity and its EF Core
/// lazy-loading proxy) with the same identifier are equal.
/// </remarks>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
/// <typeparam name="TKey">The type of the identifier.</typeparam>
public abstract class Entity<TEntity, TKey> : Entity<TEntity>
    where TEntity : Entity<TEntity, TKey>
{
    /// <summary>
    /// The identifier of the entity.
    /// </summary>
    public virtual TKey Id { get; protected set; } = default!;

    /// <summary>
    /// Initializes a new instance of the entity.
    /// </summary>
    protected Entity()
    {
    }

    /// <inheritdoc />
    public override bool IsTransient() => EqualityComparer<TKey>.Default.Equals(Id, default);

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

        var type = GetType();
        var otherType = other.GetType();
        if (!type.IsAssignableFrom(otherType) && !otherType.IsAssignableFrom(type))
        {
            return false;
        }

        if (IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EqualityComparer<TKey>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    protected override int GenerateHashCode() => HashCode.Combine(typeof(TEntity), Id);

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name} [{nameof(Id)}={Id}]";
}
