namespace Lintel.Primitives;

/// <summary>
/// Supplies strictly increasing UTC timestamps for sequential GUIDs: each timestamp is at least
/// <see cref="IncrementMs"/> milliseconds after the previous one, even if the clock does not move or goes back.
/// </summary>
/// <remarks>
/// With more than 250 GUIDs per second the timestamps run ahead of the clock; the order is still preserved.
/// </remarks>
public class UniqueSequentialGuidTimestampProvider
{
    /// <summary>
    /// The minimal distance between two timestamps, in milliseconds. Larger than the 3.33 ms precision
    /// of SQL Server <c>datetime</c>.
    /// </summary>
    public const double IncrementMs = 4;

    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private DateTime _lastValue = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance that reads the system clock.
    /// </summary>
    public UniqueSequentialGuidTimestampProvider() : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance that reads <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The clock.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public UniqueSequentialGuidTimestampProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the next timestamp.
    /// </summary>
    /// <returns>A UTC timestamp later than any previously returned one.</returns>
    public DateTime GetTimestamp()
    {
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;

        lock (_lock)
        {
            if ((timestamp - _lastValue).TotalMilliseconds < IncrementMs)
            {
                timestamp = _lastValue.AddMilliseconds(IncrementMs);
            }
            _lastValue = timestamp;
        }

        return timestamp;
    }
}
