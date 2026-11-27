namespace Lintel.Collections;

/// <summary>
/// A read-only page of items with paging metadata. Implemented by <see cref="PagedList{T}"/>.
/// </summary>
/// <remarks>
/// The interface is covariant: an <c>IPagedList&lt;OrderVm&gt;</c> can be used where an
/// <c>IPagedList&lt;object&gt;</c> is expected, for example in a view model interface with <c>out</c> type parameters.
/// </remarks>
/// <typeparam name="T">The type of items.</typeparam>
public interface IPagedList<out T> : IPagedEnumerable, IReadOnlyList<T>;
