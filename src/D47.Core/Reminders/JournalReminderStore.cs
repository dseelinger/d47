using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Reminders;

/// <summary>One reminder the file was asked to hold, and why it was refused.</summary>
public sealed record JournalReminderProblem(string Where, string Reason);

/// <summary>The Commanders' journal-triggered reminders, per Frontier id, in one file beside the executable.</summary>
public sealed class JournalReminderStore(string path, ILogger<JournalReminderStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The longest sentence a reminder may carry.</summary>
    public const int MaxSentenceLength = 300;

    /// <summary>The most reminders one Commander may hold.</summary>
    public const int MaxReminders = 64;

    private readonly Lock _gate = new();

    /// <summary>Held for the whole of a write, so two pool saves never share the pending file.</summary>
    private readonly Lock _saveGate = new();

    private Dictionary<string, IReadOnlyList<JournalReminder>> _byCommander = new(StringComparer.Ordinal);
    private IReadOnlyList<JournalReminderProblem> _problems = [];

    /// <summary>The file's contents as last read or written.</summary>
    private string? _seen;

    /// <summary>The file did not parse; nothing is written over it until it does.</summary>
    private bool _unreadable;

    /// <summary>Runs a save off the calling thread; the tick marks a reminder fired and must not write.</summary>
    public Action<Action> Dispatch { get; init; } = work => _ = Task.Run(work);

    /// <summary>Raised when the set changed, whoever changed it.</summary>
    public event Action? Changed;

    public string Path => path;

    /// <summary>Reminders that were refused, and why.</summary>
    public IReadOnlyList<JournalReminderProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>One Commander's reminders, oldest first.</summary>
    public IReadOnlyList<JournalReminder> For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId, []);
        }
    }

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _byCommander = new Dictionary<string, IReadOnlyList<JournalReminder>>(StringComparer.Ordinal);
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not read the journal reminder file");
            return false;
        }

        lock (_gate)
        {
            if (string.Equals(text, _seen, StringComparison.Ordinal))
            {
                return false;
            }
        }

        Reload(text);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Adds one reminder for a Commander and writes the file; false when the file would refuse it, the Commander
    /// already holds the most, or the file is unreadable.
    /// </summary>
    public bool Add(string frontierId, JournalReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        lock (_gate)
        {
            var held = _byCommander.GetValueOrDefault(frontierId, []);

            if (_unreadable
                || string.IsNullOrWhiteSpace(frontierId)
                || string.IsNullOrWhiteSpace(reminder.Id)
                || Refused(reminder.Sentence, reminder.Trigger, reminder.Argument, held.Count(existing => existing.Id != reminder.Id)) is not null)
            {
                return false;
            }

            Replace(frontierId, [.. held.Where(existing => existing.Id != reminder.Id), reminder]);
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Marks one armed reminder fired in memory at once and saves on the pool; false when it was not armed.</summary>
    public bool MarkFired(string frontierId, string id)
    {
        lock (_gate)
        {
            var held = _byCommander.GetValueOrDefault(frontierId, []);

            if (!held.Any(reminder => reminder.Id == id && reminder.State == JournalReminderState.Armed))
            {
                return false;
            }

            Replace(frontierId, [.. held.Select(reminder => reminder.Id == id
                ? reminder with { State = JournalReminderState.Fired }
                : reminder)]);
        }

        Dispatch(Save);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Drops a Commander's fired reminders in memory at once and saves on the pool; returns how many went.</summary>
    public int RemoveFired(string frontierId)
    {
        int removed;

        lock (_gate)
        {
            var held = _byCommander.GetValueOrDefault(frontierId, []);
            var kept = held.Where(reminder => reminder.State == JournalReminderState.Armed).ToArray();
            removed = held.Count - kept.Length;

            if (removed == 0)
            {
                return 0;
            }

            Replace(frontierId, kept);
        }

        Dispatch(Save);
        Changed?.Invoke();
        return removed;
    }

    /// <summary>Removes one reminder and writes the file; false when the Commander holds no reminder with that id.</summary>
    public bool Remove(string frontierId, string id)
    {
        lock (_gate)
        {
            var held = _byCommander.GetValueOrDefault(frontierId, []);

            if (_unreadable || held.All(reminder => reminder.Id != id))
            {
                return false;
            }

            Replace(frontierId, [.. held.Where(reminder => reminder.Id != id)]);
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Call holding <see cref="_gate"/>.</summary>
    private void Replace(string frontierId, IReadOnlyList<JournalReminder> reminders) =>
        _byCommander = new Dictionary<string, IReadOnlyList<JournalReminder>>(_byCommander, StringComparer.Ordinal)
        {
            [frontierId] = reminders,
        };

    private void Save()
    {
        lock (_saveGate)
        {
            Document document;

            lock (_gate)
            {
                if (_unreadable)
                {
                    return;
                }

                document = new Document
                {
                    Commanders =
                    [
                        .. _byCommander
                            .Where(pair => pair.Value.Count > 0)
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair => new CommanderRecord
                            {
                                FrontierId = pair.Key,
                                Reminders =
                                [
                                    .. pair.Value.Select(reminder => new ReminderRecord
                                    {
                                        Id = reminder.Id,
                                        Sentence = reminder.Sentence,
                                        Trigger = reminder.Trigger,
                                        Argument = reminder.Argument,
                                        State = reminder.State,
                                        Set = reminder.Set,
                                    }),
                                ],
                            }),
                    ],
                };
            }

            var text = JsonSerializer.Serialize(document, Json);

            try
            {
                AtomicFile.WriteAllText(path, text);

                lock (_gate)
                {
                    _seen = text;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not write the journal reminder file");
            }
        }
    }

    private void Reload(string text)
    {
        var loaded = new Dictionary<string, List<JournalReminder>>(StringComparer.Ordinal);
        var problems = new List<JournalReminderProblem>();
        var unreadable = false;

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    problems.Add(new JournalReminderProblem("a Commander", "it has no Frontier id."));
                    continue;
                }

                var frontierId = commander.FrontierId.Trim();

                if (!loaded.TryGetValue(frontierId, out var reminders))
                {
                    loaded[frontierId] = reminders = [];
                }

                foreach (var record in commander.Reminders ?? [])
                {
                    var id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id.Trim();
                    var problem = Refused(record.Sentence, record.Trigger, record.Argument, reminders.Count)
                        ?? (reminders.Any(held => held.Id == id)
                            ? new JournalReminderProblem(id, "another reminder has this id.")
                            : null);

                    if (problem is not null)
                    {
                        problems.Add(problem);
                        continue;
                    }

                    reminders.Add(new JournalReminder(
                        id,
                        record.Sentence!.Trim(),
                        record.Trigger!.Value,
                        string.IsNullOrWhiteSpace(record.Argument) ? null : record.Argument.Trim())
                    {
                        State = record.State,
                        Set = record.Set,
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            unreadable = true;
            loaded.Clear();
            problems.Add(new JournalReminderProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "The journal reminder file could not be read");
        }

        lock (_gate)
        {
            _byCommander = loaded.ToDictionary(
                pair => pair.Key, pair => (IReadOnlyList<JournalReminder>)pair.Value, StringComparer.Ordinal);
            _problems = problems;
            _unreadable = unreadable;
            _seen = text;
        }
    }

    private static JournalReminderProblem? Refused(string? said, JournalTrigger? triggered, string? argument, int held)
    {
        var sentence = said?.Trim() ?? string.Empty;
        var where = sentence.Length == 0 ? "a reminder" : sentence[..Math.Min(sentence.Length, 24)];

        return sentence.Length == 0 ? new JournalReminderProblem(where, "it has nothing to say.")
            : sentence.Length > MaxSentenceLength ? new JournalReminderProblem(
                where, $"the sentence is {sentence.Length} characters; the most is {MaxSentenceLength}.")
            : triggered is not { } trigger || !Enum.IsDefined(trigger)
                ? new JournalReminderProblem(where, "it has no trigger.")
            : JournalReminder.NeedsArgument(trigger) && string.IsNullOrWhiteSpace(argument)
                ? new JournalReminderProblem(where, $"{trigger} needs a name to match.")
            : held >= MaxReminders ? new JournalReminderProblem(where, $"this Commander already holds {MaxReminders}.")
            : null;
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public IReadOnlyList<ReminderRecord>? Reminders { get; set; }
    }

    private sealed class ReminderRecord
    {
        public string? Id { get; set; }

        public string? Sentence { get; set; }

        public JournalTrigger? Trigger { get; set; }

        public string? Argument { get; set; }

        public JournalReminderState State { get; set; } = JournalReminderState.Armed;

        public DateTimeOffset Set { get; set; }
    }
}
