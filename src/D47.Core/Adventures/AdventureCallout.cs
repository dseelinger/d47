using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Adventures;

/// <summary>
/// A beat, said when it is reached (Phase 47, "The ship's AI tells it, and the authored beat is the
/// floor").
/// </summary>
public sealed class AdventureCallout(AdventureBook book) : ICallout
{
    public string Id => "adventure";

    public const string KeyPrefix = "adventure.";

    /// <summary>
    /// The short acknowledgement's own prefix, and it is deliberately not <see cref="KeyPrefix"/>
    /// (asked for 2026-08-22).
    /// </summary>
    public const string AckPrefix = "adventure-ack.";

    public const string BackstoryPrefix = "adventure-backstory.";

    public const string CrimePrefix = "adventure-crime.";

    /// <summary>The hand-off after a beat whose line is not the ship's, said by the ship.</summary>
    public const string HandOffPrefix = "adventure-handoff.";

    /// <summary>The hand-off to the next step after a step that is not its beat's last, keyed <c>adventure-step.&lt;story&gt;.&lt;beat&gt;.&lt;step&gt;</c>.</summary>
    public const string StepPrefix = "adventure-step.";

    /// <summary>The line said when a chapter fails, keyed <c>adventure-failed.&lt;story&gt;</c>.</summary>
    public const string FailedPrefix = "adventure-failed.";

    /// <summary>A beat's second or third line, keyed <c>adventure-line.&lt;story&gt;.&lt;beat&gt;.&lt;line&gt;</c>.</summary>
    public const string LinePrefix = "adventure-line.";

    public const string BackstoryLine =
        "The story has turned. Your Backstory still describes where it began, and it is yours to change.";

    /// <summary>Whether the Commander has a Backstory set.</summary>
    public Func<bool> HasBackstory { get; init; } = () => false;

    /// <summary>Whether the Backstory nudge is switched on.</summary>
    public Func<bool> NudgeBackstory { get; init; } = () => true;

    /// <summary>Whether the core aboard is a stock core, whose Narrator tells a failure instead of the ship.</summary>
    public Func<bool> StockCoreAboard { get; set; } = () => false;

    /// <summary>Whether a Commander's reached beats wait, unsaid, behind lines that must be said first.</summary>
    public Func<string?, bool> Held { get; init; } = _ => false;

    /// <summary>How long a reached beat waits before it is said.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(20);

    public ILogger Log { get; init; } = NullLogger.Instance;

    private readonly List<AdventureMoment> _waiting = [];

    /// <summary>Beats that came due in danger; once clear they say only their hand-off.</summary>
    private readonly HashSet<AdventureMoment> _held = new(ReferenceEqualityComparer.Instance);

    /// <summary>Missions already warned about this session.</summary>
    private readonly HashSet<long> _warned = [];

    /// <summary>Which stock acknowledgement is next.</summary>
    private int _acks;

