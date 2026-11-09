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
/// broadcasts, not news. It also speaks the Informant's leaks, the Ideology arc's lines and the council's Hawk and Dove.
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

    private string? _commander;
    private DateTimeOffset? _chosenSince;
    private FactionReport? _report;
    private FactionReport? _previous;
    private IReadOnlyList<Front>? _before;
    private int _chronicled;

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
            _report = null;
            _previous = null;
            return [];
        }

        Track(held.Report);

        var campaign = Campaigns.For(id);

        if (context.IsPriming || !string.Equals(_commander, id, StringComparison.Ordinal) || held.Report is null)
        {
            _commander = id;
            _before = held.Report is null ? null : held.Fronts;
            _chronicled = -1;
            _chosenSince = campaign.Chosen?.Since;
            return [];
        }

        var chosenSince = campaign.Chosen?.Since;

        if (Held(context)
            || (ReferenceEquals(held.Fronts, _before) && campaign.Chronicle.Count == _chronicled && chosenSince == _chosenSince))
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

        var step = CampaignArcs.Step(campaign.Arc, _before, view);

        var chosen = chosenSince != _chosenSince ? campaign.Chosen : null;

        _before = held.Fronts;
        _chronicled = campaign.Chronicle.Count;
        _chosenSince = chosenSince;

        if (step.Arc != campaign.Arc && !Campaigns.SetArc(id, step.Arc))
        {
            return [];
        }

        var leaks = CampaignLeaks.Step(
            campaign.Informant,
            new LeakView(held.Faction, view.SeedKey, held.Report, _previous, state.Standings, context.Now));

        var spoken = new List<Announcement>(step.Lines.Select(Spoken));

        if (leaks.Informant != campaign.Informant && !Campaigns.SetInformant(id, leaks.Informant))
        {
            return spoken;
        }

        spoken.AddRange(leaks.Lines.Select(Whispered));

        var ideology = CampaignIdeology.Step(campaign.Ideology, view);

        if (ideology.Arc != campaign.Ideology && !Campaigns.SetIdeology(id, ideology.Arc))
        {
            return spoken;
        }

        spoken.AddRange(ideology.Lines.Select(line => Ideological(line, held.Faction)));

        var council = CampaignInternalPolitics.Step(
            campaign.Council,
            new CouncilView(held.Faction, view.SeedKey, held.Fronts, chosen, context.Now));

        if (council.Council != campaign.Council && !Campaigns.SetCouncil(id, council.Council))
        {
            return spoken;
        }

        spoken.AddRange(council.Lines.Select(line => Councillor(line, council.Council)));

        return spoken;
    }

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

    private void Track(FactionReport? report)
    {
        if (report is not null && !ReferenceEquals(report, _report))
        {
            _previous = _report;
            _report = report;
        }
    }

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

    /// <summary>Whether acts wait: in a fight, in supercruise or a jump, or in an Operation.</summary>
    private bool Held(CalloutContext context) =>
        context.Status.Has(StatusFlags.Supercruise)
        || context.Status.Has(StatusFlags.FsdJump)
        || context.Status.Has(StatusFlags.FsdCharging)
        || context.InOperation
        || fight.On(context.Now, context.Status);
}
