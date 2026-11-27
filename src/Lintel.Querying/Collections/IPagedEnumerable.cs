using System.Collections;

namespace Lintel.Collections;

/// <summary>
/// A page of items from a larger set, with paging metadata.
/// </summary>
public interface IPagedEnumerable : IEnumerable
{
    /// <summary>The number of items in the whole set.</summary>
    int TotalItems { get; }

    /// <summary>The one-based number of the page.</summary>
    int PageNumber { get; }

    /// <summary>The maximum number of items on a page.</summary>
    int PageSize { get; }

    /// <summary>The number of pages in the whole set.</summary>
    int TotalPages { get; }

    /// <summary>Whether a page exists before this one.</summary>
    bool HasPreviousPage { get; }

    /// <summary>Whether a page exists after this one.</summary>
    bool HasNextPage { get; }

    /// <summary>Whether this is the first page of a non-empty set.</summary>
    bool IsFirstPage { get; }

    /// <summary>Whether this is the last page of a non-empty set.</summary>
    bool IsLastPage { get; }

    /// <summary>The one-based position of the first item of this page in the whole set, or 0 for an empty set.</summary>
    int FirstItemOnPage { get; }

    /// <summary>The one-based position of the last item of this page in the whole set, or 0 for an empty set.</summary>
    int LastItemOnPage { get; }
}
