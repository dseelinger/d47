using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Characters;

/// <summary>
/// Each Commander's custom characters and their notes on the captain, the tower and story cast, keyed by
/// Frontier id. Reads and writes block on file I/O: call from the pool, not from a tick.
/// </summary>
public sealed class CharacterStore(string path, IFileSystem files, ILogger<CharacterStore> logger)
{
    /// <summary>The key used where the journals never said who was flying.</summary>
    public const string NoCommander = "";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();

    private IReadOnlyDictionary<string, Held> _commanders = new Dictionary<string, Held>(StringComparer.Ordinal);
    private IReadOnlyList<CharacterProblem> _problems = [];

    /// <summary>The file's contents as last read.</summary>
    private string? _seen;

    /// <summary>Raised when the set changed, whoever wrote it.</summary>
    public event Action? Changed;

    public string Path => path;

    /// <summary>Entries that were refused, and why.</summary>
    public IReadOnlyList<CharacterProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>This Commander's characters, in the order they were added.</summary>
    public IReadOnlyList<CustomCharacter> CharactersOf(string? fid) => HeldBy(fid).Characters;

    public CustomCharacter? Find(string? fid, string id) =>
        CharactersOf(fid).FirstOrDefault(character => string.Equals(character.Id, id, StringComparison.Ordinal));

    /// <summary>This Commander's notes on the carrier captain or the tower, or null.</summary>
    public string? NotesFor(string? fid, CarrierCharacter who) =>
        who == CarrierCharacter.Captain ? HeldBy(fid).CaptainNotes : HeldBy(fid).TowerNotes;

    /// <summary>This Commander's notes on one story's cast member, or null.</summary>
    public string? CastNotes(string? fid, string storyId, string castId) =>
        HeldBy(fid).Cast.FirstOrDefault(entry => entry.Is(storyId, castId))?.Notes;

    /// <summary>Why this Commander could not save <paramref name="character"/>, or null when it can be saved.</summary>
    public CharacterRefusal? Check(string? fid, CustomCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return Refusal(Tidy(character), CharactersOf(fid));
    }

    /// <summary>Adds the character, or replaces the one with its id; null when saved.</summary>
    /// <exception cref="ArgumentException">The id is not eight hex characters, or another Commander holds it.</exception>
    public CharacterRefusal? Set(string? fid, CustomCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!CustomCharacter.IsId(character.Id))
        {
            throw new ArgumentException("A character id is eight lower-case hex characters.", nameof(character));
        }

        var commander = fid ?? NoCommander;
        var commanders = Commanders();

        if (commanders.Any(pair =>
                !string.Equals(pair.Key, commander, StringComparison.Ordinal) &&
                pair.Value.Characters.Any(other => string.Equals(other.Id, character.Id, StringComparison.Ordinal))))
        {
            throw new ArgumentException($"Another Commander holds the character id {character.Id}.", nameof(character));
        }

        var tidy = Tidy(character);
        var held = commanders.GetValueOrDefault(commander, Held.Empty);

        if (Refusal(tidy, held.Characters) is { } refusal)
        {
            return refusal;
        }

        var replaced = held.Characters.Any(other => string.Equals(other.Id, tidy.Id, StringComparison.Ordinal));

        Save(commander, held with
        {
            Characters = replaced
                ? [.. held.Characters.Select(other => string.Equals(other.Id, tidy.Id, StringComparison.Ordinal) ? tidy : other)]
                : [.. held.Characters, tidy],
        });

