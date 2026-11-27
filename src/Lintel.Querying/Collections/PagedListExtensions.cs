namespace Lintel.Collections;

/// <summary>
/// Creation and conversion of <see cref="PagedList{T}"/>.
/// </summary>
public static class PagedListExtensions
{
    /// <summary>
    /// Creates a page from items that are already sliced out of the whole set.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="items">The items of this page.</param>
    /// <param name="totalItems">The number of items in the whole set.</param>
    /// <param name="pageNumber">The one-based number of the page.</param>
    /// <param name="pageSize">The maximum number of items on a page.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the paging values are inconsistent.</exception>
    public static PagedList<T> ToPagedList<T>(this IEnumerable<T> items, int totalItems, int pageNumber, int pageSize)
        => new(items, totalItems, pageNumber, pageSize);

    /// <summary>
    /// Takes the page <paramref name="pageNumber"/> out of the whole sequence <paramref name="source"/>.
    /// </summary>
    /// <remarks>
    /// An empty source gives <see cref="PagedList.Empty{T}"/>. A <paramref name="pageSize"/> of 0 or less
    /// puts all items on one page. A page number past the last page gives a page without items.
    /// </remarks>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The whole sequence.</param>
    /// <param name="pageNumber">The one-based number of the page.</param>
    /// <param name="pageSize">The maximum number of items on a page; 0 or less for all items.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pageNumber"/> is less than 1.</exception>
    public static PagedList<T> ToPagedList<T>(this IEnumerable<T> source, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);

        // Avoid enumerating a lazy sequence twice.
        if (!source.TryGetNonEnumeratedCount(out var totalItems))
        {
            var materialized = source.ToList();
            source = materialized;
            totalItems = materialized.Count;
        }

        if (totalItems == 0)
        {
            return PagedList.Empty<T>();
        }

        if (pageSize <= 0)
        {
            return new PagedList<T>(source, totalItems, pageNumber, totalItems);
        }

        var items = source.Skip(Skip(pageNumber, pageSize)).Take(pageSize);
        return new PagedList<T>(items, totalItems, pageNumber, pageSize);
    }

    /// <summary>
    /// Takes the page <paramref name="pageNumber"/> out of the query <paramref name="source"/>,
    /// executing a count query and a page query.
    /// </summary>
    /// <remarks>
    /// Same rules as <see cref="ToPagedList{T}(IEnumerable{T}, int, int)"/>. The query should be ordered
    /// for the pages to be stable. For EF Core use <c>ToPagedListAsync</c> from <c>Lintel.EntityFrameworkCore</c>.
    /// </remarks>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="pageNumber">The one-based number of the page.</param>
    /// <param name="pageSize">The maximum number of items on a page; 0 or less for all items.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pageNumber"/> is less than 1.</exception>
    public static PagedList<T> ToPagedList<T>(this IQueryable<T> source, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);

        var totalItems = source.Count();
        if (totalItems == 0)
        {
            return PagedList.Empty<T>();
        }

        if (pageSize <= 0)
        {
            return new PagedList<T>(source.ToList(), totalItems, pageNumber, totalItems);
        }

        var items = source.Skip(Skip(pageNumber, pageSize)).Take(pageSize).ToList();
        return new PagedList<T>(items, totalItems, pageNumber, pageSize);
    }

    /// <summary>
    /// Creates a page of other items with the metadata of <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="TDestination">The type of destination items.</typeparam>
    /// <param name="source">The page whose metadata is copied.</param>
    /// <param name="destination">The items of the new page.</param>
    /// <returns>The new page.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static PagedList<TDestination> ToPagedList<TDestination>(this IPagedEnumerable source, IEnumerable<TDestination> destination)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PagedList<TDestination>(destination, source.TotalItems, source.PageNumber, source.PageSize);
    }

    /// <summary>
    /// Converts a page to a serializable <see cref="PagedResult{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The page.</param>
    /// <returns>The serializable page.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    public static PagedResult<T> ToPagedResult<T>(this IPagedList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PagedResult<T>([.. source], source.TotalItems, source.PageNumber, source.PageSize);
    }

    /// <summary>
    /// Converts a serializable <see cref="PagedResult{T}"/> back to a <see cref="PagedList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="source">The serializable page.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the paging values are inconsistent.</exception>
    public static PagedList<T> ToPagedList<T>(this PagedResult<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PagedList<T>(source.Items ?? [], source.TotalItems, source.PageNumber, source.PageSize);
    }

    private static int Skip(int pageNumber, int pageSize)
        => (int)Math.Min((pageNumber - 1L) * pageSize, int.MaxValue);
}
