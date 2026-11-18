namespace D47.Core.Knowledge;

/// <summary>A codex entry another Commander logged on a body, and where.</summary>
public sealed record EarlierFind(string Body, long EntryId, string Name, double Latitude, double Longitude);

/// <summary>Everything a community record holds for one system, every category and every row.</summary>
public sealed record CommunitySystemRecord(
    IReadOnlyList<CodexReport> Codex,
    IReadOnlyList<CodexReport> OrganicScans,
    IReadOnlyList<SignalReport> Signals);

/// <summary>A codex entry or organic scan reported in a system.</summary>
/// <param name="Body">The body as the service names it, often without the system name.</param>
/// <param name="Scanned">The service's scanned flag as it sends it, "true" or "false".</param>
public sealed record CodexReport(
    string? Body,
    string? Name,
    long? EntryId,
    string? Category,
    long? IndexId,
    double? Latitude,
    double? Longitude,
    string? Scanned);

/// <summary>A count of surface signals of one kind reported on a body.</summary>
/// <param name="Body">The body as the service names it, often without the system name.</param>
public sealed record SignalReport(string? Body, string? Name, string? Category, int? Count);

/// <summary>The seam to a community record of biology found on bodies.</summary>
public interface ICommunityBiology
{
    /// <summary>The biology logged in a system. Empty when the service does not know it.</summary>
    Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken);

    /// <summary>The whole record for a system. Null when the service does not answer.</summary>
    Task<CommunitySystemRecord?> SystemRecordAsync(string systemName, CancellationToken cancellationToken) =>
        Task.FromResult<CommunitySystemRecord?>(null);
}