        return null;
    }

    /// <summary>Removes one of this Commander's characters.</summary>
    public bool Remove(string? fid, string id)
    {
        var held = HeldBy(fid);

        if (!held.Characters.Any(character => string.Equals(character.Id, id, StringComparison.Ordinal)))
        {
            return false;
        }

        Save(fid ?? NoCommander, held with
        {
            Characters = [.. held.Characters.Where(character => !string.Equals(character.Id, id, StringComparison.Ordinal))],
        });

        return true;
    }

    /// <summary>Replaces this Commander's notes on the captain or the tower; empty clears them. Null when saved.</summary>
    public CharacterRefusal? SetNotes(string? fid, CarrierCharacter who, string? notes)
    {
        var tidy = Blank(notes);

        if (NotesRefusal(tidy) is { } refusal)
        {
            return refusal;
        }

        var held = HeldBy(fid);

        Save(fid ?? NoCommander, who == CarrierCharacter.Captain
            ? held with { CaptainNotes = tidy }
            : held with { TowerNotes = tidy });

        return null;
    }

    /// <summary>Replaces this Commander's notes on one story's cast member; empty clears them. Null when saved.</summary>
    public CharacterRefusal? SetCastNotes(string? fid, string storyId, string castId, string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(castId);

        var tidy = Blank(notes);

        if (NotesRefusal(tidy) is { } refusal)
        {
            return refusal;
        }

        var held = HeldBy(fid);
        var others = held.Cast.Where(entry => !entry.Is(storyId, castId));

        Save(fid ?? NoCommander, held with
        {
            Cast = tidy is null ? [.. others] : [.. others, new CastNote(storyId.Trim(), castId.Trim(), tidy)],
        });

        return null;
    }

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (files.Stat(path) is null)
            {
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _commanders = new Dictionary<string, Held>(StringComparer.Ordinal);
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            var read = files.ReadText(path);

            if (read is null)
            {
                return false;
            }

            text = read;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read the character file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    private IReadOnlyDictionary<string, Held> Commanders()
    {
        lock (_gate)
        {
            return _commanders;
        }
    }

    private Held HeldBy(string? fid) => Commanders().GetValueOrDefault(fid ?? NoCommander, Held.Empty);

    /// <summary>Writes the file with one Commander's entry replaced, then re-reads it so the store holds only what the file says.</summary>
    private void Save(string commander, Held held)
    {
        var next = new Dictionary<string, Held>(Commanders(), StringComparer.Ordinal) { [commander] = held };

        var file = new CharacterFile
        {
            Commanders =
            [
                .. next
                    .Where(pair => !pair.Value.IsEmpty)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new CommanderLine
                    {
                        FrontierId = pair.Key,
                        Characters = [.. pair.Value.Characters.Select(character => new CharacterLine
                        {
                            Id = character.Id,
                            Name = character.Name,
                            Voice = character.Voice is { } voice
                                ? new VoiceLine { Provider = voice.Provider, VoiceId = voice.VoiceId }
                                : null,
                            Notes = character.Notes,
                            Aboard = character.Aboard,
                            System = character.System,
                        })],
                        CaptainNotes = pair.Value.CaptainNotes,
                        TowerNotes = pair.Value.TowerNotes,
                        Cast = [.. pair.Value.Cast.Select(entry => new CastLine
                        {
                            StoryId = entry.StoryId,
                            CastId = entry.CastId,
                            Notes = entry.Notes,
                        })],
                    }),
            ],
        };

        var text = JsonSerializer.Serialize(file, Json);

        try
        {
            files.WriteText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the character file");
            return;
        }

        Reload(text);
    }

    private bool Reload(string text)
    {
        CharacterFile? file;

        try
        {
            file = JsonSerializer.Deserialize<CharacterFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _problems = [new CharacterProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The character file could not be read");
            Changed?.Invoke();
            return true;
        }

        var commanders = new Dictionary<string, Held>(StringComparer.Ordinal);
        var problems = new List<CharacterProblem>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in file?.Commanders ?? [])
        {
            var fid = (line.FrontierId ?? NoCommander).Trim();
            var where = fid.Length > 0 ? $"Commander {fid}" : "no Commander";

            if (commanders.ContainsKey(fid))
            {
                problems.Add(new CharacterProblem(where, "the Commander appears twice, and a Commander has one entry."));
                continue;
            }

            commanders[fid] = new Held(
                ReadCharacters(line, where, ids, problems),
                ReadNotes(line.CaptainNotes, $"{where}, the captain", problems),
                ReadNotes(line.TowerNotes, $"{where}, the tower", problems),
                ReadCast(line, where, problems));
        }

        lock (_gate)
        {
            _commanders = commanders;
            _problems = problems;
            _seen = text;
        }

        Changed?.Invoke();
        return true;
    }

    private static List<CustomCharacter> ReadCharacters(
        CommanderLine line, string commander, HashSet<string> ids, List<CharacterProblem> problems)
    {
        var kept = new List<CustomCharacter>();

        foreach (var entry in line.Characters)
        {
            var name = Blank(entry.Name);
            var where = name is null ? commander : $"{commander}, {name}";

            if (!CustomCharacter.IsId(entry.Id))
            {
                problems.Add(new CharacterProblem(where, "its id is not eight hex characters."));
                continue;
            }

            var voice = Blank(entry.Voice?.Provider) is { } provider && Blank(entry.Voice?.VoiceId) is { } voiceId
                ? new CharacterVoice(provider, voiceId)
                : null;

            var character = new CustomCharacter(
                entry.Id!, name ?? string.Empty, voice, Blank(entry.Notes), entry.Aboard, Blank(entry.System));

            if (Refusal(character, kept) is { } refusal)
            {
                problems.Add(new CharacterProblem(where, refusal.Reason));
                continue;
            }

            if (!ids.Add(character.Id))
            {
                problems.Add(new CharacterProblem(where, $"the id {character.Id} is already in use."));
                continue;
            }

            kept.Add(character);
        }

        return kept;
    }

    private static string? ReadNotes(string? notes, string where, List<CharacterProblem> problems)
    {
        var tidy = Blank(notes);

        if (NotesRefusal(tidy) is { } refusal)
        {
            problems.Add(new CharacterProblem(where, refusal.Reason));
            return null;
        }

        return tidy;
    }

    private static List<CastNote> ReadCast(CommanderLine line, string commander, List<CharacterProblem> problems)
    {
        var kept = new List<CastNote>();

        foreach (var entry in line.Cast)
        {
            var story = Blank(entry.StoryId);
            var cast = Blank(entry.CastId);
            var where = $"{commander}, story {story ?? "(none)"}, cast {cast ?? "(none)"}";

            if (story is null || cast is null)
            {
                problems.Add(new CharacterProblem(where, "it names no story id or no cast id, and those are the key."));
                continue;
            }

            if (kept.Any(other => other.Is(story, cast)))
            {
                problems.Add(new CharacterProblem(where, "the cast member appears twice, and a cast member has one set of notes."));
                continue;
            }

            if (ReadNotes(entry.Notes, where, problems) is { } notes)
            {
                kept.Add(new CastNote(story, cast, notes));
            }
        }

        return kept;
    }

    private static CharacterRefusal? Refusal(CustomCharacter character, IReadOnlyList<CustomCharacter> others)
    {
        if (character.Name.Length == 0)
        {
            return new CharacterRefusal(CharacterField.Name, "Give this character a name.");
        }

        if (character.Name.Length > CustomCharacter.MaxName)
        {
            return new CharacterRefusal(CharacterField.Name, $"Keep the name to {CustomCharacter.MaxName} characters.");
        }

        if (others.Any(other =>
                !string.Equals(other.Id, character.Id, StringComparison.Ordinal) &&
                string.Equals(other.Name, character.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return new CharacterRefusal(CharacterField.Name, $"You already have a character named {character.Name}.");
        }

        if (!character.Aboard && character.System is null)
        {
            return new CharacterRefusal(CharacterField.System, "Name the system they are in, or check Aboard.");
        }

        return NotesRefusal(character.Notes);
    }

    private static CharacterRefusal? NotesRefusal(string? notes) =>
        notes is { Length: > CustomCharacter.MaxNotes }
            ? new CharacterRefusal(CharacterField.Notes, $"Keep the notes to {CustomCharacter.MaxNotes} characters.")
            : null;

    private static CustomCharacter Tidy(CustomCharacter character) =>
        character with
        {
            Name = Blank(character.Name) ?? string.Empty,
            Notes = Blank(character.Notes),
            System = Blank(character.System),
        };

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Held(
        IReadOnlyList<CustomCharacter> Characters,
        string? CaptainNotes,
        string? TowerNotes,
        IReadOnlyList<CastNote> Cast)
    {
        public static readonly Held Empty = new([], null, null, []);

        public bool IsEmpty => Characters.Count == 0 && CaptainNotes is null && TowerNotes is null && Cast.Count == 0;
    }

    private sealed record CastNote(string StoryId, string CastId, string Notes)
    {
        public bool Is(string storyId, string castId) =>
            string.Equals(StoryId, storyId.Trim(), StringComparison.Ordinal) &&
            string.Equals(CastId, castId.Trim(), StringComparison.Ordinal);
    }

    private sealed record CharacterFile
    {
        public IReadOnlyList<CommanderLine> Commanders { get; init; } = [];
    }

    private sealed record CommanderLine
    {
        public string? FrontierId { get; init; }

        public IReadOnlyList<CharacterLine> Characters { get; init; } = [];

        public string? CaptainNotes { get; init; }

        public string? TowerNotes { get; init; }

        public IReadOnlyList<CastLine> Cast { get; init; } = [];
    }

    private sealed record CharacterLine
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public VoiceLine? Voice { get; init; }

        public string? Notes { get; init; }

        public bool Aboard { get; init; }

        public string? System { get; init; }
    }

    private sealed record VoiceLine
    {
        public string? Provider { get; init; }

        public string? VoiceId { get; init; }
    }

    private sealed record CastLine
    {
        public string? StoryId { get; init; }

        public string? CastId { get; init; }

        public string? Notes { get; init; }
    }
}
