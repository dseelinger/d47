using D47.Core.Storage;

namespace D47.Core.Interface;

/// <summary>One screenshot to cut a codex picture from. <see cref="Noted"/> is set when its note lists the entry and region asked for.</summary>
public sealed record CodexCapture(string Path, DateTimeOffset At, string? System, string? Body, bool Noted);

public static class CodexCaptures
{
    /// <summary>
    /// The screenshots in <paramref name="folder"/>, newest first, those whose note lists <paramref name="key"/> ahead
    /// of the rest.
    /// </summary>
    public static IReadOnlyList<CodexCapture> List(IFileSystem files, string folder, CodexPictureKey key) =>
    [
        .. ScreenshotFolder.List(files, folder)
            .Select(shot => new CodexCapture(
                shot.Path,
                shot.At,
                shot.Note?.System,
                shot.Note?.Body,
                shot.Note?.CodexEntries.Any(entry => entry.EntryId == key.EntryId && entry.RegionId == key.RegionId) == true))
            .OrderByDescending(capture => capture.Noted)
            .ThenByDescending(capture => capture.At),
    ];
}
