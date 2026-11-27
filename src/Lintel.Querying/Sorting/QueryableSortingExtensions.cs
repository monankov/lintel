using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Lintel.Querying.Internal;

namespace Lintel.Querying;

/// <summary>
/// Sorting of queries by property name (for example from a query string) and paging by offset.
/// </summary>
/// <remarks>
/// Property names may be nested (<c>"Customer.Name"</c>) and are matched exactly first, then case-insensitively.
/// The ordering expression is passed to the query provider, so it is translated (for example to SQL by EF Core).
/// </remarks>
public static class QueryableSortingExtensions
{
    /// <summary>
    /// Skips <paramref name="skip"/> items and takes <paramref name="take"/> items.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="skip">The number of items to skip; 0 or more.</param>
    /// <param name="take">The number of items to take; 1 or more.</param>
    /// <returns>The paged query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="skip"/> is negative or <paramref name="take"/> is less than 1.</exception>
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, int skip, int take)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        return query.Skip(skip).Take(take);
    }

    /// <summary>
    /// Sorts a query by several properties, in the order of <paramref name="sort"/>.
    /// </summary>
    /// <remarks>
    /// An empty <paramref name="sort"/> leaves the order of the query unchanged.
    /// Use a dictionary that preserves insertion order (such as <see cref="Dictionary{TKey, TValue}"/> without removals).
    /// </remarks>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="sort">Property names and their sort order.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If a property name is empty or not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, IReadOnlyDictionary<string, SortOrder> sort)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sort);

        IOrderedQueryable<T>? ordered = null;
        foreach (var (propertyName, order) in sort)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new InvalidOperationException("Invalid property name to sort");
            }

            ordered = ordered is null
                ? query.OrderBy(propertyName, order)
                : ordered.ThenBy(propertyName, order);
        }

        return ordered ?? AsOrdered(query);
    }

    /// <summary>Sorts a query by a property in the given order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, string propertyName, SortOrder order)
        => Order(query, propertyName, order == SortOrder.Asc ? nameof(Queryable.OrderBy) : nameof(Queryable.OrderByDescending));

    /// <summary>Sorts a query by a property in ascending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, string propertyName)
        => Order(query, propertyName, nameof(Queryable.OrderBy));

    /// <summary>Sorts a query by a property in descending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> OrderByDescending<T>(this IQueryable<T> query, string propertyName)
        => Order(query, propertyName, nameof(Queryable.OrderByDescending));

    /// <summary>Adds a subsequent sort by a property in the given order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">An already sorted query.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the query is not sorted or the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> ThenBy<T>(this IQueryable<T> query, string propertyName, SortOrder order)
        => Order(query, propertyName, order == SortOrder.Asc ? nameof(Queryable.ThenBy) : nameof(Queryable.ThenByDescending));

    /// <summary>Adds a subsequent sort by a property in ascending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">A sorted query.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> query, string propertyName)
        => Order(query, propertyName, nameof(Queryable.ThenBy));

    /// <summary>Adds a subsequent sort by a property in descending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="query">A sorted query.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted query.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    public static IOrderedQueryable<T> ThenByDescending<T>(this IOrderedQueryable<T> query, string propertyName)
        => Order(query, propertyName, nameof(Queryable.ThenByDescending));

    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    [RequiresDynamicCode(PropertyPath.DynamicCodeMessage)]
    private static IOrderedQueryable<T> Order<T>(IQueryable<T> query, string propertyName, string methodName)
    {
        ArgumentNullException.ThrowIfNull(query);

        var isThenBy = methodName is nameof(Queryable.ThenBy) or nameof(Queryable.ThenByDescending);
        if (isThenBy && !typeof(IOrderedQueryable<T>).IsAssignableFrom(query.Expression.Type))
        {
            throw new InvalidOperationException($"{methodName} requires a sorted query. Call OrderBy first.");
        }

        var (parameter, body) = PropertyPath.Build<T>(propertyName);
        var keySelector = Expression.Lambda(body, parameter);

        var call = Expression.Call(typeof(Queryable), methodName, [typeof(T), body.Type],
            query.Expression, Expression.Quote(keySelector));

        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(call);
    }

    private static IOrderedQueryable<T> AsOrdered<T>(IQueryable<T> query)
        => query as IOrderedQueryable<T> ?? query.OrderBy(static _ => 0);
}
