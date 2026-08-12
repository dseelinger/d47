using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>
/// Finding the bindings file the Commander is actually using (architecture.md D4).
/// <para>
/// This is the part that is easy to get wrong, and the naive version walks into all three
/// traps at once:
/// </para>
/// <list type="number">
/// <item>
/// <b>The active preset is named in <c>StartPreset.&lt;major&gt;.start</c>, not assumed.</b>
/// Reading <c>Custom.*.binds</c> unconditionally parses a file the Commander may not be using
/// and then confidently reports the wrong keys.
/// </item>
/// <item>
/// <b>A built-in preset does not live in the user profile.</b> Custom presets are under
/// <c>Options\Bindings\</c>; the shipped ones — <c>KeyboardMouseOnly</c> and friends — live
/// in the game's install directory. Both have to be searched.
/// </item>
/// <item>
/// <b>The version suffix moves, and the two numbers are not the same number.</b> A machine can
/// have <c>Custom.4.2.binds</c> alongside <c>StartPreset.4.start</c>. Neither may be
/// hardcoded; match by pattern and take the highest present.
/// </item>
/// </list>
/// <para>
/// A Commander who has never customised their binds is the common case, not an edge case, and
/// it is exactly the case a hardcoded filename fails on — silently, by finding nothing or by
/// finding something stale.
/// </para>
/// </summary>
public static partial class BindsResolver
{
    /// <summary>Where custom presets are written.</summary>
    public static string DefaultBindingsDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Frontier Developments",
            "Elite Dangerous",
            "Options",
            "Bindings");

    /// <summary>
    /// Resolves and parses the active bindings. Returns <see cref="EliteBinds.None"/> when
    /// nothing can be found — no Elite install, or a layout d47 does not recognise — which is a
    /// state to report rather than a failure to throw on.
    /// </summary>
    /// <param name="bindingsDirectory">The user profile's Bindings folder.</param>
    /// <param name="gameDirectories">
    /// Install locations to search for shipped presets. Empty is legitimate: d47 does not
    /// require an Elite install to be locatable, and a Commander on a custom preset never needs
    /// one.
    /// </param>
    public static EliteBinds Resolve(
        string bindingsDirectory,
        IEnumerable<string> gameDirectories,
        ILogger logger)
    {
        var preset = ActivePresetName(bindingsDirectory, logger);

        if (preset is null)
        {
            logger.LogInformation(
                "No StartPreset file under {Directory}; d47 cannot say which binds are active",
                bindingsDirectory);

            return EliteBinds.None;
        }

        var searched = new List<string> { bindingsDirectory };
        searched.AddRange(gameDirectories);

        if (HighestVersioned(searched, preset, logger) is not { } file)
        {
            // Named rather than shrugged at: knowing which preset is active and not finding its
            // file is a different problem from not knowing the preset, and points at a
            // different fix.
            logger.LogInformation(
                "Active preset is {Preset} but no matching .binds file was found in {Count} locations",
                preset,
                searched.Count);

            return EliteBinds.None;
        }

        return EliteBinds.Parse(file, preset, logger);
    }

    /// <summary>
    /// The preset named by the highest-versioned <c>StartPreset.*.start</c>. Elite writes one
    /// preset name per line — one per control mode in recent versions — and they are normally
    /// the same name; the first non-empty line is taken.
    /// </summary>
    public static string? ActivePresetName(string bindingsDirectory, ILogger logger)
    {
        if (!Directory.Exists(bindingsDirectory))
        {
            return null;
        }

        var startFiles = Directory
            .EnumerateFiles(bindingsDirectory, "StartPreset*.start")
            .Select(path => (Path: path, Version: VersionOf(Path.GetFileName(path))))
            .OrderByDescending(entry => entry.Version)
            .ToArray();

        foreach (var (path, _) in startFiles)
        {
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    var name = line.Trim();

                    if (name.Length > 0)
                    {
                        return name;
                    }
                }
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not read {Path}", path);
            }
        }

        return null;
    }

    /// <summary>
    /// The highest-versioned <c>&lt;preset&gt;.*.binds</c> across every directory searched.
    /// Version comparison is numeric segment by segment, so 4.10 sorts above 4.2 — which a
    /// string comparison gets backwards, and which would quietly select a file two revisions
    /// stale.
    /// </summary>
    private static string? HighestVersioned(
        IEnumerable<string> directories,
        string preset,
        ILogger logger)
    {
        var candidates = new List<(string Path, int[] Version)>();

        foreach (var directory in directories.Where(Directory.Exists))
        {
            IEnumerable<string> files;

            try
            {
                // Recursive for the game directory: the shipped presets sit under a
                // ControlSchemes folder whose exact path has moved between game versions, and
                // searching for the file beats hardcoding the folder that holds it.
                files = Directory.EnumerateFiles(directory, "*.binds", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Could not search {Directory} for bindings", directory);
                continue;
            }

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);

                // "<preset>.binds" or "<preset>.<version>.binds", and the preset name itself can
                // contain dots, so the match is anchored on the name rather than split on them.
                if (!name.StartsWith(preset + ".", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(preset + ".binds", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidates.Add((file, VersionOf(name)));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Version, VersionOrder.Instance)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    /// <summary>
    /// The numeric segments in a filename, in order. An unversioned name yields an empty
    /// version, which sorts below every versioned one — correct, since a versioned file is the
    /// newer convention.
    /// </summary>
    private static int[] VersionOf(string fileName) =>
        [.. VersionSegment().Matches(fileName).Select(match => int.Parse(match.Value))];

    private sealed class VersionOrder : IComparer<int[]>
    {
        public static readonly VersionOrder Instance = new();

        public int Compare(int[]? left, int[]? right)
        {
            left ??= [];
            right ??= [];

            for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
            {
                var a = index < left.Length ? left[index] : 0;
                var b = index < right.Length ? right[index] : 0;

                if (a != b)
                {
                    return a.CompareTo(b);
                }
            }

            return 0;
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex VersionSegment();
}
