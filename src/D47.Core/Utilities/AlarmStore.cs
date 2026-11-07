using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Utilities;

/// <summary>The Commander's alarms, in one file beside the executable (Phase 24, "Timers and alarms").</summary>
public sealed class AlarmStore(string path, IFileSystem files, ILogger<AlarmStore> logger)
{
    private static readonly JsonSerializerOptions Json = StoreJson.KeepingNulls;

    /// <summary>The longest name an alarm may carry.</summary>
    public const int MaxNameLength = 80;

    /// <summary>The most alarms one file may hold.</summary>
    public const int MaxAlarms = 64;

    private readonly Lock _gate = new();

    private IReadOnlyList<Reminder> _alarms = [];

    /// <summary>The file's contents as last read.</summary>
    private readonly WatchedTextFile _file = new(files, path, logger, "Could not read the alarm file");

    /// <summary>Raised when the set changed, whoever wrote it — the panel, a phrase, an editor.</summary>
    public event Action? Changed;

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

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll() => _file.Poll(
        () =>
        {
            lock (_gate)
            {
                _alarms = [];
            }

            Changed?.Invoke();
        },
        Reload);

    /// <summary>
    /// Writes a new set, and re-reads what landed so the store never believes something the file does
    /// not say.
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
            files.WriteText(path, text);
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
                _file.Seen(text);
            }

            logger.LogWarning(ex, "The alarm file could not be read");
            Changed?.Invoke();
            return true;
        }

        var alarms = new List<Reminder>();

        foreach (var line in file?.Alarms ?? [])
        {
            var name = (line.Name ?? string.Empty).Trim();

            if (name.Length == 0)
            {
                continue;
            }

            if (name.Length > MaxNameLength)
            {
                continue;
            }

            if (line.Due == default)
            {
                continue;
            }

            if (alarms.Count >= MaxAlarms)
            {
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
            _file.Seen(text);
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
