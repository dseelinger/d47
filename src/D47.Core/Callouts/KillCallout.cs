using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// A remark on a notable kill: the first of the game session, the best reward so far, one within thirty
/// seconds of the last, and every fifth.
/// </summary>
public sealed class KillCallout : ICallout
{
    /// <summary>Every kill key starts with this.</summary>
    public const string KeyPrefix = "kill.";

    public const string FirstKey = KeyPrefix + "first";
    public const string QuickKey = KeyPrefix + "quick";
    public const string BestKey = KeyPrefix + "best";
    public const string CountKey = KeyPrefix + "count";

    private static readonly TimeSpan QuickWindow = TimeSpan.FromSeconds(30);

    /// <summary>How long a pilot's last line is kept for the kill remark to refer back to.</summary>
    public static readonly TimeSpan CallbackWindow = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, (string Line, DateTimeOffset At)> _lastLines =
        new(StringComparer.OrdinalIgnoreCase);

    private int _kills;
    private long _bestReward;
    private DateTimeOffset? _lastKillAt;
    private int _quickRun;

    public string Id => "kills";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _kills = 0;
                    _bestReward = 0;
                    _lastKillAt = null;
                    _quickRun = 0;
                    _lastLines.Clear();
                    break;

                case "ReceiveText":
                    Remember(journalEvent);
                    break;

                case "Bounty" or "FactionKillBond":
                    // The backlog still counts, so a restart mid-session does not call the next kill the first.
                    if (Counted(journalEvent) is { } announcement && !context.IsPriming)
                    {
                        yield return announcement;
                    }

                    break;
            }
        }
    }

    /// <summary>The last Frontier-written line each NPC sent, by name.</summary>
    private void Remember(JournalEvent journalEvent)
    {
        if (!string.Equals(journalEvent.String("Channel"), "npc", StringComparison.OrdinalIgnoreCase)
            || journalEvent.String("Message") is not { Length: > 1 } message
            || message[0] is not '$'
            || journalEvent.String("From_Localised") is not { Length: > 0 } from
            || journalEvent.String("Message_Localised") is not { Length: > 0 } line)
        {
            return;
        }

        foreach (var stale in _lastLines.Where(entry => journalEvent.Timestamp - entry.Value.At > CallbackWindow)
                     .Select(entry => entry.Key)
                     .ToList())
        {
            _lastLines.Remove(stale);
        }

        _lastLines[from] = (line, journalEvent.Timestamp);
    }

    /// <summary>Counts one kill, and returns the remark when it is a notable one.</summary>
    private Announcement? Counted(JournalEvent journalEvent)
    {
        var bond = journalEvent.Kind == "FactionKillBond";
        var reward = (bond ? journalEvent.Long("Reward") : journalEvent.Long("TotalReward")) ?? 0;
        var at = journalEvent.Timestamp;

        _kills++;
        _quickRun = _lastKillAt is { } last && at - last <= QuickWindow ? _quickRun + 1 : 1;
        _lastKillAt = at;

        var best = _kills > 1 && reward > _bestReward;
        _bestReward = Math.Max(_bestReward, reward);

        (string Key, string Reason)? why =
            _kills == 1 ? (FirstKey, "First kill of the session.")
            : _quickRun == 2 ? (QuickKey, "Two kills inside thirty seconds.")
            : _quickRun > 2 ? (QuickKey, $"{Number(_quickRun)} kills in quick succession.")
            : best ? (BestKey, "Best reward this session.")
            : _kills % 5 == 0 ? (CountKey, $"That is {Number(_kills)} kills this session.")
            : null;

        if (why is not { } chosen)
        {
            return null;
        }

        var credits = reward.ToString("N0", CultureInfo.InvariantCulture);
        var pilot = bond ? null : PilotOf(journalEvent);
        var onFoot = !bond && journalEvent.String("Target") is { } target
            && target.Contains("suitai", StringComparison.OrdinalIgnoreCase);

        var victim = bond
            ? Resolved(journalEvent.Named("VictimFaction")) is { } faction ? $"{faction} ship" : "Ship"
            : onFoot
                ? pilot ?? Resolved(journalEvent.String("Target_Localised")) ?? "Target"
                : ShipOf(journalEvent) is { } ship
                    ? pilot is null ? ship : $"{pilot}'s {ship}"
                    : pilot ?? "Target";

        var outcome = onFoot ? "down" : "destroyed";

        return new Announcement(chosen.Key, $"{chosen.Reason} {victim} {outcome}, {credits} credits.")
        {
            Callback = pilot is not null
                && _lastLines.TryGetValue(pilot, out var said)
                && at - said.At <= CallbackWindow
                    ? said.Line
                    : null,
        };
    }

    /// <summary>An NPC's name, "A Commander" for a player, whose name another player chose, or null.</summary>
    private static string? PilotOf(JournalEvent journalEvent) =>
        journalEvent.String("PilotName") is { } raw && raw.StartsWith("$cmdr", StringComparison.OrdinalIgnoreCase)
            ? "A Commander"
            : Resolved(journalEvent.String("PilotName_Localised"));

    private static string? ShipOf(JournalEvent journalEvent) =>
        Resolved(journalEvent.String("Target_Localised"))
        ?? EliteSpecifications.Ship(journalEvent.String("Target"))?.Name;

    /// <summary>The text, or null when it is empty or a key Elite left unlocalised.</summary>
    private static string? Resolved(string? text) => text is { Length: > 0 } && text[0] != '$' ? text : null;

    private static string Number(int count) => count.ToString(CultureInfo.InvariantCulture);
}
