using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Adventures;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>The story bench: Jump, Next objective and Checkpoints with Rewind, over the story in the data folder. Built only while the App has the bench on.</summary>
public sealed partial class StoryBench
{
    /// <summary>The folder under the data folder that holds the checkpoints.</summary>
    public const string Folder = "story-bench";

    /// <summary>The refusal while a chapter or a beat is being written.</summary>
    public const string Writing = "A chapter is being written. Try again once it is done.";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _data;
    private readonly IFileSystem _files;
    private readonly StoryStore _stories;
    private readonly AdventureStore _adventures;
    private readonly StoryChapterArchive _archive;
    private readonly ILogger _logger;

    /// <summary>Held across a rewind.</summary>
    private readonly Lock _gate = new();
    private StoryDirector? _director;

    public StoryBench(
        string data, IFileSystem files, StoryStore stories, AdventureStore adventures, StoryChapterArchive archive, ILogger<StoryBench> logger)
    {
        _data = data;
        _files = files;
        _stories = stories;
        _adventures = adventures;
        _archive = archive;
        _logger = logger;
        _stories.Changed += Evaluate;
    }

    /// <summary>Holds off the director's tick and writes during a rewind; a story it shelves clears the checkpoints.</summary>
    public StoryDirector? Director
    {
        get => _director;
        set
        {
            if (_director is not null)
            {
                _director.WritingChanged -= Evaluate;
                _director.Shelved -= ClearCheckpoints;
            }

            _director = value;

            if (value is not null)
            {
                value.WritingChanged += Evaluate;
                value.Shelved += ClearCheckpoints;
            }
        }
    }

    /// <summary>Deletes <c>bench.json</c> and every folder under the bench folder except the checkpoints'.</summary>
    public static void RemoveSlots(string data, IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var root = Path.Combine(data, Folder);
        var slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var path in files.Enumerate(root, "*", recursive: true))
            {
                var first = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

                if (first != StateFile && !string.Equals(first, CheckpointOwner, StringComparison.OrdinalIgnoreCase))
                {
                    slots.Add(first);
                }
            }

            slots.UnionWith(NamedSlots(files.ReadText(Path.Combine(root, StateFile))));

            foreach (var slot in slots)
            {
                files.DeleteFolder(Path.Combine(root, slot));
            }

            files.Delete(Path.Combine(root, StateFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }
    }

    private static IEnumerable<string> NamedSlots(string? text)
    {
        if (text is null)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(text);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("slots", out var named)
                || named.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return
            [
                .. named.EnumerateArray()
                    .Where(name => name.ValueKind == JsonValueKind.String)
                    .Select(name => name.GetString()!)
                    .Where(name => name.Length > 0
                        && name != ".."
                        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                        && !string.Equals(name, CheckpointOwner, StringComparison.OrdinalIgnoreCase)),
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Fires the beat the running chapter or Short Story <paramref name="key"/> is waiting on, without its journal event.
    /// Writes a file, so it never runs on the tick. Returns a refusal or null.
    /// </summary>
    public string? Step(string? frontierId, string key, DateTimeOffset now)
    {
        if (Director is not { } director)
        {
            return "There is no story director to step.";
        }

        if (director.IsWriting(frontierId) || director.IsRewriting(frontierId))
        {
            return Writing;
        }

        if (director.Book.Standing(frontierId, key)?.CurrentTrigger is not { Kind: TriggerKind.Beacon } beacon)
        {
            return director.Book.Step(frontierId, key, now);
        }

        if (beacon.SystemAddress is not { } address || !D47.Core.Persona.GuardianCores.Beacons.ContainsKey(address))
        {
            return "The beacon beat has no beacon system to scan.";
        }

        return director.Book.Step(frontierId, key, now) ?? director.RecordSteppedScan(frontierId, address, now);
    }
}
