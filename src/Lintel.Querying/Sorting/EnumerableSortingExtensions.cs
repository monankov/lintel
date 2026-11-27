using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Lintel.Querying.Internal;

namespace Lintel.Querying;

/// <summary>
/// Sorting of in-memory sequences by property name (for example from a query string).
/// </summary>
/// <remarks>
/// Property names may be nested (<c>"Customer.Name"</c>) and are matched exactly first, then case-insensitively.
/// Keys are compared with <see cref="Comparer{T}.Default"/> of <see cref="object"/>,
/// so property types must implement <see cref="IComparable"/>.
/// </remarks>
public static class EnumerableSortingExtensions
{
    /// <summary>
    /// Sorts a sequence by several properties, in the order of <paramref name="sort"/>.
    /// </summary>
    /// <remarks>An empty <paramref name="sort"/> keeps the original order.</remarks>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The sequence.</param>
    /// <param name="sort">Property names and their sort order.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If a property name is empty or not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, IReadOnlyDictionary<string, SortOrder> sort)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sort);

        IOrderedEnumerable<T>? ordered = null;
        foreach (var (propertyName, order) in sort)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new InvalidOperationException("Invalid property name to sort");
            }

            ordered = ordered is null
                ? source.OrderBy(propertyName, order)
                : ordered.ThenBy(propertyName, order);
        }

        // OrderBy is stable, so a constant key keeps the original order.
        return ordered ?? source as IOrderedEnumerable<T> ?? source.OrderBy(static _ => 0);
    }

    /// <summary>Sorts a sequence by a property in the given order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The sequence.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, string propertyName, SortOrder order)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = KeySelector<T>(propertyName);
        return order == SortOrder.Asc ? source.OrderBy(key) : source.OrderByDescending(key);
    }

    /// <summary>Sorts a sequence by a property in ascending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The sequence.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, string propertyName)
        => source.OrderBy(propertyName, SortOrder.Asc);

    /// <summary>Sorts a sequence by a property in descending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The sequence.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> OrderByDescending<T>(this IEnumerable<T> source, string propertyName)
        => source.OrderBy(propertyName, SortOrder.Desc);

    /// <summary>Adds a subsequent sort by a property in the given order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">An already sorted sequence.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the sequence is not sorted or the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> ThenBy<T>(this IEnumerable<T> source, string propertyName, SortOrder order)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source is not IOrderedEnumerable<T> ordered)
        {
            throw new InvalidOperationException($"{nameof(ThenBy)} requires a sorted sequence. Call OrderBy first.");
        }

        var key = KeySelector<T>(propertyName);
        return order == SortOrder.Asc ? ordered.ThenBy(key) : ordered.ThenByDescending(key);
    }

    /// <summary>Adds a subsequent sort by a property in ascending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">A sorted sequence.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> ThenBy<T>(this IOrderedEnumerable<T> source, string propertyName)
        => source.ThenBy(propertyName, SortOrder.Asc);

    /// <summary>Adds a subsequent sort by a property in descending order.</summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">A sorted sequence.</param>
    /// <param name="propertyName">The property name, possibly nested.</param>
    /// <returns>The sorted sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the property is not found.</exception>
    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    public static IOrderedEnumerable<T> ThenByDescending<T>(this IOrderedEnumerable<T> source, string propertyName)
        => source.ThenBy(propertyName, SortOrder.Desc);

    [RequiresUnreferencedCode(PropertyPath.TrimmingMessage)]
    private static Func<T, object?> KeySelector<T>(string propertyName)
    {
        var (parameter, body) = PropertyPath.Build<T>(propertyName);
        return Expression.Lambda<Func<T, object?>>(Expression.Convert(body, typeof(object)), parameter).Compile();
    }
}
