namespace D47.Core.Knowledge;

/// <summary>The seam to the daily signal index.</summary>
public interface ISignalIndexSource
{
    /// <summary>
    /// The index, fetched first when the copy on disk is missing or stale. Null when the setting is off or no copy
    /// exists.
    /// </summary>
    Task<SignalIndex?> GetAsync(CancellationToken cancellationToken);
}
