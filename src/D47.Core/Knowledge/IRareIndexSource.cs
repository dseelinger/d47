namespace D47.Core.Knowledge;

/// <summary>The seam to the published rare index.</summary>
public interface IRareIndexSource
{
    /// <summary>
    /// The index, fetched first when the copy on disk is missing or stale. Null when no copy exists and none could be
    /// downloaded.
    /// </summary>
    Task<RareIndex?> GetAsync(CancellationToken cancellationToken);
}
