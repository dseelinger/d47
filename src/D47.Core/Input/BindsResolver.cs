using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>Finding the bindings file the Commander is actually using.</summary>
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

    /// <summary>Resolves and parses the active bindings.</summary>
    /// <param name="bindingsDirectory">The user profile's Bindings folder.</param>
    /// <param name="gameDirectories">
    /// Install locations to search for shipped presets.
    /// </param>
    public static EliteBinds Resolve(
        string bindingsDirectory,
        IEnumerable<string> gameDirectories,
        ILogger logger) =>
        Resolve(bindingsDirectory, gameDirectories, logger, out _);

    /// <summary>
    /// As above, and says whether the answer is "nothing is bound" or "I could not look" — which used
    /// to be the same answer and cost a Commander every key d47 can press for two hours and forty-one
    /// minutes (#24).
    /// </summary>
    public static EliteBinds Resolve(
        string bindingsDirectory,
        IEnumerable<string> gameDirectories,
        ILogger logger,
        out bool unreadable)
    {
        unreadable = false;

        var preset = ActivePresetName(bindingsDirectory, logger, out var locked);

        if (preset is null)
        {
            // Two different sentences, because they point at different fixes: "locked" is not "missing" (#24).
            if (locked)
            {
                unreadable = true;

                logger.LogWarning(
                    "The StartPreset file under {Directory} could not be read this time; "
                    + "D47 does not know which binds are active yet",
                    bindingsDirectory);
            }
            else
            {
                logger.LogInformation(
                    "No StartPreset file under {Directory}; D47 cannot say which binds are active",
                    bindingsDirectory);
            }

            return EliteBinds.None;
        }

        var searched = new List<string> { bindingsDirectory };
        searched.AddRange(gameDirectories);

        if (HighestVersioned(searched, preset, logger) is not { } file)
        {
            // Named rather than shrugged at: knowing which preset is active and not finding its file is a
            // different problem from not knowing the preset, and points at a different fix.
            logger.LogInformation(
                "Active preset is {Preset} but no matching .binds file was found in {Count} locations",
                preset,
                searched.Count);

            return EliteBinds.None;
        }

        return EliteBinds.Parse(file, preset, logger);
    }

    /// <summary>The preset named by the highest-versioned <c>StartPreset.*.start</c>.</summary>
    public static string? ActivePresetName(string bindingsDirectory, ILogger logger) =>
        ActivePresetName(bindingsDirectory, logger, out _);

    /// <summary>
    /// As above, and reports through <paramref name="locked"/> whether the null means there is no such
    /// file or a file was there and something else had it open.
    /// </summary>
    public static string? ActivePresetName(string bindingsDirectory, ILogger logger, out bool locked)
    {
        locked = false;

        if (!Directory.Exists(bindingsDirectory))
        {
            return null;
        }

        var startFiles = Directory
            .EnumerateFiles(bindingsDirectory, "StartPreset*.start")
            .Select(path => (Path: path, Version: VersionOf(Path.GetFileName(path))))
            // With VersionOrder, exactly as HighestVersioned does below.
            .OrderByDescending(entry => entry.Version, VersionOrder.Instance)
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
                // Elite writes this file and lets go of it.
                locked = true;

                logger.LogWarning(ex, "Could not read {Path}", path);
            }
        }

        return null;
    }

    /// <summary>The highest-versioned <c>&lt;preset&gt;.*.binds</c> across every directory searched.</summary>
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
                // Recursive for the game directory: the shipped presets sit under a ControlSchemes folder
                // whose exact path has moved between game versions, and searching for the file beats
                // hardcoding the folder that holds it.
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

                // "<preset>.binds" or "<preset>.<version>.binds", and the preset name itself can contain
                // dots, so the match is anchored on the name rather than split on them.
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

    /// <summary>The numeric segments in a filename, in order.</summary>
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
