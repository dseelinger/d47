using System.Reflection;
using D47.Core.Updates;

namespace D47.App;

/// <summary>
/// Which build this is, from the one place that knows: the informational version the compiler stamps
/// in.
/// </summary>
public static class BuildInfo
{
    /// <summary>The full stamp, version and commit both — "0.2.2+8b21b3d…".</summary>
    public static string Full { get; } =
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    /// <summary>Just the version — "0.2.2".</summary>
    public static string Semantic { get; } =
        ReleaseVersion.TryParse(Full, out var version) ? version.ToString() : Full;

    /// <summary>
    /// The pre-release label the version carries — <c>local</c> for a hand-installed build — or null
    /// for a published one.
    /// </summary>
    public static string? Label { get; } = LabelOf(Full);

    /// <summary>"test-drive" or "local-release" for a build marked as one, or null for a published build.</summary>
    public static string? Mark { get; } =
        Assembly.GetEntryAssembly()
            ?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "BuildMark", StringComparison.Ordinal))
            ?.Value is { Length: > 0 } mark
            ? mark
            : null;

    /// <summary>The local date and time of the commit a test drive or local release was built from, or null.</summary>
    public static string? Committed { get; } =
        Assembly.GetEntryAssembly()
            ?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "CommitTime", StringComparison.Ordinal))
            ?.Value is { Length: > 0 } committed
            ? committed
            : null;

    /// <summary>The version for the title bar, followed by the commit time when the build carries one.</summary>
    public static string Titled(string version) => Committed is null ? version : $"{version} · {Committed}";

    /// <summary>Whether this build came from a working tree rather than a published release.</summary>
    public static bool IsLocal => Label is not null;

    /// <summary>
    /// The label between the version and the commit — <c>0.84.3-local+8b21b3d</c> gives <c>local</c>.
    /// </summary>
    internal static string? LabelOf(string stamp)
    {
        var version = stamp.Split('+')[0];
        var dash = version.IndexOf('-', StringComparison.Ordinal);

        return dash >= 0 && dash < version.Length - 1 ? version[(dash + 1)..] : null;
    }
}