    /// <summary>
    /// Which story and which beat an announcement of this family is about, or null if it is not one
    /// (asked for 2026-08-22).
    /// </summary>
    /// <returns>The adventure's key and the beat index, with <c>-1</c> for the opening.</returns>
    public static (string Key, int Beat)? Reached(string? announcementKey)
    {
        if (announcementKey is null || !announcementKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = announcementKey[KeyPrefix.Length..];

        // From the right: an adventure's key is the Commander's or a model's and may hold a dot, and the part
        // after the last one is always the beat.
        var dot = rest.LastIndexOf('.');

        if (dot <= 0)
        {
            return null;
        }

        var story = rest[..dot];
        var tail = rest[(dot + 1)..];

        if (string.Equals(tail, "opening", StringComparison.Ordinal))
        {
            return (story, -1);
        }

        return int.TryParse(tail, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var beat)
            ? (story, beat)
            : null;
    }

    /// <summary>
    /// Which story, beat and line an announcement is, for a beat's first line (<see cref="KeyPrefix"/>) and its
    /// later ones (<see cref="LinePrefix"/>); null for anything else.
    /// </summary>
    public static (string Key, int Beat, int Line)? Spoken(string? announcementKey)
    {
        if (Reached(announcementKey) is var (key, beat))
        {
            return (key, beat, 0);
        }

        if (announcementKey is null || !announcementKey.StartsWith(LinePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = announcementKey[LinePrefix.Length..];
        var lineDot = rest.LastIndexOf('.');
        var beatDot = lineDot > 0 ? rest.LastIndexOf('.', lineDot - 1) : -1;

        return beatDot > 0
               && int.TryParse(rest[(beatDot + 1)..lineDot], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
               && int.TryParse(rest[(lineDot + 1)..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var line)
            ? (rest[..beatDot], index, line)
            : null;
    }

    /// <summary>
    /// Says once per mission that a settlement is a crime scene for it: the current beat is an illegal
    /// mission beat and a live mission of that family has this settlement as its target.
    /// </summary>
    private IEnumerable<Announcement> CrimeWarnings(CalloutContext context, string? commander)
    {
        if (context.State is not { } state || state.Missions.Missions.Count == 0)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ApproachSettlement"
                || journalEvent.String("Name") is not { Length: > 0 } settlement
                || journalEvent.Named("StationGovernment") is not { Length: > 0 } government
                || government[0] == '$'
                || string.Equals(government, "Anarchy", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var families = book.Active(commander)
                .Where(standing => !book.IsSilenced(commander, standing.Adventure, context.Now))
                .Select(standing => standing.CurrentTrigger)
                .Where(trigger => trigger is { Kind: TriggerKind.Mission } && MissionFamilies.IsIllegal(trigger.MissionFamily))
                .Select(trigger => trigger!.MissionFamily)
                .ToList();

            var targets = state.Missions.Missions
                .Where(mission => string.Equals(mission.DestinationStation, settlement, StringComparison.OrdinalIgnoreCase)
                    && families.Any(family => MissionFamilies.Counts(family, mission.Name))
                    && !_warned.Contains(mission.Id))
                .ToList();

            if (targets.Count == 0)
            {
                continue;
            }

            _warned.UnionWith(targets.Select(mission => mission.Id));

            yield return new Announcement(
                $"{CrimePrefix}{targets[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"{settlement} is run by a {government} faction. This job is a crime here. An Anarchy-run settlement would leave you clean.")
            {
                Urgency = CalloutUrgency.Routine,
            };
        }
    }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var commander = context.State?.Identity.FrontierId;

        foreach (var journalEvent in context.Events)
        {
            book.Observe(journalEvent, commander);
        }

        var reached = book.Drain();

        // While another Commander flies, a chapter's opening is still said when the chapter is written; nothing in
        // the game caused it. Every other beat is dropped.
        var openingsOnly = context.IsPriming && context.AnotherFlying;

        // Priming folds the backlog into the standings and announces none of it — a beat that fired two hours
        // ago is in the past, and the context block already says so. Each dropped beat stops its wait.
        if (context.IsPriming && !openingsOnly)
        {
            foreach (var moment in reached)
            {
                book.Quiet(moment.FrontierId, moment.Adventure.Key);
            }

            yield break;
        }

        foreach (var moment in reached)
        {
            if (book.IsSilenced(moment.FrontierId, moment.Adventure, context.Now) || (openingsOnly && !moment.IsOpening))
            {
                book.Quiet(moment.FrontierId, moment.Adventure.Key);
                continue;
            }

            // Stamped with the tick rather than the journal's time, so the settle is measured from when d47
            // learned of it rather than from a timestamp that may be a file flush behind.
            _waiting.RemoveAll(other => other.IsStep && other.FrontierId == moment.FrontierId && other.Adventure.Key == moment.Adventure.Key && other.Position < moment.Position);
            _waiting.Add(moment with { At = context.Now });

            // And the acknowledgement, now, with no settle and no model behind it — see <see
            // cref="AdventureAcks"/> for why it is split off from the beat at all.
            if (!moment.IsOpening && !moment.IsFailure)
            {
                yield return new Announcement($"{AckPrefix}{moment.Adventure.Key}.{moment.Beat}{(moment.Step is { } step ? "." + step.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty)}", AdventureAcks.Pick(_acks++))
                {
                    Urgency = CalloutUrgency.Routine,
                };
            }
        }

        foreach (var warning in openingsOnly ? Enumerable.Empty<Announcement>() : CrimeWarnings(context, commander))
        {
            yield return warning;
        }

        if (_waiting.Count == 0)
        {
            yield break;
        }

        var inDanger = context.Status.IsKnown
                       && (context.Status.Has(StatusFlags.InDanger) || context.Status.Has(StatusFlags.BeingInterdicted));

        var due = _waiting
            .Where(moment => (moment.IsOpening || moment.IsFailure || context.Now - moment.At >= Settle) && !Held(moment.FrontierId))
            .ToList();

        foreach (var moment in due)
        {
            if (inDanger && !moment.IsOpening)
            {
                if (_held.Add(moment))
                {
                    Log.LogInformation("Holding beat {Beat} of {Story} until the ship is out of danger", moment.Beat, moment.Adventure.Key);
                }

                continue;
            }

            _waiting.Remove(moment);
            var wasHeld = _held.Remove(moment);

            if (book.IsSilenced(moment.FrontierId, moment.Adventure, context.Now) || (openingsOnly && !moment.IsOpening))
            {
                book.Quiet(moment.FrontierId, moment.Adventure.Key);
                continue;
            }

            if (wasHeld)
            {
                var superseded = _waiting.Any(other => other.FrontierId == moment.FrontierId
                    && other.Adventure.Key == moment.Adventure.Key
                    && other.Position > moment.Position);

                if (superseded || moment.HandOff is not { } held)
                {
                    book.Quiet(moment.FrontierId, moment.Adventure.Key);
                    continue;
                }

                yield return new Announcement(moment.IsStep ? moment.Key : $"{HandOffPrefix}{moment.Key[KeyPrefix.Length..]}", held)
                {
                    Urgency = CalloutUrgency.Routine,
                };

                continue;
            }

            if (moment.IsStep)
            {
                if (moment.HandOff is { } next)
                {
                    yield return new Announcement(moment.Key, next)
                    {
                        Urgency = CalloutUrgency.Routine,
                    };
                }

                continue;
            }

            if (moment.IsFailure)
            {
                var narrated = StockCoreAboard();

                yield return new Announcement(
                    moment.Key,
                    narrated ? $"The chapter failed. {moment.Reason}" : $"This chapter has failed. {moment.Reason}")
                {
                    Urgency = CalloutUrgency.Routine,
                    Voice = narrated ? Audio.VoiceRole.Narrator : Audio.VoiceRole.ShipAi,
                };

                continue;
            }

            // A ship line that ends the beat carries where to go next, as one text: the hand-off is part of what the
            // model is told to keep when it says this in the core's voice, and what plays when there is no model.
            var lines = moment.Lines;

            for (var index = 0; index < lines.Count; index++)
            {
                var last = index == lines.Count - 1;

                yield return new Announcement(moment.LineKey(index), last && IsShips(lines[index]) ? moment.Spoken(index) : lines[index].Text)
                {
                    Urgency = CalloutUrgency.Routine,

                    // The beat index rides along so the brief can say which this is without parsing the key; the
                    // opening is -1.
                    Variant = moment.Beat,
                    WhileAnotherFlies = moment.IsOpening,
                };
            }

            if (lines.Count > 0 && !IsShips(lines[^1]) && moment.HandOff is { } handOff)
            {
                yield return new Announcement($"{HandOffPrefix}{moment.Key[KeyPrefix.Length..]}", handOff)
                {
                    Urgency = CalloutUrgency.Routine,
                    WhileAnotherFlies = moment.IsOpening,
                };
            }

            if (!moment.IsOpening
                && moment.Beat < moment.Adventure.Beats.Count
                && AdventureStanding.IsTurning(moment.Adventure.Beats[moment.Beat].Function)
                && NudgeBackstory()
                && HasBackstory())
            {
                yield return new Announcement($"{BackstoryPrefix}{moment.Adventure.Key}.{moment.Beat}", BackstoryLine)
                {
                    Urgency = CalloutUrgency.Routine,
                };
            }
        }
    }

    private static bool IsShips(AdventureLine line) => line.Speaker is not { } speaker || speaker == Stories.StorySpeaker.Ship;
}
