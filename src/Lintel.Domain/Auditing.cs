namespace Lintel.Domain;

/// <summary>
/// Marker interface for entities with audit information.
/// </summary>
public interface IAuditable;

/// <summary>
/// An entity that records when it was created.
/// </summary>
public interface ICreatable : IAuditable
{
    /// <summary>The moment the entity was created.</summary>
    DateTime DateCreated { get; }
}

/// <summary>
/// An entity that records when it was last modified.
/// </summary>
public interface IModifiable : IAuditable
{
    /// <summary>The moment the entity was last modified.</summary>
    DateTime DateModified { get; }
}

/// <summary>
/// An entity that records who created it.
/// </summary>
public interface ISignedCreatable : ICreatable
{
    /// <summary>The user who created the entity, or <see langword="null"/> if unknown.</summary>
    string? CreatedBy { get; }
}

/// <summary>
/// An entity that records who last modified it.
/// </summary>
public interface ISignedModifiable : IModifiable
{
    /// <summary>The user who last modified the entity, or <see langword="null"/> if unknown.</summary>
    string? ModifiedBy { get; }
}

/// <summary>
/// An entity that must never be deleted.
/// </summary>
public interface IUndeletable : IAuditable;

/// <summary>
/// An entity that is marked as deleted instead of being removed.
/// </summary>
public interface ISoftDeletable : IUndeletable
{
    /// <summary>The moment the entity was deleted, or <see langword="null"/> if it is not deleted.</summary>
    DateTime? DateDeleted { get; }
}

/// <summary>
/// A soft-deletable entity that records who deleted it.
/// </summary>
public interface ISignedSoftDeletable : ISoftDeletable
{
    /// <summary>The user who deleted the entity, or <see langword="null"/> if unknown or not deleted.</summary>
    string? DeletedBy { get; }
}

/// <summary>
/// Names of the audit properties, for configuring persistence (column names, shadow properties).
/// </summary>
public static class AuditableProperties
{
    /// <summary><see cref="ICreatable.DateCreated"/>.</summary>
    public const string DateCreated = nameof(ICreatable.DateCreated);

    /// <summary><see cref="ISignedCreatable.CreatedBy"/>.</summary>
    public const string CreatedBy = nameof(ISignedCreatable.CreatedBy);

    /// <summary><see cref="IModifiable.DateModified"/>.</summary>
    public const string DateModified = nameof(IModifiable.DateModified);

    /// <summary><see cref="ISignedModifiable.ModifiedBy"/>.</summary>
    public const string ModifiedBy = nameof(ISignedModifiable.ModifiedBy);

    /// <summary><see cref="ISoftDeletable.DateDeleted"/>.</summary>
    public const string DateDeleted = nameof(ISoftDeletable.DateDeleted);

    /// <summary><see cref="ISignedSoftDeletable.DeletedBy"/>.</summary>
    public const string DeletedBy = nameof(ISignedSoftDeletable.DeletedBy);
}
