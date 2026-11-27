namespace Lintel.Primitives;

/// <summary>
/// Generates GUIDs that grow over time, so that they do not fragment database indexes.
/// </summary>
public interface ISequentialGuidProvider
{
    /// <summary>
    /// Generates a sequential GUID.
    /// </summary>
    /// <param name="type">Where to place the timestamp, depending on the database.</param>
    /// <returns>A new GUID.</returns>
    Guid GetSequentialGuid(SequentialGuidType type = SequentialGuidType.String);
}

/// <summary>
/// Supplies the random part of a sequential GUID. Usually <see cref="Guid.NewGuid"/>.
/// </summary>
/// <returns>A GUID whose bytes are partially replaced by the timestamp.</returns>
public delegate Guid SequentialGuidInitializer();

/// <summary>
/// Supplies the timestamp of a sequential GUID.
/// </summary>
/// <returns>The timestamp; milliseconds since the Unix epoch are written into the GUID.</returns>
public delegate DateTime SequentialGuidTimestampProvider();
