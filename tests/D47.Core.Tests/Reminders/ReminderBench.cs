using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>A reminder store in a temporary install, saving on the calling thread, and a callout over it.</summary>
internal sealed class ReminderBench : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero);

    private readonly TempInstall _install = new();

    public ReminderBench()
    {
        Store = Open();
        Callout = new JournalReminderCallout(Store) { Capacity = MaterialGrades.CapacityOf };
    }

    public string FilePath => Path.Combine(_install.Paths.Data, "journal-reminders.json");

    public JournalReminderStore Store { get; }

    public JournalReminderCallout Callout { get; }

    /// <summary>A second store over the same file, as a restart would read it.</summary>
    public JournalReminderStore Open()
    {
        var store = new JournalReminderStore(FilePath, NullLogger<JournalReminderStore>.Instance) { Dispatch = work => work() };
        store.Poll();
        return store;
    }

    public JournalReminder Arm(string frontierId, JournalTrigger trigger, string sentence, string? argument = null)
    {
        var reminder = new JournalReminder(Guid.NewGuid().ToString("N"), sentence, trigger, argument) { Set = Now.AddHours(-1) };
        Assert.True(Store.Add(frontierId, reminder));
        return reminder;
    }

    public List<Announcement> Say(CommanderGameState state, bool priming = false, params JournalEvent[] events)
    {
        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return [.. Callout.Examine(new CalloutContext(Now, priming, state, GameStatus.Unknown, NavRoute.None, events))];
    }

    public static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        return Parse(JsonSerializer.Serialize(payload));
    }

    public static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    public void Dispose() => _install.Dispose();
}
