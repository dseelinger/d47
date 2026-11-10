using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Callouts;

/// <summary>War callouts, Election callouts and Other BGS callouts switch every Liaison and arc line by its topic.</summary>
public sealed class TheBgsTopicRowsSwitchTheLiaisonAndTheArcsTests
{
    private const string Rival = "Lave Purple Gang";

    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private CampaignFrontsSnapshot? _snapshot;

    private CalloutSettings _topics = new();

    private DateTimeOffset _clock = Now;

    public TheBgsTopicRowsSwitchTheLiaisonAndTheArcsTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
    }

    private static Front Conflict(string system, string type, int ours, int theirs) =>
        new(FrontKind.Conflict, system, $"{type} against {Rival}.", SystemIntent.Hold, true, Rival, "", ours - theirs, Now, false)
        {
            Conflict = new FactionConflict(system, type, true, OpponentKind.Named, Rival, [], true, ours, theirs, "", ""),
        };

    private void Hold(IReadOnlyList<Front> fronts, params FactionSystem[] systems) =>
        _snapshot = new CampaignFrontsSnapshot("F1", Us, Report(systems), _clock, fronts, _clock, null, null);

    private CampaignArcCallout Arcs() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
        TopicOn = topic => ShippedCallouts.TopicOn(_topics, topic),
    };

    private List<Announcement> Examine(ICallout callout, bool priming = false) =>
        [.. callout.Examine(new CalloutContext(_clock, priming, _state, new GameStatus { Flags = StatusFlags.Docked }, NavRoute.None, []))];

    [Fact]
    public void AnExpansionLeakWithOtherBgsCalloutsOffSpeaksNothingSavesNothingAndStartsNoWait()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        Front[] thin = [new(FrontKind.ThinMargin, "Diso", "Thin margin.", SystemIntent.Hold, true, "Second Rival", "", 0.05, Now, false)];
        Hold(thin, Place("Diso", 0.4, others: [new FactionNeighbour("Second Rival", 0.2) { PendingStates = ["Expansion"] }]));
        var arcs = Arcs();
        Examine(arcs, priming: true);

        _topics = new CalloutSettings { BgsOther = false };

        Assert.Empty(Examine(arcs));
        Assert.Null(_store.For("F1").Informant);

        _topics = new CalloutSettings();

        Assert.Equal(CampaignArcCallout.KeyPrefix + "leak", Assert.Single(Examine(arcs)).Key);
    }

    [Fact]
    public void WithOnlyWarCalloutsOnANemesisWarLineIsSpokenAndALiaisonHandInOnAnotherFrontIsNot()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        var lave = Place("Lave", 0.4, active: ["War"], others: [new FactionNeighbour(Rival, 0.4) { ActiveStates = ["War"] }]);
        _topics = new CalloutSettings { BgsWars = true, BgsElections = false, BgsOther = false };

        Hold([Conflict("Lave", "War", 1, 1)], lave);
        var arcs = Arcs();
        Examine(arcs, priming: true);

        Hold([Conflict("Lave", "War", 1, 2)], lave);
        Assert.Equal(CampaignArcCallout.KeyPrefix + "broadcast", Assert.Single(Examine(arcs)).Key);

        var report = Report(Place("Lave", 0.30, active: ["Retreat"]));
        var fronts = CampaignFronts.Of(report, _state.Standings, _store.For("F1"), _clock);
        _snapshot = new CampaignFrontsSnapshot("F1", Us, report, _clock, fronts, _clock, null, null);
        _store.Choose("F1", fronts[0].Kind, fronts[0].System, _clock);
        _state.BgsWork = BgsWork.None.Apply(Jump("Lave", Now.AddDays(-1), Faction(Us, 0.3)));

        var liaison = new CampaignLiaisonCallout(new NearbyFight())
        {
            Campaigns = _store,
            Fronts = () => _snapshot,
            TopicOn = topic => ShippedCallouts.TopicOn(_topics, topic),
        };

        Examine(liaison, priming: true);
        _state.BgsWork = _state.BgsWork with { Entries = [new BgsEntry(Now.AddMinutes(5), 1, Us, BgsWorkKind.Mission, 2)] };

        Assert.Empty(Examine(liaison));
    }

    [Fact]
    public void ASettingsFileWithTheRemovedKeysLoadsAndTheTopicRowsReadOn()
    {
        var read = JsonSerializer.Deserialize<D47Settings>(
            """{"callouts":{"campaignLiaison":false,"campaignArcs":false}}""",
            SettingsStore.Json)!;

        Assert.True(read.Callouts.BgsWars);
        Assert.True(read.Callouts.BgsElections);
        Assert.True(read.Callouts.BgsOther);
    }
}
