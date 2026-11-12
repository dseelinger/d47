using System.Globalization;
using D47.Core.Journal;
using D47.Core.Storage;

namespace D47.Core.Interface;

/// <summary>A JPEG in the screenshots folder, with its note when it has a readable one.</summary>
public sealed record KeptScreenshot(string Path, DateTimeOffset At, PictureNote? Note);

/// <summary>Lists the JPEG copies of Elite's screenshots, and moves <c>capture-*.jpg</c> pictures and their notes from a captures folder into them.</summary>
public static class ScreenshotFolder
{
    private const string NameStamp = "yyyy-MM-dd HH-mm-ss";

    private const string CaptureStamp = "yyyyMMdd-HHmmss";

    /// <summary>
    /// Every JPEG in <paramref name="folder"/>, newest first. One with no readable note is dated from the local time its
    /// name starts with, else from its file time.
    /// </summary>
    public static IReadOnlyList<KeptScreenshot> List(IFileSystem files, string folder)
    {
        ArgumentNullException.ThrowIfNull(files);

        var found = new List<KeptScreenshot>();

        foreach (var path in files.Enumerate(folder, "*.jpg"))
        {
            var note = PictureNote.Parse(files.ReadText(System.IO.Path.ChangeExtension(path, ".json")));
            var at = note?.At ?? DatedName(System.IO.Path.GetFileName(path)) ?? Written(files, path);

            found.Add(new KeptScreenshot(path, at, note));
        }

        return [.. found.OrderByDescending(shot => shot.At)];
    }

    /// <summary>The local time a file name in <see cref="EliteScreenshot.Name"/>'s form starts with; null for any other name.</summary>
    public static DateTimeOffset? DatedName(string fileName) => Stamped(fileName, 0, NameStamp);

    /// <summary>
    /// Moves each <c>capture-*.jpg</c> in <paramref name="captures"/> and its note into <paramref name="target"/>, named
    /// as <see cref="EliteScreenshot.Name"/> names a screenshot of the same time and place. A picture that cannot be read
    /// or written stays where it is and the rest still move. Returns how many moved.
    /// </summary>
    public static int MoveCaptures(IFileSystem files, string captures, string target)
    {
        ArgumentNullException.ThrowIfNull(files);

        var moved = 0;

        foreach (var path in files.Enumerate(captures, "capture-*.jpg"))
        {
            try
            {
                if (MoveCapture(files, path, target))
                {
                    moved++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return moved;
    }

    private static bool MoveCapture(IFileSystem files, string path, string target)
    {
        if (files.ReadBytes(path) is not { } jpeg)
        {
            return false;
        }

        var notePath = System.IO.Path.ChangeExtension(path, ".json");
        var note = PictureNote.Parse(files.ReadText(notePath));
        var name = System.IO.Path.GetFileName(path);
        var at = note?.At ?? Stamped(name, "capture-".Length, CaptureStamp) ?? Written(files, path);
        var shot = new EliteScreenshot(name, note?.Width ?? 0, note?.Height ?? 0, note?.System, EliteScreenshot.ShortBody(note?.System, note?.Body));

        var copy = 1;

        while (files.Stat(System.IO.Path.Combine(target, shot.Name(at.LocalDateTime, copy))) is not null)
        {
            copy++;
        }

        var moveTo = System.IO.Path.Combine(target, shot.Name(at.LocalDateTime, copy));

        files.WriteBytes(moveTo, jpeg);

        if (note is not null)
        {
            files.WriteText(System.IO.Path.ChangeExtension(moveTo, ".json"), note.Write());
        }

        files.Delete(path);
        files.Delete(notePath);

        return true;
    }

    private static DateTimeOffset? Stamped(string fileName, int start, string format) =>
        fileName.Length >= start + format.Length
        && DateTime.TryParseExact(
            fileName.AsSpan(start, format.Length),
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var local)
            ? new DateTimeOffset(local)
            : null;

    private static DateTimeOffset Written(IFileSystem files, string path) =>
        files.Stat(path) is { } state
            ? new DateTimeOffset(DateTime.SpecifyKind(state.Written, DateTimeKind.Utc))
            : DateTimeOffset.MinValue;
}
