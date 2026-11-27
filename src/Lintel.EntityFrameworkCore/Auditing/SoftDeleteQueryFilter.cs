using Lintel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore;

/// <summary>
/// The named query filter that hides soft-deleted <see cref="ISoftDeletable"/> entities,
/// added by <see cref="ModelBuilderExtensions.ApplyAuditableConfiguration(ModelBuilder, Action{Options.AuditableConfigurationOptions}?)"/>.
/// </summary>
/// <remarks>
/// The filter is named, so it can be disabled alone while other query filters (for example multi-tenancy) stay active.
/// </remarks>
public static class SoftDeleteQueryFilter
{
    /// <summary>The name of the filter.</summary>
    public const string Name = "Lintel.SoftDelete";

    /// <summary>
    /// Includes soft-deleted entities in the query, keeping all other query filters.
    /// </summary>
    /// <typeparam name="T">The type of entities.</typeparam>
    /// <param name="source">The query.</param>
    /// <returns>The query without the soft-delete filter.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    public static IQueryable<T> IncludeSoftDeleted<T>(this IQueryable<T> source)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.IgnoreQueryFilters([Name]);
    }
}
