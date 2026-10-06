using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Reminders;

/// <summary>Speaks each armed journal reminder once, when its trigger matches, and marks it fired.</summary>
/// <remarks>
/// The hold and material triggers fire when the condition becomes true, not while it holds: a reminder set
/// with the hold already full waits for it to empty and fill again.
/// </remarks>
public sealed class JournalReminderCallout(JournalReminderStore store) : ICallout
{
    public string Id => "reminders";

    public const string KeyPrefix = "reminder.";

    /// <summary>How many of a material can be held, or null when that is not known.</summary>
    public Func<string, int?> Capacity { get; set; } = _ => null;

    /// <summary>The Commander the baselines below were read for.</summary>
    private string? _commander;

    private bool? _holdEmpty;
    private bool? _holdFull;

    /// <summary>Whether each named material was full on the last tick, by the reminder's argument.</summary>
    private readonly Dictionary<string, bool> _materialFull = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The latest <c>LoadGame</c> in the primed backlog, owed to next-session reminders on the first live tick.</summary>
    private DateTimeOffset? _primedSession;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        var commander = state.Identity.FrontierId;

        if (!string.Equals(commander, _commander, StringComparison.Ordinal))
        {
            _commander = commander;
            _holdEmpty = null;
            _holdFull = null;
            _materialFull.Clear();
        }

        var armed = store.For(commander).Where(reminder => reminder.State == JournalReminderState.Armed).ToArray();

        var (wasEmpty, wasFull) = (_holdEmpty, _holdFull);
        (_holdEmpty, _holdFull) = HoldNow(state);
        var filled = MaterialsFilled(state, armed);

        if (context.IsPriming)
        {
            foreach (var journalEvent in context.Events.Where(journalEvent => journalEvent.Kind == "LoadGame"))
            {
                _primedSession = journalEvent.Timestamp;
            }

            yield break;
        }

        var due = new List<(JournalReminder Reminder, string LeadIn)>();

        void Fire(JournalTrigger trigger, Func<JournalReminder, bool> matches, Func<JournalReminder, string> leadIn)
        {
            foreach (var reminder in armed)
            {
                if (reminder.Trigger == trigger && matches(reminder) && due.All(owed => owed.Reminder.Id != reminder.Id))
                {
                    due.Add((reminder, leadIn(reminder)));
                }
            }
        }

        if (_primedSession is { } opened)
        {
            _primedSession = null;
            Fire(JournalTrigger.NextSession, reminder => reminder.Set < opened, _ => NextSessionLeadIn);
        }

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    // Before this session's own reminders fire, so those wait for the next one.
                    store.RemoveFired(commander);
                    Fire(JournalTrigger.NextSession, reminder => reminder.Set < journalEvent.Timestamp, _ => NextSessionLeadIn);
                    break;

                case "Docked":
                    var station = journalEvent.String("StationName");

                    Fire(JournalTrigger.NextDocking, _ => true, _ => "You asked me to remind you when you docked.");
                    Fire(
                        JournalTrigger.DockingAt,
                        reminder => Same(reminder.Argument, station),
                        reminder => $"You asked me to remind you when you docked at {reminder.Argument}.");

                    if (state.Carrier is { Owned: true, IsSquadron: false, DockedAtOwnCarrier: true })
                    {
                        Fire(
                            JournalTrigger.OwnCarrier,
                            _ => true,
                            _ => "You asked me to remind you when you were back aboard your carrier.");
                    }

                    break;

                case "FSDJump" or "CarrierJump" or "Location":
                    var system = journalEvent.String("StarSystem");

                    Fire(
                        JournalTrigger.ArrivalIn,
                        reminder => Same(reminder.Argument, system),
                        reminder => $"You asked me to remind you when you reached {reminder.Argument}.");
                    break;
            }
        }

        if (_holdEmpty == true && wasEmpty == false)
        {
            Fire(JournalTrigger.HoldEmpty, _ => true, _ => "You asked me to remind you when your hold was empty.");
        }

        if (_holdFull == true && wasFull == false)
        {
            Fire(JournalTrigger.HoldFull, _ => true, _ => "You asked me to remind you when your hold was full.");
        }

        Fire(
            JournalTrigger.MaterialFull,
            reminder => reminder.Argument is { } name && filled.Contains(name),
            reminder => $"You asked me to remind you when your {Holding(state, reminder.Argument!)?.Speak() ?? reminder.Argument} was full.");

        foreach (var (reminder, leadIn) in due)
        {
            if (store.MarkFired(commander, reminder.Id, context.Now))
            {
                yield return new Announcement(KeyPrefix + reminder.Id, leadIn) { Verbatim = reminder.Sentence };
            }
        }
    }

    private const string NextSessionLeadIn = "You asked me to remind you at the start of your next session.";

    /// <summary>Whether the hold is empty and whether it is full, each null until the ship's hold has been read.</summary>
    private static (bool? Empty, bool? Full) HoldNow(CommanderGameState state)
    {
        if (!state.Hold.IsKnown || !state.Hold.IsShip)
        {
            return (null, null);
        }

        return (
            state.Hold.Count == 0,
            state.Ship.CargoCapacity is { } capacity && capacity > 0 ? state.Hold.Count >= capacity : null);
    }

    /// <summary>
    /// The material names whose material became full this tick. Every name a material reminder has named this
    /// run is followed, so a baseline does not go stale once its reminder has fired.
    /// </summary>
    private HashSet<string> MaterialsFilled(CommanderGameState state, IReadOnlyList<JournalReminder> armed)
    {
        var filled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in armed
                     .Where(reminder => reminder.Trigger == JournalTrigger.MaterialFull && reminder.Argument is not null)
                     .Select(reminder => reminder.Argument!)
                     .Concat(_materialFull.Keys)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .ToArray())
        {
            var holding = Holding(state, name);

            if (Capacity(holding?.Name ?? name) is not { } capacity || capacity <= 0)
            {
                continue;
            }

            var full = (holding?.Count ?? 0) >= capacity;

            if (full && _materialFull.TryGetValue(name, out var was) && !was)
            {
                filled.Add(name);
            }

            _materialFull[name] = full;
        }

        return filled;
    }

    /// <summary>The holding a reminder's argument names, by journal name or by the name Elite displays.</summary>
    private static MaterialHolding? Holding(CommanderGameState state, string name) =>
        state.Materials.Find(name)
        ?? state.Materials.All.FirstOrDefault(holding =>
            string.Equals(holding.DisplayName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Case-insensitive on the whole name.</summary>
    private static bool Same(string? wanted, string? seen) =>
        wanted is { Length: > 0 } && string.Equals(wanted.Trim(), seen?.Trim(), StringComparison.OrdinalIgnoreCase);
}
