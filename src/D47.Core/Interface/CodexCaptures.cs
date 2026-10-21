using D47.Core.Storage;

namespace D47.Core.Interface;

/// <summary>One kept capture. <see cref="Noted"/> is set when its note lists the entry and region asked for.</summary>
public sealed record CodexCapture(string Path, DateTimeOffset At, string? System, string? Body, bool Noted);

public static class CodexCaptures
{
    /// <summary>
    /// The captures in <paramref name="folder"/>, newest first, those whose note lists
    /// <paramref name="key"/> ahead of the rest. A capture with no readable note is listed under its file time.
    /// </summary>
    public static IReadOnlyList<CodexCapture> List(IFileSystem files, string folder, CodexPictureKey key)
    {
        ArgumentNullException.ThrowIfNull(files);

        var found = new List<CodexCapture>();

        foreach (var path in files.Enumerate(folder, "capture-*.jpg"))
        {
            var note = PictureNote.Parse(files.ReadText(System.IO.Path.ChangeExtension(path, ".json")));
            var at = note?.At
                     ?? (files.Stat(path) is { } state ? new DateTimeOffset(DateTime.SpecifyKind(state.Written, DateTimeKind.Utc)) : DateTimeOffset.MinValue);
            var noted = note?.CodexEntries.Any(entry => entry.EntryId == key.EntryId && entry.RegionId == key.RegionId) == true;

            found.Add(new CodexCapture(path, at, note?.System, note?.Body, noted));
        }

        return [.. found.OrderByDescending(capture => capture.Noted).ThenByDescending(capture => capture.At)];
    }
}
