using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The Nemesis is introduced over comms in its leader's voice and kept in the campaign record; it broadcasts when a front
/// moves, never for the fronts it finds at startup, and waits out supercruise.
/// </summary>
public sealed class TheNemesisBroadcastsWhenAFrontMovesTests
{
    private const string Rival = "Lave Purple Gang";

    private readonly CampaignStore _store = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private CampaignFrontsSnapshot? _snapshot;

    public TheNemesisBroadcastsWhenAFrontMovesTests() => _store.SetFaction("F1", Us, clearIntents: false);

    private static Front War(int ours, int theirs) =>
        new(FrontKind.Conflict, "Lave", $"War against {Rival}, active.", SystemIntent.Hold, true, Rival, "", ours - theirs, Now, false)
        {
            Conflict = new FactionConflict("Lave", "War", true, OpponentKind.Named, Rival, [], true, ours, theirs, "", ""),
        };

    private static Front Thin(double lead) =>
        new(FrontKind.ThinMargin, "Leesti", $"The lead over {Rival} is under the 10 points kept for hold.", SystemIntent.Hold, true, Rival, "", lead, Now, false)
        {
            Lead = lead,
        };

    private void Hold(params Front[] fronts) =>
        _snapshot = new CampaignFrontsSnapshot("F1", Us, Report(Place("Lave", 0.4)), Now, fronts, Now, null, null);

    private CampaignArcCallout Arcs() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
        Voice = (_, _) => new PinnedVoice("edge", "en-GB-RyanNeural") { Link = CampaignArcs.Link },
    };

    private List<Announcement> Examine(CampaignArcCallout arcs, bool priming = false, StatusFlags flags = StatusFlags.Docked) =>
        [.. arcs.Examine(new CalloutContext(Now, priming, _state, new GameStatus { Flags = flags }, NavRoute.None, []))];

    [Fact]
    public void TheNemesisIsIntroducedOverCommsInItsLeadersVoiceAndRemembered()
    {
        Hold(War(0, 0));
        var arcs = Arcs();
        Examine(arcs, priming: true);

        var said = Assert.Single(Examine(arcs));

        var arc = _store.For("F1").Arc;
        Assert.NotNull(arc);
        Assert.Equal(Rival, arc.Rival);
        Assert.Equal(CampaignArcCallout.KeyPrefix + "introduction", said.Key);
        Assert.Equal(arc.Leader, said.Speaker);
        Assert.Equal(VoiceRole.Comms, said.Voice);
        Assert.True(said.InRoleVoice);
        Assert.Equal(CampaignArcs.Link, said.Pinned!.Link);
        Assert.NotNull(said.Arc);

        Hold(War(0, 0));
        Assert.Empty(Examine(arcs));
        Assert.Empty(Examine(Started()));
    }

    [Fact]
    public void FrontsFoundAtStartupAreNotBroadcast()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        Hold(War(1, 3));

        Assert.Empty(Examine(Started()));
    }

    [Fact]
    public void TheFirstReportAfterStartupIsTheBaselineNotNews()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        _snapshot = new CampaignFrontsSnapshot("F1", Us, null, null, [], null, null, null);
        var arcs = Started();

        Hold(Thin(-0.03));

        Assert.Empty(Examine(arcs));

        Hold(Thin(-0.03), War(1, 1));
        Assert.Empty(Examine(arcs));
    }

    [Fact]
    public void ADayWonIsBroadcastOnceTheFrontsShowIt()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        Hold(War(1, 1));
        var arcs = Started();

        Hold(War(1, 2));

        Assert.Equal(CampaignArcCallout.KeyPrefix + "broadcast", Assert.Single(Examine(arcs)).Key);
        Assert.Empty(Examine(arcs));
    }

    [Fact]
    public void ABroadcastWaitsOutSupercruise()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        Hold(War(1, 1));
        var arcs = Started();

        Hold(War(1, 2));

        Assert.Empty(Examine(arcs, flags: StatusFlags.Supercruise));
        Assert.Single(Examine(arcs, flags: StatusFlags.InMainShip));
    }

    private CampaignArcCallout Started()
    {
        var arcs = Arcs();
        Examine(arcs, priming: true);
        Assert.Empty(Examine(arcs));
        return arcs;
    }
}
