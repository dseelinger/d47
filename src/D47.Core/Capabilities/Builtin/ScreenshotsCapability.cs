using D47.Core.Configuration;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Elite's screenshots copied as JPEG, on the settings surface and nowhere else.</summary>
public static class ScreenshotsCapability
{
    public const string Id = "screenshots";

    public const string ConvertKey = "screenshots.convert";

    public const string FolderKey = "screenshots.folder";

    /// <summary>The folder the JPEG copies go in.</summary>
    public static string TargetFolder() => Path.Combine(EliteScreenshot.Folder(), "D47");

    public static CapabilityDescriptor Create() => new()
    {
        Id = Id,
        Group = "Foundation",
        Name = "Screenshots",
        Summary = "Save a JPEG copy of each Elite screenshot, named after the time and the place.",
        Examples =
        [
            "stop converting my screenshots",
            "start converting my screenshots",
        ],
        Display = new CapabilityDisplay { ShowOnPanel = false },
        Settings = [ConvertRow(), FolderRow()],
        Tools = [],
    };

    private static SettingRow ConvertRow() => new()
    {
        Key = ConvertKey,
        Label = "Convert Elite's screenshots",
        Help =
            "When you take a screenshot in Elite, D47 saves a JPEG copy named after the time and the place, "
            + "in a D47 folder beside Elite's own screenshots. Elite's BMP is left as it is.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "on",
        DocsAnchor = "the-details",
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("start converting my screenshots", "true"),
            new SettingCommandPhrase("stop converting my screenshots", "false"),
        ],
        Binding = SettingBinding.Toggle(
            s => s.Screenshots.Convert,
            (s, on) => s with { Screenshots = s.Screenshots with { Convert = on } }),
    };

    private static SettingRow FolderRow() => new()
    {
        Key = FolderKey,
        Label = "Saved to",
        Help = "Where the JPEG copies go.",
        Kind = SettingKind.Info,
        DocsAnchor = "the-details",
        Binding = new SettingBinding { Read = _ => TargetFolder() },
    };
}
