using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Callouts;

/// <summary>The Liaison briefs every war, civil war and election of the campaign faction once per BGS tick.</summary>
public sealed class TheLiaisonBriefsEveryWarAndElectionTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private CampaignFrontsSnapshot? _snapshot;

    private DateTimeOffset _clock = Now;

    private DateTimeOffset? _tick = Now.AddHours(-2);

    public TheLiaisonBriefsEveryWarAndElectionTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
    }

    private static FactionNeighbour Rival(string state, bool active = true) =>
        new("Lave Purple Gang", 0.2)
        {
            ActiveStates = active ? [state] : [],
            PendingStates = active ? [] : [state],
        };

    private void Hold(params FactionSystem[] systems)
    {
        var report = Report(systems);

        _snapshot = new CampaignFrontsSnapshot("F1", Us, report, _clock, [], _clock, null, null);
    }

    private void AllThree() => Hold(
        Place("Diso", 0.4, active: ["War"], others: [Rival("War")]),
        Place("Lave", 0.4, pending: ["Civil war"], others: [Rival("Civil war", active: false)]),
        Place("Leesti", 0.4, pending: ["Election"], others: [Rival("Election", active: false)]));

    private CampaignLiaisonCallout Liaison(CampaignStore? store = null) => new(new NearbyFight())
    {
        Campaigns = store ?? _store,
        Fronts = () => _snapshot,
        LastTick = () => _tick,
        TopicOn = topic => topic != BgsTopic.War || _wars,
    };

    private bool _wars = true;

    private List<Announcement> Examine(CampaignLiaisonCallout liaison, bool priming = false, StatusFlags flags = StatusFlags.Docked) =>
        [.. liaison.Examine(new CalloutContext(_clock, priming, _state, new GameStatus { Flags = flags }, NavRoute.None, []))];

    private CampaignLiaisonCallout Started(CampaignStore? store = null)
    {
        var liaison = Liaison(store);
        Examine(liaison, priming: true);
        return liaison;
    }

    [Fact]
    public void OneLineNamesEveryConflictWarThenCivilWarThenElection()
    {
        AllThree();

        var said = Assert.Single(Examine(Started()));

        Assert.Equal(CampaignLiaisonCallout.KeyPrefix + "conflicts", said.Key);

        var war = said.Text.IndexOf("Diso", StringComparison.Ordinal);
        var civil = said.Text.IndexOf("Lave", StringComparison.Ordinal);
        var election = said.Text.IndexOf("Leesti", StringComparison.Ordinal);

        Assert.True(war >= 0 && war < civil && civil < election, said.Text);
        Assert.Contains("active war against Lave Purple Gang", said.Text, StringComparison.Ordinal);
        Assert.Contains("pending civil war", said.Text, StringComparison.Ordinal);
        Assert.Contains("pending election", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithWarCalloutsOffTheBriefingNamesTheElectionAndNotTheWar()
    {
        Hold(
            Place("Diso", 0.4, active: ["War"], others: [Rival("War")]),
            Place("Leesti", 0.4, pending: ["Election"], others: [Rival("Election", active: false)]));
        _wars = false;

        var said = Assert.Single(Examine(Started()));

        Assert.Contains("Leesti", said.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diso", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithWarAndElectionCalloutsOffTheBriefingIsNotSaid()
    {
        AllThree();
        var liaison = Liaison();
        liaison.TopicOn = topic => topic == BgsTopic.Other;
        Examine(liaison, priming: true);

        Assert.Empty(Examine(liaison));
    }

    [Fact]
    public void ATickNotYetBriefedIsBriefedOnTheFirstPassWithAReportAndNotAgain()
    {
        AllThree();

        var liaison = Started();

        Assert.Single(Examine(liaison));
        Assert.Empty(Examine(liaison));
    }

    [Fact]
    public void ATickBriefedAndSavedIsNotBriefedAgainAfterARestart()
    {
        AllThree();
        Assert.Single(Examine(Started()));

        var reloaded = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        Assert.Equal(_tick, reloaded.For("F1").ConflictsBriefed);
        Assert.Empty(Examine(Started(reloaded)));
    }

    [Fact]
    public void ABriefingBeforeATickIsGivenAgainAfterTheNextTick()
    {
        AllThree();
        var liaison = Started();
        Assert.Single(Examine(liaison));

        _tick = Now.AddMinutes(-5);

        Assert.Single(Examine(liaison));
        Assert.Empty(Examine(liaison));
    }

    [Fact]
    public void AnOpponentThatIsOneOfSeveralIsNotYetKnown()
    {
        Hold(Place("Diso", 0.4, active: ["War"], others: [Rival("War"), new FactionNeighbour("Second Rival", 0.1) { ActiveStates = ["War"] }]));

        var said = Assert.Single(Examine(Started()));

        Assert.Contains("opponent not yet known", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidWithoutConflictsAndTheBriefingWaitsOutSupercruise()
    {
        Hold(Place("Diso", 0.4));

        var quiet = Started();

        Assert.Empty(Examine(quiet));

        AllThree();
        _tick = Now.AddMinutes(-5);

        Assert.Empty(Examine(quiet, flags: StatusFlags.Supercruise));
        Assert.Single(Examine(quiet, flags: StatusFlags.InMainShip));
    }
}
