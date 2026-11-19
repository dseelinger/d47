using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// One region first to send to Canonn, credited to <paramref name="CommanderName"/>. <paramref name="ClientVersion"/>
/// is sent as written.
/// </summary>
public sealed record RegionFirstReport(RegionFirstRecord Record, string CommanderName, bool Odyssey, string ClientVersion);

/// <summary>Sends a region first to Canonn's codex record.</summary>
public interface IRegionFirstReport
{
    /// <summary>True only when Canonn answered 200, which does not confirm the find was recorded.</summary>
    Task<bool> SendAsync(RegionFirstReport report, CancellationToken cancellationToken);
}
