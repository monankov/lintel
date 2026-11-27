using System.Globalization;

namespace Lintel.Primitives;

/// <summary>
/// Generates unique file names that sort by creation time: <c>yyyyMMddHHmmss-{guid}</c>.
/// </summary>
public static class SequentialFileName
{
    /// <summary>
    /// Generates a file name without extension, for example <c>20261125143005-3f2504e04f8941d39a0c0305e82c3301</c>.
    /// </summary>
    /// <remarks>The date part uses local time and the invariant culture.</remarks>
    /// <returns>A new file name.</returns>
    public static string NewFileName()
        => $"{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}";

    /// <summary>
    /// Generates a file name with the extension of <paramref name="existingFileName"/>, if it has one.
    /// </summary>
    /// <param name="existingFileName">A file name or path whose extension is kept.</param>
    /// <returns>A new file name.</returns>
    public static string NewFileName(string? existingFileName)
    {
        var extension = Path.GetExtension(existingFileName);
        return string.IsNullOrWhiteSpace(extension)
            ? NewFileName()
            : NewFileNameWithExtension(extension);
    }

    /// <summary>
    /// Generates a file name with the given extension.
    /// </summary>
    /// <param name="extension">The extension, with or without the leading dot.</param>
    /// <returns>A new file name.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="extension"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="extension"/> is empty or white space.</exception>
    public static string NewFileNameWithExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        return extension.StartsWith('.')
            ? NewFileName() + extension
            : NewFileName() + "." + extension;
    }
}
