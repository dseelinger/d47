using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Utilities;

/// <summary>One alarm the file was asked to hold, and why it was refused.</summary>
public sealed record AlarmProblem(string Where, string Reason);

/// <summary>
/// The Commander's alarms, in one file beside the executable (list.md Phase 24, "Timers and
/// alarms").
/// <para>
/// <b>Alarms survive a restart and timers do not</b>, which is the whole reason this file exists
/// and the reason it holds only half of what the Utilities tab shows: an alarm for 07:00 is a
/// promise about a wall-clock moment that outlives the process, and a forty-minute timer through
/// a crash is a question nobody can answer.
/// </para>
/// <para>
/// <b>Change is detected by comparing content rather than a last-write time.</b> Phase 21's
/// correction: Windows updates the file-system clock about every 15.6 ms, so two writes inside
/// one tick carry the same stamp and the second is invisible. A hand edit is a one-off — miss it
/// and it stays missed until the Commander edits again, having watched d47 ignore them once.
/// </para>
/// <para>
/// <b>A bad line is reported, never silently dropped.</b> An alarm somebody relies on to leave
/// the house is not a thing to lose quietly, and the rest of the file still loads.
/// </para>
/// </summary>
public sealed class AlarmStore(string path, ILogger<AlarmStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The longest name an alarm may carry. A guard on a hand-editable file.</summary>
    public const int MaxNameLength = 80;

    /// <summary>The most alarms one file may hold. A guard on the same file.</summary>
    public const int MaxAlarms = 64;

    private readonly Lock _gate = new();

    private IReadOnlyList<Reminder> _alarms = [];
    private IReadOnlyList<AlarmProblem> _problems = [];

    /// <summary>The file's contents as last read. See the class summary for why not a stamp.</summary>
    private string? _seen;

    /// <summary>Raised when the set changed, whoever wrote it — the panel, a phrase, an editor.</summary>
    public event Action? Changed;

    public string Path => path;

    public IReadOnlyList<Reminder> Alarms
    {
        get
        {
            lock (_gate)
            {
                return _alarms;
            }
        }
    }

    /// <summary>Alarms that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<AlarmProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core, so
    /// an alarm edited by hand is live without a restart.
    /// </summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                // Not an error: no alarms is the normal state, and will be for most Commanders.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _alarms = [];
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
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read the alarm file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    /// <summary>
    /// Writes a new set, and re-reads what landed so the store never believes something the file
    /// does not say.
    /// </summary>
    public void Save(IReadOnlyList<Reminder> alarms)
    {
        var file = new AlarmFile
        {
            Alarms = [.. alarms
                .Where(alarm => alarm.Kind == ReminderKind.Alarm)
                .Take(MaxAlarms)
                .Select(alarm => new AlarmLine
                {
                    Id = alarm.Id,
                    Name = alarm.Name,
                    Due = alarm.Due,
                })],
        };

        var text = JsonSerializer.Serialize(file, Json);

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the alarm file");
            return;
        }

        Reload(text);
    }

    private bool Reload(string text)
    {
        AlarmFile? file;

        try
        {
            file = JsonSerializer.Deserialize<AlarmFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _problems = [new AlarmProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The alarm file could not be read");
            Changed?.Invoke();
            return true;
        }

        var alarms = new List<Reminder>();
        var problems = new List<AlarmProblem>();

        foreach (var line in file?.Alarms ?? [])
        {
            var name = (line.Name ?? string.Empty).Trim();

            if (name.Length == 0)
            {
                problems.Add(new AlarmProblem("an alarm", "it has no name."));
                continue;
            }

            if (name.Length > MaxNameLength)
            {
                problems.Add(new AlarmProblem(
                    name[..Math.Min(name.Length, 24)],
                    $"the name is {name.Length} characters; the most is {MaxNameLength}."));

                continue;
            }

            if (line.Due == default)
            {
                problems.Add(new AlarmProblem(name, "it has no time to go off at."));
                continue;
            }

            if (alarms.Count >= MaxAlarms)
            {
                problems.Add(new AlarmProblem(name, $"the file already holds {MaxAlarms} alarms."));
                continue;
            }

            alarms.Add(new Reminder(
                string.IsNullOrWhiteSpace(line.Id) ? Guid.NewGuid().ToString("N") : line.Id!,
                name,
                line.Due,
                ReminderKind.Alarm));
        }

        lock (_gate)
        {
            _alarms = alarms;
            _problems = problems;
            _seen = text;
        }

        Changed?.Invoke();
        return true;
    }

    private sealed record AlarmFile
    {
        public IReadOnlyList<AlarmLine> Alarms { get; init; } = [];
    }

    private sealed record AlarmLine
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public DateTimeOffset Due { get; init; }
    }
}
