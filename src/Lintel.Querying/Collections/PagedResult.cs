namespace Lintel.Collections;

/// <summary>
/// A serializable page of items with paging metadata, for APIs and caches.
/// Replaces <c>SerializablePagedList</c> from Structr.
/// </summary>
/// <remarks>
/// Serializes with System.Text.Json without custom converters:
/// <c>{ "items": [...], "totalItems": 10, "pageNumber": 1, "pageSize": 5 }</c>.
/// Convert back with <see cref="PagedListExtensions.ToPagedList{T}(PagedResult{T})"/>.
/// </remarks>
/// <typeparam name="T">The type of items.</typeparam>
/// <param name="Items">The items of this page.</param>
/// <param name="TotalItems">The number of items in the whole set.</param>
/// <param name="PageNumber">The one-based number of the page.</param>
/// <param name="PageSize">The maximum number of items on a page.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalItems, int PageNumber, int PageSize);
