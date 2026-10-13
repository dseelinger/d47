using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using D47.Core.Adventures;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>The three files that hold one slot's story state.</summary>
public sealed record StorySlotFiles(string Stories, string Adventures, string Chapters)
{
    /// <summary>The slot files in <paramref name="folder"/>.</summary>
    public static StorySlotFiles In(string folder) => new(
        Path.Combine(folder, "story.json"),
        Path.Combine(folder, "adventures.json"),
        Path.Combine(folder, "story-chapters.jsonl"));
}

/// <summary>
/// A maintainer's slots for testing stories, each holding its own story state, switched live. My story is the files in
/// the data folder; a bench slot is a folder under <c>data/story-bench</c>, and <c>data/story-bench/bench.json</c>
/// names the slots and the active one. Built only while the App has the bench on.
/// </summary>
public sealed partial class StoryBench
{
    /// <summary>The folder under the data folder that holds the bench slots and <c>bench.json</c>.</summary>
    public const string Folder = "story-bench";

    /// <summary>The name shown for the files in the data folder.</summary>
    public const string MyStory = "My story";

    /// <summary>The refusal while a chapter or a beat is being written.</summary>
    public const string Writing = "A chapter is being written. Switch slots once it is done.";

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

    /// <summary>Held across a change of the slots; <see cref="_state"/> is read without it.</summary>
    private readonly Lock _gate = new();
    private volatile State _state;

    public StoryBench(
        string data, IFileSystem files, StoryStore stories, AdventureStore adventures, StoryChapterArchive archive, ILogger<StoryBench> logger)
    {
        _data = data;
        _files = files;
        _stories = stories;
        _adventures = adventures;
        _archive = archive;
        _logger = logger;
        _state = Load(data, files);
    }

    /// <summary>Holds off the director's tick and writes during a switch, and forgets what it held of the old slot.</summary>
    public StoryDirector? Director { get; set; }

    /// <summary>Stores the Backstory setting.</summary>
    public Action<string> SetBackstory { get; set; } = _ => { };

    /// <summary>Raised on the switching thread after a switch.</summary>
    public event Action? Switched;

    /// <summary>The active bench slot, or null for My story.</summary>
    public string? Active => _state.Active;

    /// <summary>Whether a bench slot, not My story, is active.</summary>
    public bool OnBench => Active is not null;

    /// <summary>The bench slots, in the order they were made.</summary>
    public IReadOnlyList<string> Slots => _state.Slots;

    /// <summary>
    /// The files the stores open at startup: the active slot's while <paramref name="enabled"/>, and the data folder's
    /// otherwise, without reading <c>bench.json</c>.
    /// </summary>
    public static StorySlotFiles FilesFor(string data, bool enabled, IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return StorySlotFiles.In(enabled && Load(data, files).Active is { } active ? SlotFolder(data, active) : data);
    }

    /// <summary>Makes an empty bench slot. Returns a refusal or null.</summary>
    public string? Create(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (!IsSlotName(trimmed) || string.Equals(trimmed, MyStory, StringComparison.OrdinalIgnoreCase))
        {
            return "A slot name is 1 to 40 letters, digits, spaces, hyphens or underscores, is not a Windows device name, and is not \"My story\".";
        }

        lock (_gate)
        {
            if (_state.Slots.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                return $"There is already a slot called {trimmed}.";
            }

            try
            {
                _files.CreateFolder(SlotFolder(_data, trimmed));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not make the story bench slot {Slot}", trimmed);
                return $"The folder for {trimmed} could not be made.";
            }
            _state = _state with { Slots = [.. _state.Slots, trimmed] };
            Save();
        }

        _logger.LogInformation("Made the story bench slot {Slot}", trimmed);
        return null;
    }

