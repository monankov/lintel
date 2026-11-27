using System.Collections;

namespace Lintel.Collections;

/// <summary>
/// An immutable page of items from a larger set, with paging metadata.
/// </summary>
/// <remarks>
/// A <see cref="PagedList{T}"/> serializes to JSON as a plain array. To keep the metadata,
/// convert it with <see cref="PagedListExtensions.ToPagedResult{T}(IPagedList{T})"/>.
/// </remarks>
/// <typeparam name="T">The type of items.</typeparam>
public class PagedList<T> : IPagedList<T>
{
    private readonly List<T> _items;

    /// <inheritdoc />
    public int TotalItems { get; }

    /// <inheritdoc />
    public int PageNumber { get; }

    /// <inheritdoc />
    public int PageSize { get; }

    /// <inheritdoc />
    public int TotalPages { get; }

    /// <inheritdoc />
    public bool HasPreviousPage { get; }

    /// <inheritdoc />
    public bool HasNextPage { get; }

    /// <inheritdoc />
    public bool IsFirstPage { get; }

    /// <inheritdoc />
    public bool IsLastPage { get; }

    /// <inheritdoc />
    public int FirstItemOnPage { get; }

    /// <inheritdoc />
    public int LastItemOnPage { get; }

    /// <summary>Gets the item at <paramref name="index"/> on this page.</summary>
    /// <param name="index">The zero-based index on the page.</param>
    public T this[int index] => _items[index];

    /// <summary>The number of items on this page.</summary>
    public int Count => _items.Count;

    /// <summary>
    /// Initializes an empty first page.
    /// </summary>
    public PagedList() : this([], 0, 1, 0)
    {
    }

    /// <summary>
    /// Initializes a page from items that are already sliced out of the whole set.
    /// </summary>
    /// <param name="items">The items of this page.</param>
    /// <param name="totalItems">The number of items in the whole set.</param>
    /// <param name="pageNumber">The one-based number of the page.</param>
    /// <param name="pageSize">The maximum number of items on a page.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the paging values are inconsistent.</exception>
    public PagedList(IEnumerable<T> items, int totalItems, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);

        var list = items.ToList();

        if (totalItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalItems), totalItems,
                "Total number of elements in superset must be greater or equal 0");
        }
        if (totalItems < list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(totalItems), totalItems,
                "Total number of elements in superset must be greater or equal collection items count");
        }
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber,
                "Page number must be greater or equal 1");
        }
        if (pageSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize,
                "Page size must be greater or equal 0");
        }
        if (pageSize < list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize,
                "Page size must be greater or equal collection items count");
        }

        _items = list;

        TotalItems = totalItems;
        PageNumber = pageNumber;
        PageSize = pageSize;

        TotalPages = totalItems > 0 && pageSize > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 0;
        HasPreviousPage = pageNumber > 1 && pageNumber <= TotalPages;
        HasNextPage = pageNumber < TotalPages;
        IsFirstPage = TotalPages > 0 && pageNumber == 1;
        IsLastPage = TotalPages > 0 && pageNumber == TotalPages;
        FirstItemOnPage = TotalPages > 0 ? ((pageNumber - 1) * pageSize) + 1 : 0;
        LastItemOnPage = TotalPages > 0 ? Math.Min(FirstItemOnPage + pageSize - 1, totalItems) : 0;
    }

    /// <summary>
    /// Projects the items of this page, keeping the paging metadata.
    /// </summary>
    /// <typeparam name="TResult">The type of projected items.</typeparam>
    /// <param name="selector">The projection.</param>
    /// <returns>A page with projected items and the same metadata.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="selector"/> is <see langword="null"/>.</exception>
    public PagedList<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return new PagedList<TResult>(_items.Select(selector), TotalItems, PageNumber, PageSize);
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Factory methods for <see cref="PagedList{T}"/>.
/// </summary>
public static class PagedList
{
    /// <summary>
    /// Returns an empty first page.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <returns>An empty page.</returns>
    public static PagedList<T> Empty<T>() => new();
}
