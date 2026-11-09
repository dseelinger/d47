using D47.Core.Audio;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Callouts;

/// <summary>
/// The campaign faction's Liaison: briefs a front when it is chosen, gives a dispatch after each BGS tick, acknowledges work
/// and milestones on the front, and gives a resolved front's outcome with the next one proposed. Beats wait while the
/// Commander is in a fight, in supercruise or a jump, or in an Operation. What is there at startup is seen and not said.
/// </summary>
public sealed class CampaignLiaisonCallout(NearbyFight fight) : ICallout
{
    public string Id => "campaign-liaison";

    public const string KeyPrefix = "campaign.liaison.";

    /// <summary>The least time between two acknowledgements of work handed in.</summary>
    public static readonly TimeSpan HandInGap = TimeSpan.FromMinutes(30);

    /// <summary>How long an outcome waits for the next front to be proposed before it is said without one.</summary>
    public static readonly TimeSpan NextFrontWait = TimeSpan.FromMinutes(1);

    /// <summary>The campaigns, or null where there are none.</summary>
    public CampaignStore? Campaigns { get; set; }

    /// <summary>The shown Commander's campaign report and fronts, or null.</summary>
    public Func<CampaignFrontsSnapshot?> Fronts { get; set; } = () => null;

    /// <summary>The front proposed while none is chosen, or null.</summary>
    public Func<FrontProposal?> Proposal { get; set; } = () => null;

    /// <summary>The time of the last BGS tick, or null.</summary>
    public Func<DateTimeOffset?> LastTick { get; set; } = () => null;

    /// <summary>The Liaison's voice for a faction, or null for the comms role's own.</summary>
    public Func<string, PinnedVoice?> Voice { get; set; } = _ => null;

    private sealed record Pending(LiaisonBeat Beat, DateTimeOffset At)
    {
        public CampaignLiaisonLine? Line { get; init; }

        public ChronicleEntry? Entry { get; init; }

        public Front? Front { get; init; }

        public DateTimeOffset Tick { get; init; }
    }

    private readonly List<Pending> _pending = [];

    private string? _commander;
    private (FrontKind Kind, string System, DateTimeOffset Since)? _briefed;
    private string? _lastEntry;
    private DateTimeOffset _workSeen;
    private DateTimeOffset? _handInAt;
    private DateTimeOffset? _askedFor;
    private Front? _front;
    private int? _oursWon;
    private double? _margin;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var held = Fronts();
        var state = context.State;

        if (Campaigns is null
            || held is null
            || state?.Identity.FrontierId is not { Length: > 0 } id
            || !string.Equals(held.FrontierId, id, StringComparison.Ordinal))
        {
            _commander = null;
            _pending.Clear();
            return [];
        }

        var campaign = Campaigns.For(id);

        if (context.IsPriming || !string.Equals(_commander, id, StringComparison.Ordinal))
        {
            Seed(id, campaign, held, state);
            return [];
        }

        Watch(campaign, held, state, context.Now);

