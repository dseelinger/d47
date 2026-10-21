using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A targeted Commander met in an earlier game session: when, where and the most notable thing that happened,
/// once per Commander per session and never under threat.
/// </summary>
public sealed class MetBeforeCallout(CommanderLog log) : ICallout
{
    public string Id => "met-before";

    private readonly HashSet<string> _told = new(StringComparer.Ordinal);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var underThreat = default(bool?);

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "LoadGame")
            {
                _told.Clear();
                continue;
            }

            if (journalEvent.Kind != "ShipTargeted"
                || context.IsPriming
                || !log.HistoryFolded
                || context.State?.Identity.FrontierId is not { } fid
                || journalEvent.String("PilotName") is not { } pilot
                || CommanderLog.PilotNameOf(pilot) is not { } name)
            {
                continue;
            }

            underThreat ??= Threat.IsUnder(context);

            var told = fid + "|" + CommanderLog.KeyOf(name);

            if (underThreat.Value || _told.Contains(told))
            {
                continue;
            }

            var earlier = log.Encounters(fid, name).Where(log.IsBeforeThisSession).ToList();

            if (earlier.Count == 0)
            {
                continue;
            }

            _told.Add(told);

            yield return new Announcement($"met-before.{CommanderLog.KeyOf(name)}", Say(earlier, context.Now));
        }
    }

    private static string Say(IReadOnlyList<CommanderEncounter> earlier, DateTimeOffset now)
    {
        var (kind, phrase) = Notable.First(pair => earlier.Any(e => (e.Kinds & pair.Kind) != 0));
        var meeting = earlier.First(e => (e.Kinds & kind) != 0);

        var crime = kind == EncounterKind.Crime
            && string.Equals(meeting.CrimeType, "assault", StringComparison.OrdinalIgnoreCase)
                ? "they assaulted you"
                : phrase;

        var where = meeting.System.Length > 0 ? $" in {meeting.System}" : string.Empty;

        return $"Commander {meeting.Name} — you met {Ago(now - meeting.Last)}{where}; {crime}.";
    }

    private static readonly (EncounterKind Kind, string Phrase)[] Notable =
    [
        (EncounterKind.KilledBy, "they killed you"),
        (EncounterKind.Killed, "you killed them"),
        (EncounterKind.InterdictedBy | EncounterKind.EscapedInterdictionBy, "they interdicted you"),
        (EncounterKind.Interdicted, "you interdicted them"),
        (EncounterKind.Crime, "they committed a crime against you"),
        (EncounterKind.Winged, "you flew in a wing together"),
        (EncounterKind.Messaged, "you exchanged messages"),
        (EncounterKind.Targeted, "you targeted them"),
    ];

    private static string Ago(TimeSpan span)
    {
        if (span < TimeSpan.FromHours(1))
        {
            return "earlier today";
        }

        if (span < TimeSpan.FromDays(1))
        {
            return Count((int)span.TotalHours, "hour");
        }

        if (span < TimeSpan.FromDays(14))
        {
            return Count((int)span.TotalDays, "day");
        }

        if (span < TimeSpan.FromDays(60))
        {
            return Count((int)(span.TotalDays / 7), "week");
        }

        return span < TimeSpan.FromDays(730)
            ? Count((int)(span.TotalDays / 30), "month")
            : Count((int)(span.TotalDays / 365), "year");
    }

    private static string Count(int n, string unit)
    {
        string[] words = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"];
        var said = n < words.Length ? words[n] : n.ToString(CultureInfo.InvariantCulture);
        return $"{said} {unit}{(n == 1 ? string.Empty : "s")} ago";
    }
}
