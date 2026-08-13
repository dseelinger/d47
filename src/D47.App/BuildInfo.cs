using System.Reflection;
using D47.Core.Updates;

namespace D47.App;

/// <summary>
/// Which build this is, from the one place that knows: the informational version the compiler
/// stamps in. Read once here rather than at each site that wants it, so the title bar, the
/// update check, the log and the About dialog cannot disagree about what is running.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    /// The full stamp, version and commit both — "0.2.2+8b21b3d…". What a bug report needs, and
    /// therefore what the About dialog shows and lets you select.
    /// </summary>
    public static string Full { get; } =
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    /// <summary>
    /// Just the version — "0.2.2". What a title bar wants: the commit hash is forty characters
    /// of noise in the one piece of chrome that is on screen the entire time.
    /// <para>
    /// Falls back to the full string when there is no version to find, which is the case for a
    /// build run straight out of the repository.
    /// </para>
    /// </summary>
    public static string Semantic { get; } =
        ReleaseVersion.TryParse(Full, out var version) ? version.ToString() : Full;
}