        return Held(context) ? [] : Say(id, campaign, held, state, context.Now);
    }

    /// <summary>Takes what is there now as already said.</summary>
    private void Seed(string id, Campaign campaign, CampaignFrontsSnapshot held, CommanderGameState state)
    {
        if (!string.Equals(_commander, id, StringComparison.Ordinal))
        {
            _askedFor = null;
            _handInAt = null;
        }

        _commander = id;
        _pending.Clear();
        _briefed = Identity(campaign.Chosen);
        _lastEntry = KeyOf(campaign.Chronicle.LastOrDefault());
        _workSeen = Newest(state.BgsWork);
        Baseline(campaign.Chosen is { } chosen ? Find(held.Fronts, chosen) : null);
    }

    private void Watch(Campaign campaign, CampaignFrontsSnapshot held, CommanderGameState state, DateTimeOffset now)
    {
        var chosen = campaign.Chosen;
        var front = chosen is null ? null : Find(held.Fronts, chosen);
        var last = campaign.Chronicle.LastOrDefault();

        if (KeyOf(last) is var entry && !string.Equals(entry, _lastEntry, StringComparison.Ordinal))
        {
            _lastEntry = entry;

            if (last is not null)
            {
                _pending.RemoveAll(pending => pending.Beat != LiaisonBeat.Resolution);
                _pending.Add(new Pending(LiaisonBeat.Resolution, now) { Entry = last, Front = _front });
            }
        }

        if (chosen is null || front is null)
        {
            _workSeen = Newest(state.BgsWork);
            return;
        }

        if (Identity(chosen) != _briefed)
        {
            _briefed = Identity(chosen);
            _pending.RemoveAll(pending => pending.Beat != LiaisonBeat.Resolution);
            _pending.Add(new Pending(LiaisonBeat.Briefing, now));
            Baseline(front);
        }

        var faction = held.Faction;
        var government = held.Report?.Government;
        var handedIn = state.BgsWork.Entries
            .Where(work => work.At > _workSeen
                && string.Equals(work.Faction, faction, StringComparison.OrdinalIgnoreCase)
                && string.Equals(state.BgsWork.NameOf(work.SystemAddress), front.System, StringComparison.OrdinalIgnoreCase)
                && CampaignLiaison.Counts(front, work.Kind, work.Amount))
            .ToList();

        _workSeen = Newest(state.BgsWork);

        if (handedIn.Count > 0 && (_handInAt is not { } lastHandIn || now - lastHandIn >= HandInGap))
        {
            _handInAt = now;
            Queue(LiaisonBeat.HandIn, now, CampaignLiaison.HandIn(front, handedIn, faction, government));
        }

        var firstDay = false;

        if (front.Conflict is { Visited: true } conflict)
        {
            firstDay = _oursWon == 0 && conflict.OursWon > 0;
            _oursWon = conflict.OursWon;

            if (firstDay)
            {
                Queue(LiaisonBeat.FirstDayWon, now, CampaignLiaison.FirstDayWon(front, faction, government));
            }
        }

        if (front.Kind != FrontKind.Stale && front.Margin is { } margin)
        {
            if (!firstDay && _margin < 0 && margin >= 0)
            {
                Queue(LiaisonBeat.MarginRestored, now, CampaignLiaison.MarginRestored(front, faction, government));
            }

            _margin = margin;
        }

        _front = front;

        if (LastTick() is { } tick && tick > chosen.Since && chosen.Reported != tick)
        {
            if (state.Standings.Latest(front.System) is { } reading && reading.SeenAt >= tick)
            {
                _pending.RemoveAll(pending => pending.Beat is LiaisonBeat.MorningReport or LiaisonBeat.Dispatch);
                _pending.Add(new Pending(LiaisonBeat.Dispatch, now) { Tick = tick });
            }
            else if (_askedFor != tick)
            {
                _askedFor = tick;
                _pending.RemoveAll(pending => pending.Beat is LiaisonBeat.MorningReport);
                _pending.Add(new Pending(LiaisonBeat.MorningReport, now) { Tick = tick });
            }
        }
    }

    /// <summary>The pending beats that can be said now, in the order they arose; an outcome waits for the next front.</summary>
    private List<Announcement> Say(string id, Campaign campaign, CampaignFrontsSnapshot held, CommanderGameState state, DateTimeOffset now)
    {
        var said = new List<Announcement>();
        var faction = held.Faction;
        var government = held.Report?.Government;
        var chosen = campaign.Chosen;
        var front = chosen is null ? null : Find(held.Fronts, chosen);

        foreach (var pending in _pending.ToList())
        {
            CampaignLiaisonLine? line;

            switch (pending.Beat)
            {
                case LiaisonBeat.Resolution:
                    var proposal = Proposal();

                    if (proposal is null && chosen is null && held.Fronts.Count > 0 && now - pending.At < NextFrontWait)
                    {
                        continue;
                    }

                    line = CampaignLiaison.Resolution(pending.Entry!, pending.Front, chosen is null ? proposal?.Front : front, faction, government);
                    break;

                case LiaisonBeat.Briefing:
                    line = front is null
                        ? null
                        : CampaignLiaison.Briefing(front, CampaignNextAction.For(front, faction, state.Combat, state.BgsWork, now), faction, government);
                    break;

                case LiaisonBeat.Dispatch:
                    line = front is null
                        ? null
                        : CampaignLiaison.Dispatch(front, faction, state.BgsWork, state.Standings, pending.Tick, now, government);

                    if (line is not null)
                    {
                        Campaigns!.NoteReported(id, chosen!.Kind, chosen.System, pending.Tick);
                    }

                    break;

                case LiaisonBeat.MorningReport:
                    line = front is null ? null : CampaignLiaison.MorningReport(front, faction, government);
                    break;

                default:
                    line = pending.Line;
                    break;
            }

            _pending.Remove(pending);

            if (line is not null)
            {
                said.Add(Spoken(line));
            }
        }

        return said;
    }

    private Announcement Spoken(CampaignLiaisonLine line) =>
        new(KeyPrefix + line.Beat.ToString().ToLowerInvariant(), line.Text)
        {
            Voice = VoiceRole.Comms,
            Speaker = CampaignLiaison.Speaker,
            InRoleVoice = true,
            Pinned = Voice(line.Faction),
            Liaison = line,
        };

    private void Queue(LiaisonBeat beat, DateTimeOffset now, CampaignLiaisonLine line)
    {
        _pending.RemoveAll(pending => pending.Beat == beat);
        _pending.Add(new Pending(beat, now) { Line = line });
    }

    private void Baseline(Front? front)
    {
        _front = front;
        _oursWon = front?.Conflict is { Visited: true } conflict ? conflict.OursWon : null;
        _margin = front?.Kind == FrontKind.Stale ? null : front?.Margin;
    }

    /// <summary>Whether beats wait: in a fight, in supercruise or a jump, or in an Operation.</summary>
    private bool Held(CalloutContext context) =>
        context.Status.Has(StatusFlags.Supercruise)
        || context.Status.Has(StatusFlags.FsdJump)
        || context.Status.Has(StatusFlags.FsdCharging)
        || context.InOperation
        || fight.On(context.Now, context.Status);

    private static (FrontKind, string, DateTimeOffset)? Identity(ChosenFront? chosen) =>
        chosen is null ? null : (chosen.Kind, chosen.System.ToUpperInvariant(), chosen.Since);

    private static string? KeyOf(ChronicleEntry? entry) =>
        entry is null ? null : $"{entry.Kind}|{entry.System}|{entry.Outcome}|{entry.Ticks.Count}|{entry.Ticks.FirstOrDefault():O}";

    private static DateTimeOffset Newest(BgsWork work) =>
        work.Entries.Count == 0 ? DateTimeOffset.MinValue : work.Entries.Max(entry => entry.At);

    private static Front? Find(IReadOnlyList<Front> fronts, ChosenFront chosen) =>
        fronts.FirstOrDefault(front => front.Kind == chosen.Kind && string.Equals(front.System, chosen.System, StringComparison.OrdinalIgnoreCase));
}
