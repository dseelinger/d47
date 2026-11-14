using D47.Core.Configuration;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Elite's screenshots copied as JPEG, with their switch on the settings surface and the pictures on Commander › Ledgers › Screenshots.</summary>
public static class ScreenshotsCapability
{
    public const string Id = "screenshots";

    public const string ConvertKey = "screenshots.convert";

    public const string DeleteOriginalKey = "screenshots.deleteOriginal";

    public const string FolderKey = "screenshots.folder";

    /// <summary>The folder the JPEG copies go in.</summary>
    public static string TargetFolder() => EliteScreenshot.Folder();

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
            "delete my screenshot originals",
            "keep my screenshot originals",
        ],
        Display = new CapabilityDisplay { ShowOnPanel = false },
        Settings = [ConvertRow(), DeleteOriginalRow(), FolderRow()],
        Tools = [],
    };

    private static SettingRow ConvertRow() => new()
    {
        Key = ConvertKey,
        Exercise =
            "Open Settings › You and your records › Logs and goals, group Screenshots, and find "
            + "Convert Elite's screenshots (on by default). On, take a screenshot in Elite: a JPEG "
            + "appears in Elite's pictures folder (the Saved to row beside it "
            + "shows the path), named for the local time, the system and the body, such as "
            + "\"2026-10-29 14-05-09 Sol.jpg\". Elite's BMP goes to the Recycle Bin (see the next row). Turn the row off, or say \"stop "
            + "converting my screenshots\", and take another: no new JPEG appears. \"start converting "
            + "my screenshots\" turns it on.",
        Label = "Convert Elite's screenshots",
        Help =
            "When you take a screenshot in Elite, D47 saves a JPEG copy named after the time and the place, "
            + "in Elite's pictures folder. Elite's BMP goes to the Recycle Bin once the JPEG is saved, "
            + "unless you turn off Delete Elite's BMP after converting.",
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

    private static SettingRow DeleteOriginalRow() => new()
    {
        Key = DeleteOriginalKey,
        Exercise =
            "Open Settings › You and your records › Logs and goals, group Screenshots, and find "
            + "Delete Elite's BMP after converting (on by default, a ticked checkbox). Ticked, with "
            + "Convert Elite's screenshots on, take a screenshot in Elite: a JPEG appears in the D47 "
            + "folder and Elite's BMP is in the Recycle Bin instead of Elite's pictures folder. Say "
            + "\"keep my screenshot originals\": the row unticks, and the next screenshot leaves its BMP "
            + "in Elite's folder. \"delete my screenshot originals\" ticks it again.",
        Label = "Delete Elite's BMP after converting",
        Help =
            "After D47 saves the JPEG copy of a screenshot, Elite's BMP goes to the Recycle Bin. "
            + "This does nothing while converting is off. Windows deletes a file too large for the "
            + "Recycle Bin outright.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "on",
        DocsAnchor = "the-details",
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("delete my screenshot originals", "true"),
            new SettingCommandPhrase("keep my screenshot originals", "false"),
        ],
        Binding = SettingBinding.Toggle(
            s => s.Screenshots.DeleteOriginal,
            (s, on) => s with { Screenshots = s.Screenshots with { DeleteOriginal = on } }),
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
