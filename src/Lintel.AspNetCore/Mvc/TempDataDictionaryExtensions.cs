using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Lintel.AspNetCore.Mvc;

/// <summary>
/// Storing objects in TempData as JSON (System.Text.Json).
/// </summary>
public static class TempDataDictionaryExtensions
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Stores <paramref name="value"/> as JSON.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="tempData">The TempData.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="tempData"/> is <see langword="null"/>.</exception>
    public static void Put<T>(this ITempDataDictionary tempData, string key, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(tempData);

        tempData[key] = JsonSerializer.Serialize(value, _options);
    }

    /// <summary>
    /// Reads a value stored by <see cref="Put{T}"/> without marking it for deletion.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="tempData">The TempData.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/> if there is none.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="tempData"/> is <see langword="null"/>.</exception>
    public static T? Peek<T>(this ITempDataDictionary tempData, string key) where T : class
    {
        ArgumentNullException.ThrowIfNull(tempData);

        return tempData.Peek(key) is string json ? JsonSerializer.Deserialize<T>(json, _options) : null;
    }

    /// <summary>
    /// Reads a value stored by <see cref="Put{T}"/> and marks it for deletion at the end of the request
    /// (the usual TempData semantics: shown once).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="tempData">The TempData.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/> if there is none.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="tempData"/> is <see langword="null"/>.</exception>
    public static T? Get<T>(this ITempDataDictionary tempData, string key) where T : class
    {
        ArgumentNullException.ThrowIfNull(tempData);

        return tempData[key] is string json ? JsonSerializer.Deserialize<T>(json, _options) : null;
    }
}
