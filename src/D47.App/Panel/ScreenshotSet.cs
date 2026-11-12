using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using D47.Core.Interface;
using D47.Core.Storage;

namespace D47.App.Panel;

/// <summary>One screenshot as the Screenshots ledger draws it.</summary>
public sealed record ScreenshotItem(KeptScreenshot Shot, long Length, string System, string Body, bool CodexPicture)
{
    public string Stem => Path.GetFileNameWithoutExtension(Shot.Path);
}

/// <summary>How the screenshots ledger reaches the Codex pictures cut from a screenshot: the keys, a rename and a removal.</summary>
public sealed record CodexLinks(
    Func<string, IReadOnlyList<CodexPictureKey>> CutFrom,
    Func<string, string, int> Renamed,
    Action<CodexPictureKey> Removed);

/// <summary>The Commander's screenshots: the JPEG copies in one folder, their cached thumbnails and the Explorer call.</summary>
public sealed class ScreenshotSet
{
    public const int ThumbnailWidth = 520;

    public const int ThumbnailHeight = 292;

    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly string _thumbnails;
    private readonly Func<IReadOnlySet<string>> _codexCut;
    private readonly Action<string?> _show;
    private readonly Func<string, bool> _recycle;
    private readonly string? _bmpFolder;
    private readonly CodexLinks? _codex;

    /// <param name="folder"><c>ScreenshotsCapability.TargetFolder()</c>.</param>
    /// <param name="thumbnails"><c>AppPaths.ScreenshotThumbnails</c>.</param>
    /// <param name="codexCut">The screenshot names a Codex picture is cut from.</param>
    /// <param name="show">Opens Explorer on a file, or on the folder when null; defaults to Explorer.</param>
    /// <param name="recycle">Sends a file to the Recycle Bin; false or a throw when it cannot.</param>
    /// <param name="bmpFolder">Elite's pictures folder, where the BMP a note names is.</param>
    /// <param name="codex">The Codex pictures cut from a screenshot; null when there are none to reach.</param>
    public ScreenshotSet(
        IFileSystem files,
        string folder,
        string thumbnails,
        Func<IReadOnlySet<string>>? codexCut = null,
        Action<string?>? show = null,
        Func<string, bool>? recycle = null,
        string? bmpFolder = null,
        CodexLinks? codex = null)
    {
        _files = files;
        _folder = folder;
        _thumbnails = thumbnails;
        _codexCut = codexCut ?? (() => new HashSet<string>());
        _show = show ?? ShowInExplorer;
        _recycle = recycle ?? (_ => false);
        _bmpFolder = bmpFolder;
        _codex = codex;
    }

    /// <summary>Elite's BMP the picture was converted from, when it is still there; null otherwise. Reads the disk: call it off the UI thread.</summary>
    public string? BmpOf(ScreenshotItem item) =>
        _bmpFolder is { } folder ? ScreenshotEdits.BmpToDelete(_files, item.Shot.Note, folder) : null;

    /// <summary>The names of the Codex entries whose picture was cut from <paramref name="item"/>. Reads the disk: call it off the UI thread.</summary>
    public IReadOnlyList<string> CodexEntriesCut(ScreenshotItem item) =>
        [.. CutFrom(item).Select(key => Named(item, key))];

    /// <summary>Renames the picture and its note, and the screenshot name every Codex picture cut from it records. Call it off the UI thread.</summary>
    public ScreenshotRename Rename(ScreenshotItem item, string? stem)
    {
        var result = ScreenshotEdits.Rename(_files, item.Shot.Path, stem);

        if (result.Path is { } renamed && !string.Equals(renamed, item.Shot.Path, StringComparison.Ordinal))
        {
            _codex?.Renamed(Path.GetFileName(item.Shot.Path), Path.GetFileName(renamed));
        }

        return result;
    }

