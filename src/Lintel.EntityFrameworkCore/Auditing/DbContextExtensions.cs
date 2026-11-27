using Lintel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Lintel.EntityFrameworkCore;

/// <summary>
/// Auditing of tracked entities before saving.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Fills the audit properties of added, modified and deleted <see cref="IAuditable"/> entities
    /// and turns deletion of <see cref="IUndeletable"/> entities into soft deletion.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Added: <c>DateCreated</c>/<c>CreatedBy</c> and <c>DateModified</c>/<c>ModifiedBy</c> are set, <c>DateDeleted</c>/<c>DeletedBy</c> are cleared.</item>
    /// <item>Modified: <c>DateModified</c>/<c>ModifiedBy</c> are set; <c>DateCreated</c>/<c>CreatedBy</c> are protected from changes.</item>
    /// <item>Deleted <see cref="IUndeletable"/>: the entity (and its owned entities) is kept; for <see cref="ISoftDeletable"/>
    /// <c>DateDeleted</c>/<c>DeletedBy</c> are set instead.</item>
    /// </list>
    /// Call it before <c>SaveChanges</c>, or register <see cref="AuditSaveChangesInterceptor"/> to do it automatically.
    /// </remarks>
    /// <param name="context">The context.</param>
    /// <param name="timestampProvider">Supplies the timestamp; <see cref="DateTime.Now"/> by default.</param>
    /// <param name="signProvider">Supplies the user name; <see langword="null"/> by default.</param>
    /// <returns>The same context.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static DbContext Audit(this DbContext context,
        AuditTimestampProvider? timestampProvider = null,
        AuditSignProvider? signProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ChangeTracker.DetectChanges();

        var entries = context.ChangeTracker.Entries<IAuditable>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
        {
            return context;
        }

        var timestamp = timestampProvider?.Invoke() ?? DateTime.Now;
        var sign = signProvider?.Invoke();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    OnAdded(entry, timestamp, sign);
                    break;
                case EntityState.Modified:
                    OnModified(entry, timestamp, sign);
                    break;
                case EntityState.Deleted:
                    OnDeleted(entry, timestamp, sign);
                    break;
            }
        }

        return context;
    }

    private static void OnAdded(EntityEntry entry, DateTime timestamp, string? sign)
    {
        var entity = entry.Entity;

        if (entity is ICreatable)
        {
            entry.Property(AuditableProperties.DateCreated).CurrentValue = timestamp;
            if (entity is ISignedCreatable)
            {
                entry.Property(AuditableProperties.CreatedBy).CurrentValue = sign;
            }
        }
        if (entity is IModifiable)
        {
            entry.Property(AuditableProperties.DateModified).CurrentValue = timestamp;
            if (entity is ISignedModifiable)
            {
                entry.Property(AuditableProperties.ModifiedBy).CurrentValue = sign;
            }
        }
        if (entity is ISoftDeletable)
        {
            entry.Property(AuditableProperties.DateDeleted).CurrentValue = null;
            if (entity is ISignedSoftDeletable)
            {
                entry.Property(AuditableProperties.DeletedBy).CurrentValue = null;
            }
        }
    }

    private static void OnModified(EntityEntry entry, DateTime timestamp, string? sign)
    {
        var entity = entry.Entity;

        ProtectCreation(entry);

        if (entity is IModifiable)
        {
            entry.Property(AuditableProperties.DateModified).CurrentValue = timestamp;
            if (entity is ISignedModifiable)
            {
                entry.Property(AuditableProperties.ModifiedBy).CurrentValue = sign;
            }
        }
    }

    private static void OnDeleted(EntityEntry entry, DateTime timestamp, string? sign)
    {
        var entity = entry.Entity;
        if (entity is not IUndeletable)
        {
            return;
        }

        entry.State = EntityState.Unchanged;
        RestoreOwned(entry);

        if (entity is ISoftDeletable)
        {
            var dateDeleted = entry.Property(AuditableProperties.DateDeleted);
            dateDeleted.CurrentValue = timestamp;
            dateDeleted.IsModified = true;

            if (entity is ISignedSoftDeletable)
            {
                var deletedBy = entry.Property(AuditableProperties.DeletedBy);
                deletedBy.CurrentValue = sign;
                deletedBy.IsModified = true;
            }
        }
    }

    private static void ProtectCreation(EntityEntry entry)
    {
        if (entry.Entity is ICreatable)
        {
            entry.Property(AuditableProperties.DateCreated).IsModified = false;
            if (entry.Entity is ISignedCreatable)
            {
                entry.Property(AuditableProperties.CreatedBy).IsModified = false;
            }
        }
    }

    // EF Core marks owned entities as Deleted together with their owner; a soft-deleted owner keeps them.
    private static void RestoreOwned(EntityEntry entry)
    {
        foreach (var navigation in entry.Navigations)
        {
            if (!navigation.Metadata.TargetEntityType.IsOwned())
            {
                continue;
            }

            var ownedEntries = navigation switch
            {
                ReferenceEntry reference when reference.TargetEntry is not null => [reference.TargetEntry],
                CollectionEntry collection when collection.CurrentValue is not null =>
                    collection.CurrentValue.Cast<object>().Select(item => collection.FindEntry(item)).OfType<EntityEntry>().ToList(),
                _ => [],
            };

            foreach (var ownedEntry in ownedEntries.Where(owned => owned.State == EntityState.Deleted))
            {
                ownedEntry.State = EntityState.Unchanged;
                RestoreOwned(ownedEntry);
            }
        }
    }
}
