using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Adventures;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>A story chapter moved out of the adventure file, with when each of its beats fired.</summary>
/// <param name="PickedAt">The pick of the story run the chapter belongs to; null when no record of that run was found.</param>
public sealed record ArchivedChapter(
    string FrontierId,
    string StoryId,
    DateTimeOffset? PickedAt,
    Adventure Adventure,
    IReadOnlyList<DateTimeOffset> Fired,
    IReadOnlyList<string> FiredBy)
{
    /// <summary>The chapter's beats as fired, for the log.</summary>
    public AdventureStanding Standing => new() { Adventure = Adventure, Fired = Fired, FiredBy = FiredBy };

    /// <summary>Whether the chapter belongs to this run of the story.</summary>
    public bool Of(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        return string.Equals(StoryId, story.Id, StringComparison.OrdinalIgnoreCase) && PickedAt == story.PickedAt;
    }
}

/// <summary>Finished story chapters — <c>data/story-chapters.jsonl</c>, one chapter per line, appended.</summary>
public sealed class StoryChapterArchive
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string? _path;
    private readonly ILogger? _logger;
    private readonly Lock _gate = new();
    private readonly List<ArchivedChapter> _chapters;

    /// <summary>Whether the file ends without a line terminator, as a process killed mid-append leaves it.</summary>
    private bool _danglingLine;

    private StoryChapterArchive(string? path, ILogger? logger, List<ArchivedChapter> chapters, bool danglingLine)
    {
        _path = path;
        _logger = logger;
        _chapters = chapters;
        _danglingLine = danglingLine;
    }

    /// <summary>Held in memory only.</summary>
    public static StoryChapterArchive InMemory() => new(null, null, [], false);

    public static StoryChapterArchive Open(string path, ILogger<StoryChapterArchive> logger) =>
        new(path, logger, Read(path, logger), EndsMidLine(path));

    /// <summary>This Commander's archived chapters, oldest first.</summary>
    public IReadOnlyList<ArchivedChapter> For(string? frontierId)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_gate)
        {
            return [.. _chapters.Where(chapter => string.Equals(chapter.FrontierId, commander, StringComparison.Ordinal))];
        }
    }

    /// <summary>The archived chapter of this run of the story with this key, or null.</summary>
    public ArchivedChapter? Find(string? frontierId, Story story, string key) =>
        For(frontierId).LastOrDefault(chapter => chapter.Of(story) && string.Equals(chapter.Adventure.Key, key, StringComparison.OrdinalIgnoreCase));

    public void Append(ArchivedChapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        lock (_gate)
        {
            _chapters.Add(chapter);

            if (_path is null)
            {
                return;
            }

            var line = new Line
            {
                FrontierId = chapter.FrontierId,
                StoryId = chapter.StoryId,
                PickedAt = chapter.PickedAt,
                Adventure = AdventureStore.ToJson(chapter.Adventure),
                Fired = chapter.Fired,
                FiredBy = chapter.FiredBy,
            };

            try
            {
                var lead = _danglingLine ? Environment.NewLine : string.Empty;

                File.AppendAllText(_path, lead + JsonSerializer.Serialize(line, Json) + Environment.NewLine);
                _danglingLine = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger?.LogError(ex, "Could not append to {Path}", _path);
            }
        }
    }

    private static List<ArchivedChapter> Read(string path, ILogger logger)
    {
        var chapters = new List<ArchivedChapter>();

        if (!File.Exists(path))
        {
            return chapters;
        }

        try
        {
            var number = 0;

            foreach (var text in File.ReadLines(path))
            {
                number++;

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (Parse(text) is { } chapter)
                {
                    chapters.Add(chapter);
                }
                else
                {
                    logger.LogWarning("Skipped line {Number} of {Path}: it is not an archived chapter", number, path);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not read {Path}", path);
        }

        return chapters;
    }

    private static ArchivedChapter? Parse(string text)
    {
        try
        {
            if (JsonSerializer.Deserialize<Line>(text, Json) is not { StoryId: { Length: > 0 } storyId, Adventure: { } element } line
                || AdventureStore.FromJson(element, []) is not { } adventure)
            {
                return null;
            }

            return new ArchivedChapter(
                line.FrontierId ?? AdventureStore.NoCommander,
                storyId,
                line.PickedAt,
                adventure,
                line.Fired ?? [],
                line.FiredBy ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool EndsMidLine(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);

            if (stream.Length == 0)
            {
                return false;
            }

            stream.Seek(-1, SeekOrigin.End);
            return stream.ReadByte() is not ((byte)'\n' or (byte)'\r');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private sealed class Line
    {
        public string? FrontierId { get; set; }

        public string? StoryId { get; set; }

        public DateTimeOffset? PickedAt { get; set; }

        public JsonElement? Adventure { get; set; }

        public IReadOnlyList<DateTimeOffset>? Fired { get; set; }

        public IReadOnlyList<string>? FiredBy { get; set; }
    }
}
