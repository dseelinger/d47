using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>What a save, load or delete of a binding profile did, as a sentence to say back.</summary>
public sealed record BindingProfileOutcome(bool Done, string Reply);

/// <summary>
/// Named copies of Elite's bindings: every <c>StartPreset*.start</c> file and every custom
/// <c>.binds</c> file they name, copied into a folder per name and copied back on request.
/// </summary>
/// <param name="bindingsDirectory">Elite's Bindings folder.</param>
/// <param name="profilesDirectory">Where the named copies live, one folder per profile.</param>
/// <param name="eliteRunning">Whether Elite is running; it reads the files only at start, so nothing is copied while it runs.</param>
public sealed partial class BindingProfiles(
    string bindingsDirectory,
    string profilesDirectory,
    Func<bool> eliteRunning,
    ILogger logger)
{
    /// <summary>Said when Elite is running.</summary>
    public const string EliteIsRunning =
        "Elite is running. It reads its bindings only when it starts, so close Elite first and ask again.";

    /// <summary>The profile a load saves the replaced set under.</summary>
    public const string BeforeLastLoad = "before last load";

    /// <summary>Raised after a save or a delete changes the list.</summary>
    public event Action? Changed;

    /// <summary>The saved profile names, in alphabetical order.</summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            try
            {
                return Directory.Exists(profilesDirectory)
                    ? [.. Directory.EnumerateDirectories(profilesDirectory)
                        .Select(Path.GetFileName)
                        .OfType<string>()
                        .Where(IsValidName)
                        .Order(StringComparer.OrdinalIgnoreCase)]
                    : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not list the binding profiles in {Directory}", profilesDirectory);
                return [];
            }
        }
    }

    /// <summary>The files that make up the current set, by full path.</summary>
    public IReadOnlyList<string> CurrentSet()
    {
        if (!Directory.Exists(bindingsDirectory))
        {
            return [];
        }

        var starts = Directory.EnumerateFiles(bindingsDirectory, "StartPreset*.start").ToArray();

        var presets = starts
            .SelectMany(File.ReadLines)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var binds = Directory
            .EnumerateFiles(bindingsDirectory, "*.binds")
            .Where(path => presets.Any(preset => IsPresetFile(Path.GetFileName(path), preset)));

        return [.. starts.Concat(binds).Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Copies the current set under <paramref name="name"/>, replacing a profile of that name.</summary>
    public BindingProfileOutcome Save(string name)
    {
        if (Refusal(name) is { } refused)
        {
            return refused;
        }

        name = name.Trim();

        try
        {
            var files = CurrentSet();

            if (files.Count == 0)
            {
                return new(false, "I could not find any Elite bindings to save.");
            }

            var target = Path.Combine(profilesDirectory, name);
            var staging = Path.Combine(profilesDirectory, ".saving");

            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            Directory.CreateDirectory(staging);

            foreach (var file in files)
            {
                File.Copy(file, Path.Combine(staging, Path.GetFileName(file)));
            }

            // The old copy goes only once the new one is complete.
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            Directory.Move(staging, target);

            logger.LogInformation("Saved {Count} binding files as the profile {Name}", files.Count, name);
            Changed?.Invoke();

            return new(true, $"Saved your bindings as \"{name}\".");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not save the binding profile {Name}", name);
            return new(false, $"I could not save \"{name}\": {ex.Message}");
        }
    }

    /// <summary>Copies a saved profile back into Elite's Bindings folder.</summary>
    public BindingProfileOutcome Load(string name)
    {
        if (Refusal(name) is { } refused)
        {
            return refused;
        }

        if (Find(name) is not { } found)
        {
            return new(false, NotFound(name));
        }

        try
        {
            // Read first: the backup below may replace the profile being loaded.
            var files = Directory
                .GetFiles(Path.Combine(profilesDirectory, found))
                .Select(path => (Name: Path.GetFileName(path), Bytes: File.ReadAllBytes(path)))
                .ToArray();

            if (!files.Any(file => IsStartFile(file.Name)))
            {
                return new(false, $"\"{found}\" has no StartPreset file, so Elite would not use it.");
            }

            var kept = CurrentSet().Count > 0 && Save(BeforeLastLoad).Done;

            Directory.CreateDirectory(bindingsDirectory);

            foreach (var (fileName, bytes) in files)
            {
                File.WriteAllBytes(Path.Combine(bindingsDirectory, fileName), bytes);
            }

            // Removed after the profile is written: a start file the profile does not carry would outrank the
            // profile's own when it has a higher version.
            foreach (var start in Directory.EnumerateFiles(bindingsDirectory, "StartPreset*.start").ToArray())
            {
                if (!files.Any(file => string.Equals(file.Name, Path.GetFileName(start), StringComparison.OrdinalIgnoreCase)))
                {
                    File.Delete(start);
                }
            }

            logger.LogInformation("Loaded {Count} binding files from the profile {Name}", files.Length, found);

            var reply = $"Loaded the \"{found}\" bindings. Elite will use them when it next starts.";

            return new(true, kept ? $"{reply} The set they replaced is saved as \"{BeforeLastLoad}\"." : reply);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not load the binding profile {Name}", found);
            return new(false, $"I could not load \"{found}\": {ex.Message}");
        }
    }

    /// <summary>Removes a saved profile. Elite's own files are not touched.</summary>
    public BindingProfileOutcome Delete(string name)
    {
        if (Find(name) is not { } found)
        {
            return new(false, NotFound(name));
        }

        try
        {
            Directory.Delete(Path.Combine(profilesDirectory, found), recursive: true);
            logger.LogInformation("Deleted the binding profile {Name}", found);
            Changed?.Invoke();

            return new(true, $"Deleted the \"{found}\" bindings profile.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete the binding profile {Name}", found);
            return new(false, $"I could not delete \"{found}\": {ex.Message}");
        }
    }

    /// <summary>Whether a name can be a profile: letters, digits, spaces, hyphens and underscores.</summary>
    public static bool IsValidName(string? name) =>
        name is not null
        && name.Trim().Length is > 0 and <= 40
        && ValidName().IsMatch(name.Trim())
        && !ReservedName().IsMatch(name.Trim());

    private static bool IsStartFile(string fileName) =>
        fileName.StartsWith("StartPreset", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".start", StringComparison.OrdinalIgnoreCase);

    private BindingProfileOutcome? Refusal(string name)
    {
        if (!IsValidName(name))
        {
            return new(false, "A profile name is letters, digits, spaces, hyphens and underscores, up to forty characters.");
        }

        return eliteRunning() ? new(false, EliteIsRunning) : null;
    }

    private string? Find(string name) =>
        Names.FirstOrDefault(saved => string.Equals(saved, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private string NotFound(string name) =>
        Names is { Count: > 0 } names
            ? $"There is no \"{name.Trim()}\" bindings profile. Saved: {string.Join(", ", names)}."
            : "There are no saved bindings profiles yet.";

    /// <summary>"&lt;preset&gt;.binds" or "&lt;preset&gt;.&lt;version&gt;.binds".</summary>
    private static bool IsPresetFile(string fileName, string preset) =>
        fileName.Equals(preset + ".binds", StringComparison.OrdinalIgnoreCase)
        || (fileName.StartsWith(preset + ".", StringComparison.OrdinalIgnoreCase)
            && PresetVersion().IsMatch(fileName[(preset.Length + 1)..]));

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _-]*$")]
    private static partial Regex ValidName();

    /// <summary>Windows device names, which cannot be folder names.</summary>
    [GeneratedRegex(@"^(?:con|prn|aux|nul|com[0-9]|lpt[0-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex ReservedName();

    [GeneratedRegex(@"^\d+(\.\d+)*\.binds$", RegexOptions.IgnoreCase)]
    private static partial Regex PresetVersion();
}
