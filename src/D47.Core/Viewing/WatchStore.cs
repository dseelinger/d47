using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Viewing;

/// <summary>
/// A title in the install's watch list or with a saved place, keyed by source and id. <paramref name="Video"/> is what
/// the title was when it was last added or played, so a row can be drawn and played again without asking the source.
/// </summary>
public sealed record WatchEntry(
    string Source,
    string Id,
    bool InList,
    DateTimeOffset? AddedAt,
    TimeSpan Place,
    bool Finished,
    DateTimeOffset? LastWatched,
    VideoTitle? Video = null);

/// <summary>
/// Your list and the place reached in each title, one for the install, kept at <c>data\watch.json</c>.
/// A title is keyed by its source and id, so catalogue titles and a Commander's own files both fit.
/// </summary>
public sealed class WatchStore(string path, IFileSystem files, ILogger<WatchStore> logger)
{
    private readonly Lock _gate = new();

    private Dictionary<(string Source, string Id), WatchEntry> _entries = [];

    public event Action? Changed;

    /// <summary>Reads the file. A file that does not parse is copied to <c>watch.json.bad</c>, removed, and the store starts empty.</summary>
    public void Load()
    {
        string? text;

        try
        {
            text = files.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {Path}; the watch list starts empty", path);
            return;
        }

        if (text is null)
        {
            return;
        }

        Dictionary<(string Source, string Id), WatchEntry> loaded;

        try
        {
            var document = JsonSerializer.Deserialize<Document>(text, StoreJson.OmittingNulls)
                ?? throw new JsonException("The file is empty.");
            loaded = [];

            foreach (var row in document.Titles ?? [])
            {
                if (string.IsNullOrWhiteSpace(row.Source) || string.IsNullOrWhiteSpace(row.Id) || row.Seconds < 0)
                {
                    continue;
                }

                loaded[(row.Source, row.Id)] = new WatchEntry(
                    row.Source, row.Id, row.InList, row.AddedAt, TimeSpan.FromSeconds(row.Seconds), row.Finished, row.LastWatched, ReadVideo(row));
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException or NotSupportedException)
        {
            logger.LogWarning(ex, "{Path} could not be read; it is set aside and the watch list starts empty", path);
            SetAside();
            return;
        }

        lock (_gate)
        {
            _entries = loaded;
        }
    }

    /// <summary>Titles in Your list, oldest added first.</summary>
    public IReadOnlyList<WatchEntry> YourList()
    {
        lock (_gate)
        {
            return [.. _entries.Values.Where(entry => entry.InList).OrderBy(entry => entry.AddedAt)];
        }
    }

    /// <summary>Titles with a place that are not finished, most recently watched first.</summary>
    public IReadOnlyList<WatchEntry> CarryOnWatching()
    {
        lock (_gate)
        {
            return
            [
                .. _entries.Values
                    .Where(entry => entry.Place > TimeSpan.Zero && !entry.Finished)
                    .OrderByDescending(entry => entry.LastWatched),
            ];
        }
    }

    public WatchEntry? Find(string source, string id)
    {
        lock (_gate)
        {
            return _entries.GetValueOrDefault((source, id));
        }
    }

    /// <summary>Adds the title to Your list; one already there keeps its position in the list. <paramref name="video"/> replaces what is kept of the title when given.</summary>
    public void Add(string source, string id, DateTimeOffset now, VideoTitle? video = null) =>
        Change(source, id, entry => entry is { InList: true }
            ? entry with { Video = video ?? entry.Video }
            : (entry ?? Blank(source, id)) with { InList = true, AddedAt = now, Video = video ?? entry?.Video });

    /// <summary>Takes the title off Your list; its saved place stays.</summary>
    public void Remove(string source, string id) =>
        Change(source, id, entry => entry is null ? null : entry with { InList = false, AddedAt = null });

    /// <summary>Saves where the title was left, and whether it is finished. <paramref name="video"/> replaces what is kept of the title when given.</summary>
    public void SavePlace(string source, string id, TimeSpan place, bool finished, DateTimeOffset now, VideoTitle? video = null)
    {
        var kept = place < TimeSpan.Zero ? TimeSpan.Zero : place;

        Change(source, id, entry => (entry ?? Blank(source, id)) with
        {
            Place = kept,
            Finished = finished,
            LastWatched = now,
            Video = video ?? entry?.Video,
        });
    }

    /// <summary>The source ids of every title in Your list or with a saved place.</summary>
    public IReadOnlyList<string> Sources()
    {
        lock (_gate)
        {
            return [.. _entries.Values.Select(entry => entry.Source).Distinct(StringComparer.Ordinal)];
        }
    }

    private static WatchEntry Blank(string source, string id) =>
        new(source, id, false, null, TimeSpan.Zero, false, null);

    private void Change(string source, string id, Func<WatchEntry?, WatchEntry?> edit)
    {
        string json;

        lock (_gate)
        {
            var current = _entries.GetValueOrDefault((source, id));
            var next = edit(current);

            if (next == current)
            {
                return;
            }

            var updated = new Dictionary<(string Source, string Id), WatchEntry>(_entries);

            if (next is null || next is { InList: false, Finished: false } && next.Place == TimeSpan.Zero)
            {
                updated.Remove((source, id));
            }
            else
            {
                updated[(source, id)] = next;
            }

            _entries = updated;
            json = Serialize(updated.Values);
        }

        StoreFiles.TryWriteText(files, path, json, logger);
        Changed?.Invoke();
    }

    private static string Serialize(IEnumerable<WatchEntry> entries) =>
        JsonSerializer.Serialize(
            new Document
            {
                Titles = [.. entries.Select(entry => new Row
                {
                    Source = entry.Source,
                    Id = entry.Id,
                    InList = entry.InList,
                    AddedAt = entry.AddedAt,
                    Seconds = entry.Place.TotalSeconds,
                    Finished = entry.Finished,
                    LastWatched = entry.LastWatched,
                    Video = WriteVideo(entry.Video),
                })],
            },
            StoreJson.OmittingNulls);

    private static VideoRow? WriteVideo(VideoTitle? video) =>
        video is null
            ? null
            : new VideoRow
            {
                Title = video.Title,
                Kind = video.Kind,
                Licence = video.Licence,
                LengthSeconds = video.Length?.TotalSeconds,
                Media = video.Media?.OriginalString,
                Subtitles = video.Subtitles?.OriginalString,
                Credit = video.Credit,
                Unplayable = video.Unplayable,
            };

    private const double MostSeconds = 10_000_000;

    private static VideoTitle? ReadVideo(Row row) =>
        row.Video is { } video && !string.IsNullOrWhiteSpace(video.Title)
            ? new VideoTitle(
                row.Source,
                row.Id,
                video.Title,
                video.Kind,
                video.Licence ?? string.Empty,
                video.LengthSeconds is > 0 and < MostSeconds ? TimeSpan.FromSeconds(video.LengthSeconds.Value) : null,
                AddressOf(video.Media),
                AddressOf(video.Subtitles),
                video.Credit,
                video.Unplayable)
            : null;

    private static Uri? AddressOf(string? text) =>
        Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri) ? uri : null;

    private void SetAside()
    {
        try
        {
            files.Copy(path, path + ".bad");
            files.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not set {Path} aside", path);
        }
    }

    private sealed class Document
    {
        public List<Row>? Titles { get; set; }
    }

    private sealed class Row
    {
        public string Source { get; set; } = "";

        public string Id { get; set; } = "";

        public bool InList { get; set; }

        public DateTimeOffset? AddedAt { get; set; }

        public double Seconds { get; set; }

        public bool Finished { get; set; }

        public DateTimeOffset? LastWatched { get; set; }

        public VideoRow? Video { get; set; }
    }

    private sealed class VideoRow
    {
        public string Title { get; set; } = "";

        public VideoKind Kind { get; set; }

        public string? Licence { get; set; }

        public double? LengthSeconds { get; set; }

        public string? Media { get; set; }

        public string? Subtitles { get; set; }

        public string? Credit { get; set; }

        public string? Unplayable { get; set; }
    }
}
