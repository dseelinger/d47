namespace D47.Core.Knowledge;

/// <summary>The seam to whatever knows when the last background simulation tick happened.</summary>
public interface IBgsTickSource
{
    /// <summary>The time of the last tick, or null when the source did not answer or answered with nothing usable.</summary>
    Task<DateTimeOffset?> LastTickAsync(CancellationToken cancellationToken);
}