    /// <summary>
    /// Makes <paramref name="slot"/> the active slot, null for My story: switches the outgoing slot's current stories
    /// off, reads the incoming slot's files, switches back on the stories it switched off when that slot was left, and
    /// sets the Backstory from <paramref name="frontierId"/>'s current story. Writes files, so it never runs on the
    /// tick. Returns a refusal or null.
    /// </summary>
    public string? SwitchTo(string? slot, string? frontierId, DateTimeOffset now)
    {
        lock (_gate)
        {
            var target = slot is null ? null : _state.Slots.FirstOrDefault(name => string.Equals(name, slot, StringComparison.OrdinalIgnoreCase));

            if (slot is not null && target is null)
            {
                return $"There is no slot called {slot}.";
            }

            if (string.Equals(target, _state.Active, StringComparison.Ordinal))
            {
                return $"{target ?? MyStory} is already the active slot.";
            }

            if (Director is { } held && !held.HoldForSwitch())
            {
                return Writing;
            }

            try
            {
                foreach (var (commander, story) in _stories.All().Where(entry => entry.Story.IsCurrent && !entry.Story.IsOff))
                {
                    _stories.Update(commander, story.Id, current => current.SwitchedOff(now));
                }

                var files = StorySlotFiles.In(target is null ? _data : SlotFolder(_data, target));

                if (target is not null)
                {
                    _files.CreateFolder(SlotFolder(_data, target));
                }

                _archive.Reopen(files.Chapters);
                _adventures.Repoint(files.Adventures);
                _stories.Reopen(files.Stories);

                if (_state.Left.FirstOrDefault(left => string.Equals(left.Slot, target, StringComparison.Ordinal)) is { } returning)
                {
                    foreach (var (commander, story) in _stories.All().Where(entry => entry.Story.IsCurrent && entry.Story.OffSpans is [.., { To: null } open] && open.From == returning.At))
                    {
                        _stories.Update(commander, story.Id, current => current.SwitchedOn(now));
                    }
                }

                _state = _state with
                {
                    Active = target,
                    Left =
                    [
                        .. _state.Left.Where(left => left.Slot != target && left.Slot != _state.Active),
                        new LeftSlot(_state.Active, now),
                    ],
                };
                Save();

                if (_stories.Current(frontierId) is { } current && Director?.Catalog.Find(current.Id) is { } card)
                {
                    SetBackstory(card.InYourWords);
                }
            }
            finally
            {
                Director?.ReleaseSwitch();
            }
        }

        _logger.LogInformation("Switched the story bench to {Slot}", slot ?? MyStory);
        Switched?.Invoke();
        return null;
    }

    private static string SlotFolder(string data, string name) => Path.Combine(data, Folder, name);

    private static string StatePath(string data) => Path.Combine(data, Folder, "bench.json");

    private static State Load(string data, IFileSystem files)
    {
        try
        {
            if (files.ReadText(StatePath(data)) is not { } text || JsonSerializer.Deserialize<State>(text, Json) is not { } state)
            {
                return new State();
            }

            var slots = (state.Slots ?? []).Where(IsSlotName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var active = slots.FirstOrDefault(name => string.Equals(name, state.Active, StringComparison.Ordinal));

            return new State { Active = active, Slots = slots, Left = [.. (state.Left ?? []).OfType<LeftSlot>()] };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new State();
        }
    }

    /// <summary>Called holding <see cref="_gate"/>.</summary>
    private void Save()
    {
        try
        {
            _files.CreateFolder(Path.Combine(_data, Folder));
            _files.WriteText(StatePath(_data), JsonSerializer.Serialize(_state, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not write {Path}", StatePath(_data));
        }
    }

    private static bool IsSlotName(string? name) =>
        name is not null && SlotName().IsMatch(name) && !DeviceName().IsMatch(name);

    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceName();

    [GeneratedRegex(@"^[A-Za-z0-9_\-]([A-Za-z0-9 _\-]{0,38}[A-Za-z0-9_\-])?$")]
    private static partial Regex SlotName();

    /// <summary>When a slot was left; <see cref="Slot"/> is null for My story.</summary>
    private sealed record LeftSlot(string? Slot, DateTimeOffset At);

    private sealed record State
    {
        public string? Active { get; init; }

        public IReadOnlyList<string> Slots { get; init; } = [];

        public IReadOnlyList<LeftSlot> Left { get; init; } = [];
    }
}
