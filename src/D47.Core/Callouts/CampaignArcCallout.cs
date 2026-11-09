using D47.Core.Audio;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Callouts;

/// <summary>
/// The campaign's arc: the Nemesis introduced when it first leads a front against the campaign faction, its broadcasts when
/// it wins a day or takes the lead, and its concession or vow of revenge when it loses. Acts wait while the Commander is in
/// a fight, in supercruise or a jump, or in an Operation. The first fronts computed from a report are the baseline for
/// broadcasts, not news.
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

    private string? _commander;
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
            return [];
        }

        if (context.IsPriming || !string.Equals(_commander, id, StringComparison.Ordinal) || held.Report is null)
        {
            _commander = id;
            _before = held.Report is null ? null : held.Fronts;
            _chronicled = -1;
            return [];
        }

        var campaign = Campaigns.For(id);

        if (Held(context) || (ReferenceEquals(held.Fronts, _before) && campaign.Chronicle.Count == _chronicled))
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

        _before = held.Fronts;
        _chronicled = campaign.Chronicle.Count;

        if (step.Arc != campaign.Arc && !Campaigns.SetArc(id, step.Arc))
        {
            return [];
        }

        return [.. step.Lines.Select(Spoken)];
    }

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
