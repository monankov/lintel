using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lintel.AspNetCore.Http;

/// <summary>
/// Conversion of query strings to route values, for building links that keep the current filters.
/// </summary>
public static class QueryCollectionExtensions
{
    /// <summary>
    /// Copies the query parameters to a new <see cref="RouteValueDictionary"/>.
    /// </summary>
    /// <param name="collection">The query.</param>
    /// <returns>The route values.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="collection"/> is <see langword="null"/>.</exception>
    public static RouteValueDictionary ToRouteValueDictionary(this IQueryCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var routeValues = new RouteValueDictionary();
        foreach (var (key, value) in collection)
        {
            routeValues[key] = value;
        }
        return routeValues;
    }

    /// <summary>
    /// Copies the query parameters and replaces <paramref name="newKey"/> with <paramref name="newValue"/>;
    /// a <see langword="null"/> or empty value removes the parameter.
    /// </summary>
    /// <param name="collection">The query.</param>
    /// <param name="newKey">The parameter to replace.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns>The route values.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="collection"/> is <see langword="null"/>.</exception>
    public static RouteValueDictionary ToRouteValueDictionary(this IQueryCollection collection, string newKey, object? newValue)
    {
        var routeValues = collection.ToRouteValueDictionary();

        // Query keys are case-insensitive: remove "Page" when replacing "page".
        foreach (var key in routeValues.Keys.Where(key => string.Equals(key, newKey, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            routeValues.Remove(key);
        }

        if (!string.IsNullOrEmpty(newValue?.ToString()))
        {
            routeValues[newKey] = newValue;
        }
        return routeValues;
    }
}
