using System.Globalization;
using D47.Core.Storage;

namespace D47.Core.Interface;

/// <summary>What <see cref="ScreenshotEdits.Rename"/> did: the new path, or why nothing changed. <see cref="IsName"/> marks a refused name rather than a failed file operation.</summary>
public sealed record ScreenshotRename(string? Path, string? Error, bool IsName = false)
{
    public bool Done => Path is not null;
}

/// <summary>Renaming and deleting a JPEG in the screenshots folder, with its note and, when asked, Elite's BMP.</summary>
public static class ScreenshotEdits
{
    public const string Extension = ".jpg";

    private const int LongestStem = 200;

    private const string TempSuffix = ".d47-renaming";

    private static readonly string[] Reserved =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Why <paramref name="stem"/> cannot name the picture at <paramref name="path"/>; null when it can.</summary>
    public static string? NameProblem(IFileSystem files, string path, string? stem)
    {
        ArgumentNullException.ThrowIfNull(files);

        var name = stem?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            return "Type a name.";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "A name can't hold \\ / : * ? \" < > |.";
        }

        if (name.EndsWith('.'))
        {
            return "A name can't end with a dot.";
        }

        if (Reserved.Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase))
        {
            return "Windows doesn't allow that name.";
        }

        if (name.Length > LongestStem)
        {
            return $"A name can be at most {LongestStem.ToString(CultureInfo.InvariantCulture)} characters.";
        }

        var target = Target(path, name);

        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return files.Stat(target) is not null || files.Stat(Path.ChangeExtension(target, ".json")) is not null
            ? "A picture with that name already exists."
            : null;
    }

    /// <summary>
    /// Renames the JPEG and its note to <paramref name="stem"/>. The note keeps its time, so the picture keeps its
    /// place in the list. A file that cannot be copied or removed leaves the picture and its note as they were.
    /// </summary>
    public static ScreenshotRename Rename(IFileSystem files, string path, string? stem)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (NameProblem(files, path, stem) is { } problem)
        {
            return new ScreenshotRename(null, problem, IsName: true);
        }

        var target = Target(path, stem!.Trim());

        if (string.Equals(target, path, StringComparison.Ordinal))
        {
            return new ScreenshotRename(path, null);
        }

        var note = Path.ChangeExtension(path, ".json");
        var targetNote = Path.ChangeExtension(target, ".json");
        var hasNote = files.Stat(note) is not null;

        try
        {
            if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
            {
                ChangeCase(files, path, target);

                if (hasNote)
                {
                    ChangeCase(files, note, targetNote);
                }

                return new ScreenshotRename(target, null);
            }

            files.Copy(path, target);

            if (hasNote)
            {
                CopyOrUndo(files, note, targetNote, target);
            }

            RemoveOrUndo(files, path, hasNote ? [target, targetNote] : [target]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ScreenshotRename(null, Reason(ex));
        }

        if (hasNote)
        {
            TryDelete(files, note);
        }

        return new ScreenshotRename(target, null);
    }

    /// <summary>Elite's BMP the note of a converted picture names, when it is still in <paramref name="eliteFolder"/> with the length and write time the note recorded; null otherwise.</summary>
    public static string? BmpToDelete(IFileSystem files, PictureNote? note, string eliteFolder)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (note?.Bmp is not { Length: > 0 } name
            || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
            || !name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Path.Combine(eliteFolder, name);

        return files.Stat(path) is { } state
            && note.BmpLength == state.Length
            && note.BmpWritten == new DateTimeOffset(state.Written.ToUniversalTime())
            ? path
            : null;
    }

    /// <summary>
    /// Sends <paramref name="bmp"/>, when given, then the JPEG to the Recycle Bin through <paramref name="recycle"/> and
    /// deletes the note. Returns why it stopped, or null when it is done; a failure on the BMP leaves the JPEG as it was.
    /// </summary>
    public static string? Delete(IFileSystem files, string path, string? bmp, Func<string, bool> recycle)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(recycle);

        if (bmp is not null && Recycled(recycle, bmp) is { } bmpReason)
        {
            return $"Elite's BMP could not be moved to the Recycle Bin: {bmpReason}";
        }

        if (Recycled(recycle, path) is { } reason)
        {
            return bmp is null
                ? $"The JPEG could not be moved to the Recycle Bin: {reason}"
                : $"Elite's BMP is in the Recycle Bin, but the JPEG could not be moved there: {reason}";
        }

        TryDelete(files, Path.ChangeExtension(path, ".json"));

        return null;
    }

    private static string Target(string path, string stem) =>
        Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, stem + Extension);

    private static string? Recycled(Func<string, bool> recycle, string path)
    {
        try
        {
            return recycle(path) ? null : "Windows would not move it.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            return Reason(ex);
        }
    }

    private static string Reason(Exception ex) => ex.Message.TrimEnd('.', ' ') + ".";

    private static void CopyOrUndo(IFileSystem files, string from, string to, string undo)
    {
        try
        {
            files.Copy(from, to);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(files, undo);
            throw;
        }
    }

    private static void RemoveOrUndo(IFileSystem files, string path, string[] undo)
    {
        try
        {
            files.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            foreach (var copy in undo)
            {
                TryDelete(files, copy);
            }

            throw;
        }
    }

    private static void ChangeCase(IFileSystem files, string from, string to)
    {
        var temp = from + TempSuffix;

        files.Copy(from, temp);
        files.Delete(from);
        files.Copy(temp, to);
        files.Delete(temp);
    }

    private static void TryDelete(IFileSystem files, string path)
    {
        try
        {
            files.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
