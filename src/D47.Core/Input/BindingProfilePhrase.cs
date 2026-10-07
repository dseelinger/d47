using System.Text.RegularExpressions;

namespace D47.Core.Input;

/// <summary>What <see cref="BindingProfilePhrase.Read"/> made of an utterance: which tool, and the profile name.</summary>
public sealed record BindingProfileReading(string Tool, string Name);

/// <summary>The model-free grammar for binding profiles: "save these bindings as &lt;name&gt;", "load the &lt;name&gt; bindings".</summary>
public static partial class BindingProfilePhrase
{
    /// <summary>The utterance read as a save or a load, or null when it is not one this grammar takes.</summary>
    public static BindingProfileReading? Read(string? input)
    {
        var said = (input ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim();

        if (Save().Match(said) is { Success: true } save && Named(save) is { } saved)
        {
            return new(Capabilities.Builtin.BindingProfilesCapability.SaveTool, saved);
        }

        var load = LoadBefore().Match(said);

        if (!load.Success)
        {
            load = LoadAfter().Match(said);
        }

        if (load.Success && Named(load) is { } loaded)
        {
            return new(Capabilities.Builtin.BindingProfilesCapability.LoadTool, loaded);
        }

        return null;
    }

    private static string? Named(Match match) =>
        match.Groups["name"].Value.Trim() is { Length: > 0 } name
        && BindingProfiles.IsValidName(name)
        && !NotAName.Contains(name)
            ? name
            : null;

    /// <summary>Words the patterns can capture that only ever belong to the sentence.</summary>
    private static readonly HashSet<string> NotAName = new(["the", "my", "current", "these"], StringComparer.OrdinalIgnoreCase);

    private const string Binds = "(?:bindings|binds|controls|key ?binds)";

    /// <summary>Load takes no "controls", which names game screens as often as a profile.</summary>
    private const string LoadBinds = "(?:bindings|binds|key ?binds)";

    [GeneratedRegex(
        @"^save (?:these|the|my|the current|my current) " + Binds + @" (?:as|under) (?:the )?(?<name>.+?)(?: profile)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Save();

    [GeneratedRegex(
        @"^load (?:the |my )?(?<name>.+?) " + LoadBinds + @"(?: profile)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoadBefore();

    [GeneratedRegex(
        @"^load (?:the |my )?" + LoadBinds + @" (?:profile )?(?:called |named )?(?<name>.+?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoadAfter();
}
