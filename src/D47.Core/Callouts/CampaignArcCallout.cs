using D47.Core.Audio;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;

namespace D47.Core.Callouts;

/// <summary>
/// The campaign's arc: the Nemesis introduced when it first leads a front against the campaign faction, its broadcasts when
/// it wins a day or takes the lead, and its concession or vow of revenge when it loses. Acts wait while the Commander is in
/// a fight, in supercruise or a jump, or in an Operation. The first fronts computed from a report are the baseline for
/// broadcasts, not news. It also speaks the Informant's leaks, the Ideology arc's lines, the council's Hawk and Dove, the Crisis arc, and the reveals of the Nemesis arc's secret.
/// One engine's lines are spoken a pass, at most once every <see cref="Gap"/>, wars first, then elections; an engine
/// whose lines are not spoken keeps its state and offers them again.
/// </summary>
public sealed class CampaignArcCallout(NearbyFight fight) : ICallout
{
    public string Id => "campaign-arcs";

    public const string KeyPrefix = "campaign.nemesis.";

    /// <summary>The campaigns, or null where there are none.</summary>
    public CampaignStore? Campaigns { get; set; }

    /// <summary>The shown Commander's campaign report and fronts, or null.</summary>
    public Func<CampaignFrontsSnapshot?> Fronts { get; set; } = () => null;

    /// <summary>The Nemesis's voice for a rival and the campaign faction, or null for the comms role's own.</summary>
    public Func<string, string, PinnedVoice?> Voice { get; set; } = (_, _) => null;

    /// <summary>The Informant's voice for the Informant's name, the campaign faction and the rival it leaks about, or null for the comms role's own.</summary>
    public Func<string, string, string, PinnedVoice?> InformantVoice { get; set; } = (_, _, _) => null;

    /// <summary>The Liaison's voice for the campaign faction, or null for the comms role's own.</summary>
    public Func<string, PinnedVoice?> LiaisonVoice { get; set; } = _ => null;

    /// <summary>A councillor's or defector's voice for a name, the campaign faction and the names voiced before it, or null for the comms role's own.</summary>
    public Func<string, string, IReadOnlyList<string>, PinnedVoice?> PoliticsVoice { get; set; } = (_, _, _) => null;

    /// <summary>Whether lines of a topic may be spoken.</summary>
    public Func<BgsTopic, bool> TopicOn { get; set; } = _ => true;

    /// <summary>After an arc line is spoken, how long before the next one may be.</summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMinutes(120);

