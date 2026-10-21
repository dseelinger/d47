namespace D47.Core.Knowledge;

/// <summary>A codex entry another Commander logged on a body, and where.</summary>
public sealed record EarlierFind(string Body, long EntryId, string Name, double Latitude, double Longitude);

/// <summary>The seam to a community record of biology found on bodies.</summary>
public interface ICommunityBiology
{
    /// <summary>The biology logged in a system. Empty when the service does not know it.</summary>
    Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken);
}