    /// <summary>
    /// Recycles the picture, and Elite's BMP when asked, then deletes its note, its thumbnail and the Codex pictures cut
    /// from it. Returns why it stopped, or null when it is done. Call it off the UI thread.
    /// </summary>
    public string? Delete(ScreenshotItem item, bool withBmp)
    {
        var thumbnail = ThumbnailName(item.Shot.Path, _files.Stat(item.Shot.Path));
        var cut = CutFrom(item);
        var reason = ScreenshotEdits.Delete(_files, item.Shot.Path, withBmp ? BmpOf(item) : null, _recycle);

        if (reason is not null)
        {
            return reason;
        }

        if (thumbnail is not null)
        {
            try
            {
                _files.Delete(Path.Combine(_thumbnails, thumbnail));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        foreach (var key in cut)
        {
            _codex?.Removed(key);
        }

        return null;
    }

    /// <summary>Every screenshot, newest first. Reads the folder: call it off the UI thread.</summary>
    public IReadOnlyList<ScreenshotItem> List()
    {
        var cut = _codexCut();

        return
        [
            .. ScreenshotFolder.List(_files, _folder).Select(shot =>
            {
                var (system, body) = Place(shot);

                return new ScreenshotItem(shot, _files.Stat(shot.Path)?.Length ?? 0, system, body, cut.Contains(Path.GetFileName(shot.Path)));
            }),
        ];
    }

    /// <summary>The picture's pixel size from its header; null when it cannot be read.</summary>
    public (int Width, int Height)? PixelSize(string path)
    {
        try
        {
            using var stream = _files.OpenRead(path);

            return stream is null ? null : JpegSize.TryRead(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Opens Explorer with <paramref name="path"/> selected, or on the screenshots folder when null.</summary>
    public void Show(string? path) => _show(path);

    /// <summary>
    /// The cached thumbnail of <paramref name="item"/>, made when its length or write time differs from the cached one.
    /// Decodes the file: call it off the UI thread.
    /// </summary>
    public Bitmap? Thumbnail(ScreenshotItem item)
    {
        var name = ThumbnailName(item.Shot.Path, _files.Stat(item.Shot.Path));

        if (name is null)
        {
            return null;
        }

        var cached = Path.Combine(_thumbnails, name);

        try
        {
            if (_files.OpenRead(cached) is { } existing)
            {
                using (existing)
                {
                    return new Bitmap(existing);
                }
            }

            var small = Decode(item.Shot.Path, ThumbnailWidth, ThumbnailHeight);

            if (small is null)
            {
                return null;
            }

            using var png = new MemoryStream();
            small.Save(png, new PngBitmapEncoderOptions());
            _files.WriteBytes(cached, png.ToArray());

            return small;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Deletes the cached thumbnails that no listed screenshot, at its current length and write time, uses.</summary>
    public void PruneThumbnails(IEnumerable<ScreenshotItem> current)
    {
        var keep = current
            .Select(item => ThumbnailName(item.Shot.Path, _files.Stat(item.Shot.Path)))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _files.Enumerate(_thumbnails, "*.png"))
        {
            if (keep.Contains(Path.GetFileName(path)))
            {
                continue;
            }

            try
            {
                _files.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// The picture decoded no larger than <paramref name="maxWidth"/> by <paramref name="maxHeight"/> and never larger
    /// than itself. Decodes the file: call it off the UI thread.
    /// </summary>
    public Bitmap? Decode(string path, int maxWidth, int maxHeight)
    {
        try
        {
            var size = PixelSize(path);

            using var stream = _files.OpenRead(path);

            if (stream is null)
            {
                return null;
            }

            var width = size is { } known
                ? Math.Max(1, (int)Math.Round(known.Width * Math.Min(1.0, Math.Min((double)maxWidth / known.Width, (double)maxHeight / known.Height))))
                : maxWidth;

            return Bitmap.DecodeToWidth(stream, width);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    internal static string? ThumbnailName(string path, FileState? state)
    {
        if (state is not { } stat)
        {
            return null;
        }

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..16];

        return string.Create(CultureInfo.InvariantCulture, $"{hash}-{stat.Length}-{stat.Written.Ticks}.png");
    }

    private IReadOnlyList<CodexPictureKey> CutFrom(ScreenshotItem item) =>
        _codex?.CutFrom(Path.GetFileName(item.Shot.Path)) ?? [];

    private static string Named(ScreenshotItem item, CodexPictureKey key) =>
        item.Shot.Note?.CodexEntries.FirstOrDefault(entry => entry.EntryId == key.EntryId)?.Name is { Length: > 0 } name
            ? name
            : string.Create(CultureInfo.InvariantCulture, $"entry {key.EntryId}");

    private static (string System, string Body) Place(KeptScreenshot shot)
    {
        if (shot.Note is { System: { Length: > 0 } system } note)
        {
            var body = note.Body ?? string.Empty;

            return (system, body.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase) ? body[(system.Length + 1)..] : body);
        }

        var stem = Path.GetFileNameWithoutExtension(shot.Path);

        return (stem.Length > 20 ? stem[20..] : string.Empty, string.Empty);
    }

    private void ShowInExplorer(string? path)
    {
        try
        {
            if (path is null)
            {
                _files.CreateFolder(_folder);
            }

            var info = new ProcessStartInfo("explorer.exe")
            {
                Arguments = path is null ? $"\"{_folder}\"" : $"/select,\"{path}\"",
            };

            Process.Start(info)?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