    private string? _commander;
    private DateTimeOffset? _chosenSince;
    private FactionReport? _previous;
    private IReadOnlyList<Front>? _before;
    private int _chronicled;
    private string? _waitFor;
    private DateTimeOffset? _waitFrom;
    private bool _owed;

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
            _before = null;
            _previous = null;
            return [];
        }

        var campaign = Campaigns.For(id);

        if (context.IsPriming || !string.Equals(_commander, id, StringComparison.Ordinal) || held.Report is null)
        {
            if (!string.Equals(_waitFor, id, StringComparison.Ordinal))
            {
                _waitFor = id;
                _waitFrom = null;
            }

            _commander = id;
            _before = held.Report is null ? null : held.Fronts;
            _previous = held.Report;
            _chronicled = -1;
            _chosenSince = campaign.Chosen?.Since;
            _owed = false;
            return [];
        }

        var chosenSince = campaign.Chosen?.Since;

        if (Held(context)
            || (_waitFrom is { } waiting && context.Now - waiting < Gap)
            || (!_owed && ReferenceEquals(held.Fronts, _before) && campaign.Chronicle.Count == _chronicled && chosenSince == _chosenSince))
        {
            return [];
        }

        var view = new ArcView(
            held.Faction,
            CampaignArcs.SeedKey(state.Squadron.Key, held.Faction),
            held.Report,
            held.Fronts,
            campaign.Chronicle,
            context.Now);

        var chosen = chosenSince != _chosenSince ? campaign.Chosen : null;

        var nemesis = CampaignArcs.Step(campaign.Arc, _before, view);
        var leaks = CampaignLeaks.Step(
            campaign.Informant,
            new LeakView(held.Faction, view.SeedKey, held.Report, _previous, state.Standings, context.Now, held.Fronts));
        var ideology = CampaignIdeology.Step(campaign.Ideology, view);
        var council = CampaignInternalPolitics.Step(
            campaign.Council,
            new CouncilView(held.Faction, view.SeedKey, held.Fronts, chosen, context.Now));
        var crises = CampaignCrisis.Step(
            campaign.Crises,
            new CrisisView(held.Faction, view.SeedKey, held.Report, _previous, context.Now));
        var secret = CampaignSecrets.Step(
            campaign.Secret,
            nemesis.Lines.Count == 0 ? nemesis.Arc : campaign.Arc,
            held.Faction,
            campaign.Chronicle,
            context.Now);

        ArcEngineStep[] offered =
        [
            new(nemesis.Lines, () => nemesis.Arc == campaign.Arc || Campaigns.SetArc(id, nemesis.Arc), Spoken),
            new(leaks.Lines, () => leaks.Informant == campaign.Informant || Campaigns.SetInformant(id, leaks.Informant), Whispered),
            new(ideology.Lines, () => ideology.Arc == campaign.Ideology || Campaigns.SetIdeology(id, ideology.Arc), line => Ideological(line, held.Faction)),
            new(council.Lines, () => council.Council == campaign.Council || Campaigns.SetCouncil(id, council.Council), line => Councillor(line, council.Council)),
            new(crises.Lines, () => crises.Crises.SequenceEqual(campaign.Crises) || Campaigns.SetCrises(id, crises.Crises), line => Crisis(line, held.Faction)),
            new(
                secret.Lines,
                () => campaign.Secret is not { } kept || secret.Secret is not { } revealed || ReferenceEquals(kept, revealed)
                    || Campaigns.SetRevealed(id, kept, revealed),
                line => Said(line, LiaisonVoice(held.Faction))),
        ];

        var steps = offered.Select(step => step with { Lines = [.. step.Lines.Where(line => TopicOn(BgsTopics.Of(line, held.Fronts)))], Offered = step.Lines.Count }).ToArray();

        var winner = Winner(steps, held.Fronts);

        foreach (var quiet in steps.Where(step => step.Offered == 0))
        {
            quiet.Save();
        }

        var won = winner is not null && winner.Save();

        bool Settled(ArcEngineStep step) => step.Offered == 0 || (won && ReferenceEquals(step, winner));

        if (Settled(steps[0]))
        {
            _before = held.Fronts;
        }

        if (Settled(steps[3]))
        {
            _chosenSince = chosenSince;
        }

        if (Settled(steps[1]) && Settled(steps[4]))
        {
            _previous = held.Report;
        }

        _chronicled = campaign.Chronicle.Count;
        _owed = winner is not null || steps.Any(step => step.Offered > 0 && step.Lines.Count == 0);

        if (winner is null)
        {
            return [];
        }

        _waitFrom = context.Now;

        return won ? [.. winner.Lines.Select(winner.Announce)] : [];
    }

    /// <summary>0 for a line about a war or civil war, 1 for one about an election, 2 for any other.</summary>
    private static int Rank(CampaignArcLine line, IReadOnlyList<Front> fronts) => (int)BgsTopics.Of(line, fronts);

    /// <summary>The step whose lines are spoken: of the steps with lines, the first in engine order with the best-ranked line.</summary>
    private static ArcEngineStep? Winner(IReadOnlyList<ArcEngineStep> steps, IReadOnlyList<Front> fronts) =>
        steps
            .Where(step => step.Lines.Count > 0)
            .Select((step, order) => (Step: step, Rank: step.Lines.Min(line => Rank(line, fronts)), Order: order))
            .OrderBy(ranked => ranked.Rank)
            .ThenBy(ranked => ranked.Order)
            .Select(ranked => ranked.Step)
            .FirstOrDefault();

    private Announcement Crisis(CampaignArcLine line, string faction) =>
        line.Act == ArcAct.Exploit ? Said(line, Voice(line.Rival, line.Faction)) : Said(line, LiaisonVoice(faction));

    private Announcement Ideological(CampaignArcLine line, string faction) =>
        line.Act switch
        {
            ArcAct.Election => Said(line, LiaisonVoice(faction)),
            ArcAct.Defection => Said(
                line,
                PoliticsVoice(line.Rival, faction, []) is { } defector ? defector with { Link = CampaignIdeology.DefectorLink } : null),
            _ => Said(line, Voice(line.Rival, line.Faction)),
        };

    private Announcement Councillor(CampaignArcLine line, CampaignCouncil? council) =>
        Said(
            line,
            PoliticsVoice(
                line.Leader,
                line.Faction,
                council is not null && string.Equals(line.Leader, council.Dove, StringComparison.Ordinal) ? [council.Hawk] : []));

    private static Announcement Said(CampaignArcLine line, PinnedVoice? voice) =>
        new(KeyPrefix + line.Act.ToString().ToLowerInvariant(), line.Text)
        {
            Voice = VoiceRole.Comms,
            Speaker = line.Leader,
            InRoleVoice = true,
            Pinned = voice,
            Arc = line,
        };

    private Announcement Whispered(CampaignArcLine line) =>
        new(KeyPrefix + line.Act.ToString().ToLowerInvariant(), line.Text)
        {
            Voice = VoiceRole.Comms,
            Speaker = line.Leader,
            InRoleVoice = true,
            Pinned = InformantVoice(line.Leader, line.Faction, line.Rival),
            Arc = line,
        };

    private Announcement Spoken(CampaignArcLine line) =>
        new(KeyPrefix + line.Act.ToString().ToLowerInvariant(), line.Text)
        {
            Voice = VoiceRole.Comms,
            Speaker = line.Leader,
            InRoleVoice = true,
            Pinned = Voice(line.Rival, line.Faction),
            Arc = line,
        };

    /// <summary>One engine's lines, how to save its new state, and how each line is said.</summary>
    private sealed record ArcEngineStep(IReadOnlyList<CampaignArcLine> Lines, Func<bool> Save, Func<CampaignArcLine, Announcement> Announce)
    {
        /// <summary>How many lines the engine produced before topics left some out.</summary>
        public int Offered { get; init; }
    }

    /// <summary>Whether acts wait: in a fight, in supercruise or a jump, or in an Operation.</summary>
    private bool Held(CalloutContext context) =>
        context.Status.Has(StatusFlags.Supercruise)
        || context.Status.Has(StatusFlags.FsdJump)
        || context.Status.Has(StatusFlags.FsdCharging)
        || context.InOperation
        || fight.On(context.Now, context.Status);
}
