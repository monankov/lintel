namespace Lintel.Primitives;

/// <summary>
/// Where the timestamp is placed in a sequential GUID, depending on how the database sorts GUIDs.
/// </summary>
/// <remarks>
/// For PostgreSQL and other databases that sort GUIDs as strings or bytes from the start, consider
/// the built-in <see cref="Guid.CreateVersion7()"/> for new keys.
/// </remarks>
public enum SequentialGuidType
{
    /// <summary>
    /// Sequential when sorted by the string representation (<c>char(36)</c>, PostgreSQL <c>uuid</c>, MySQL <c>char</c>).
    /// </summary>
    String,

    /// <summary>
    /// Sequential when sorted by the bytes of <see cref="Guid.ToByteArray()"/> (Oracle <c>raw(16)</c>, <c>binary(16)</c>).
    /// </summary>
    Binary,

    /// <summary>
    /// Sequential for SQL Server <c>uniqueidentifier</c>, which compares the last 6 bytes first.
    /// </summary>
    Ending,
}
