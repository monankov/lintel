using Lintel.Collections;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore;

/// <summary>
/// Asynchronous paging of EF Core queries.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Takes the page <paramref name="pageNumber"/> out of the query, executing a count query and a page query.
    /// </summary>
    /// <remarks>
    /// Same rules as the synchronous <c>ToPagedList</c> from <c>Lintel.Collections</c>: an empty result gives
    /// <see cref="PagedList.Empty{T}"/>, a <paramref name="pageSize"/> of 0 or less puts all items on one page.
    /// Order the query for the pages to be stable.
    /// </remarks>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="pageNumber">The one-based number of the page.</param>
    /// <param name="pageSize">The maximum number of items on a page; 0 or less for all items.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pageNumber"/> is less than 1.</exception>
    public static async Task<PagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);

        var totalItems = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalItems == 0)
        {
            return [];
        }

        if (pageSize <= 0)
        {
            var all = await source.ToListAsync(cancellationToken).ConfigureAwait(false);
            return new PagedList<T>(all, totalItems, pageNumber, totalItems);
        }

        var skip = (int)Math.Min((pageNumber - 1L) * pageSize, int.MaxValue);
        var items = await source.Skip(skip).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedList<T>(items, totalItems, pageNumber, pageSize);
    }
}
