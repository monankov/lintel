namespace Lintel.Domain;

/// <summary>
/// Base class for entities that record when they were created and last modified.
/// </summary>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
public abstract class AuditableEntity<TEntity> : Entity<TEntity>, ICreatable, IModifiable
    where TEntity : AuditableEntity<TEntity>
{
    /// <inheritdoc />
    public DateTime DateCreated { get; protected set; }

    /// <inheritdoc />
    public DateTime DateModified { get; protected set; }
}

/// <summary>
/// Base class for entities with an identifier that record when they were created and last modified.
/// </summary>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
/// <typeparam name="TKey">The type of the identifier.</typeparam>
public abstract class AuditableEntity<TEntity, TKey> : Entity<TEntity, TKey>, ICreatable, IModifiable
    where TEntity : AuditableEntity<TEntity, TKey>
{
    /// <inheritdoc />
    public DateTime DateCreated { get; protected set; }

    /// <inheritdoc />
    public DateTime DateModified { get; protected set; }
}

/// <summary>
/// Base class for entities that record when and by whom they were created and last modified.
/// </summary>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
public abstract class SignedAuditableEntity<TEntity> : AuditableEntity<TEntity>, ISignedCreatable, ISignedModifiable
    where TEntity : SignedAuditableEntity<TEntity>
{
    /// <inheritdoc />
    public string? CreatedBy { get; protected set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; protected set; }
}

/// <summary>
/// Base class for entities with an identifier that record when and by whom they were created and last modified.
/// </summary>
/// <typeparam name="TEntity">The deriving entity type.</typeparam>
/// <typeparam name="TKey">The type of the identifier.</typeparam>
public abstract class SignedAuditableEntity<TEntity, TKey> : AuditableEntity<TEntity, TKey>, ISignedCreatable, ISignedModifiable
    where TEntity : SignedAuditableEntity<TEntity, TKey>
{
    /// <inheritdoc />
    public string? CreatedBy { get; protected set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; protected set; }
}
