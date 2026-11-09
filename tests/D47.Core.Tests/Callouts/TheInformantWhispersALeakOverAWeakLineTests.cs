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
/// The Informant leaks a computed threat over comms in its own voice on a weak line, once, and is kept in the campaign
/// record with its leaks.
/// </summary>
public sealed class TheInformantWhispersALeakOverAWeakLineTests
{
    private const string Rival = "Lave Purple Gang";

    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private CampaignFrontsSnapshot? _snapshot;

    public TheInformantWhispersALeakOverAWeakLineTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
    }

    private void Hold(double rival) =>
        _snapshot = new CampaignFrontsSnapshot(
            "F1",
            Us,
            Report(Place("Lave", 0.40, others: [new FactionNeighbour(Rival, rival)])),
            Now,
            [],
            Now,
            null,
            null);

    private CampaignArcCallout Arcs() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
        InformantVoice = (_, _, _) => new PinnedVoice("edge", "en-GB-SoniaNeural") { Link = CampaignLeaks.Link },
    };

    private List<Announcement> Examine(CampaignArcCallout arcs, bool priming = false, StatusFlags flags = StatusFlags.Docked) =>
        [.. arcs.Examine(new CalloutContext(Now, priming, _state, new GameStatus { Flags = flags }, NavRoute.None, []))];

    [Fact]
    public void ALeakIsSpokenOnceOverAWeakLineAndRemembered()
    {
        var arcs = Arcs();
        Hold(0.45);
        Examine(arcs, priming: true);
        Hold(0.53);

        var said = Assert.Single(Examine(arcs));

        var informant = _store.For("F1").Informant;
        Assert.NotNull(informant);
        Assert.Equal(CampaignArcCallout.KeyPrefix + "leak", said.Key);
        Assert.Equal(informant.Name, said.Speaker);
        Assert.Equal(VoiceRole.Comms, said.Voice);
        Assert.Equal(CampaignLeaks.Link, said.Pinned!.Link);
        Assert.True(CampaignLeaks.Link < CampaignArcs.Link);
        Assert.Equal("Lave", said.Arc!.System);
        Assert.Single(informant.Leaks);

        Hold(0.53);
        Assert.Empty(Examine(arcs));
    }

    [Fact]
    public void ALeakWaitsOutSupercruiseAndIsNotLostByIt()
    {
        var arcs = Arcs();
        Hold(0.45);
        Examine(arcs, priming: true);
        Hold(0.53);

        Assert.Empty(Examine(arcs, flags: StatusFlags.Supercruise));
        Assert.Null(_store.For("F1").Informant);
        Assert.Single(Examine(arcs));
    }

    [Fact]
    public void NoLeakIsSpokenWithoutARisingThreat()
    {
        var arcs = Arcs();
        Hold(0.53);
        Examine(arcs, priming: true);
        Hold(0.53);

        Assert.Empty(Examine(arcs));
        Assert.Null(_store.For("F1").Informant);
    }

    [Fact]
    public void NamingAnotherFactionClearsTheInformant()
    {
        _store.SetInformant("F1", new CampaignInformant("Wren", Now, []));

        _store.SetFaction("F1", "Another Faction", clearIntents: false);

        Assert.Null(_store.For("F1").Informant);
    }

    [Fact]
    public void TheInformantAndItsLeaksSurviveAReload()
    {
        var leak = new Leak(LeakKind.Coup, "Lave", Rival, 0.53, 2, Now);
        _store.SetInformant("F1", new CampaignInformant("Wren", Now, [leak]));

        var store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        store.Load();
        var reloaded = store.For("F1").Informant;

        Assert.NotNull(reloaded);
        Assert.Equal("Wren", reloaded.Name);
        Assert.Equal(leak, Assert.Single(reloaded.Leaks));
    }
}
