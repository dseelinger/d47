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
/// The Liaison briefs a chosen front, asks for the morning report and gives one dispatch per tick, says the outcome with
/// the next front, and waits while the Commander is in supercruise.
/// </summary>
public sealed class TheLiaisonSpeaksAboutTheChosenFrontTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly CampaignFocus _focus;

    private CampaignFrontsSnapshot? _snapshot;

    private DateTimeOffset _clock = Now;

    private DateTimeOffset? _tick;

    public TheLiaisonSpeaksAboutTheChosenFrontTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
        _focus = new CampaignFocus(_store, () => _snapshot, () => _state, () => _tick, () => _clock);
    }

    private CampaignLiaisonCallout Liaison() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
        Proposal = () => _focus.Proposal,
        LastTick = () => _tick,
        Voice = _ => new PinnedVoice("edge", "en-GB-RyanNeural") { Link = CampaignLiaison.Link },
    };

    private static FactionSystem Retreat(string name = "Lave") => Place(name, 0.30, active: ["Retreat"]);

    private void Hold(params FactionSystem[] systems)
    {
        var report = Report(systems);

        _snapshot = new CampaignFrontsSnapshot("F1", Us, report, _clock, CampaignFronts.Of(report, _state.Standings, _store.For("F1"), _clock), _clock, null, null);
    }

    private List<Announcement> Examine(CampaignLiaisonCallout liaison, bool priming = false, StatusFlags flags = StatusFlags.Docked) =>
        [.. liaison.Examine(new CalloutContext(_clock, priming, _state, new GameStatus { Flags = flags }, NavRoute.None, []))];

    private CampaignLiaisonCallout Started()
    {
        var liaison = Liaison();
        Examine(liaison, priming: true);
        Assert.Empty(Examine(liaison));
        return liaison;
    }

    [Fact]
    public void AFrontAlreadyChosenAtStartupIsNotBriefedAgain()
    {
        Hold(Retreat());
        _focus.Take(null, "Lave");

        Assert.Empty(Examine(Started()));
    }

    [Fact]
    public void ChoosingAFrontBriefsItOverCommsInTheLiaisonsVoice()
    {
        Hold(Retreat());
        var liaison = Started();

        _focus.Take(null, "Lave");

        var said = Assert.Single(Examine(liaison));

        Assert.Equal(CampaignLiaisonCallout.KeyPrefix + "briefing", said.Key);
        Assert.StartsWith("The front is the retreat in Lave.", said.Text, StringComparison.Ordinal);
        Assert.Equal(VoiceRole.Comms, said.Voice);
        Assert.Equal(CampaignLiaison.Speaker, said.Speaker);
        Assert.True(said.InRoleVoice);
        Assert.Equal(CampaignLiaison.Link, said.Pinned!.Link);
        Assert.Null(said.CommsChannel);
        Assert.Empty(Examine(liaison));
    }

    [Fact]
    public void TheBriefingIsWrittenFromItsOwnBriefWhateverCoreIsAboard()
    {
        Hold(Retreat());
        var liaison = Started();
        _focus.Take(null, "Lave");

        var said = Assert.Single(Examine(liaison));
        var brief = FlavourBriefs.For(said, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.False(brief.NeedsPersona);
        Assert.Contains("liaison officer for Kestrel Reach Compact", brief.Speaker, StringComparison.Ordinal);
        Assert.Contains(said.Text, brief.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBriefingWaitsOutSupercruise()
    {
        Hold(Retreat());
        var liaison = Started();
        _focus.Take(null, "Lave");

        Assert.Empty(Examine(liaison, flags: StatusFlags.Supercruise));

        Assert.Single(Examine(liaison, flags: StatusFlags.InMainShip));
    }

    [Fact]
    public void AfterATickWithNoNewerReadingTheLiaisonAsksForTheMorningReportThenGivesTheDispatch()
    {
        Hold(Retreat());
        _focus.Take(null, "Lave");
        var liaison = Started();

        _clock = Now.AddHours(1);
        _tick = Now.AddMinutes(30);

        var asked = Assert.Single(Examine(liaison));

        Assert.Equal(CampaignLiaisonCallout.KeyPrefix + "morningreport", asked.Key);
        Assert.Empty(Examine(liaison));

        _state.Standings = _state.Standings.Apply(Jump("Lave", Now.AddMinutes(50), $"{Faction(Us, 0.31)},{Faction("Lave Purple Gang", 0.1)}"));

        var dispatch = Assert.Single(Examine(liaison));

        Assert.Equal(CampaignLiaisonCallout.KeyPrefix + "dispatch", dispatch.Key);
        Assert.Equal(_tick, _store.For("F1").Chosen!.Reported);
        Assert.Empty(Examine(liaison));
    }

    [Fact]
    public void ATickHasOneDispatchAcrossARestart()
    {
        Hold(Retreat());
        _focus.Take(null, "Lave");
        _state.Standings = _state.Standings.Apply(Jump("Lave", Now.AddMinutes(50), $"{Faction(Us, 0.31)},{Faction("Lave Purple Gang", 0.1)}"));
        _clock = Now.AddHours(1);
        _tick = Now.AddMinutes(30);

        var first = Liaison();
        Examine(first, priming: true);

        Assert.Equal(CampaignLiaisonCallout.KeyPrefix + "dispatch", Assert.Single(Examine(first)).Key);

        var reloaded = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        Assert.Equal(_tick, reloaded.For("F1").Chosen!.Reported);

        var restarted = Liaison();
        restarted.Campaigns = reloaded;
        Examine(restarted, priming: true);

        Assert.Empty(Examine(restarted));
    }

    [Fact]
    public void ATickBeforeTheFrontWasChosenIsNotReported()
    {
        _tick = Now.AddMinutes(-30);
        Hold(Retreat());
        _focus.Take(null, "Lave");

        Assert.Empty(Examine(Started()));
    }

    [Fact]
    public void AResolvedFrontGetsItsOutcomeOnceTheNextFrontIsProposed()
    {
        Hold(Retreat(), Retreat("Diso"));
        _focus.Take(null, "Lave");
        var liaison = Started();

        _clock = Now.AddHours(1);
        Hold(Retreat("Diso"));
        _focus.Poll();

        Assert.Null(_store.For("F1").Chosen);
        Assert.Empty(Examine(liaison));

        _focus.Poll();

        var said = Assert.Single(Examine(liaison));

        Assert.Equal(CampaignLiaisonCallout.KeyPrefix + "resolution", said.Key);
        Assert.Equal(
            "The retreat in Lave is over. It is resolved. The next front proposed is the retreat in Diso: Retreat is active here.",
            said.Text);
    }

    [Fact]
    public void WorkThatCountsOnTheFrontIsAcknowledged()
    {
        Hold(Retreat());
        _focus.Take(null, "Lave");
        _state.BgsWork = BgsWork.None.Apply(Jump("Lave", Now.AddDays(-1), Faction(Us, 0.3)));
        var liaison = Started();

        _state.BgsWork = _state.BgsWork with { Entries = [new BgsEntry(Now.AddMinutes(5), 1, Us, BgsWorkKind.Mission, 2)] };

        var said = Assert.Single(Examine(liaison));

        Assert.Equal("You have just completed 1 mission for Kestrel Reach Compact in Lave.", said.Text);

        _state.BgsWork = _state.BgsWork with { Entries = [.. _state.BgsWork.Entries, new BgsEntry(Now.AddMinutes(6), 1, Us, BgsWorkKind.Mission, 1)] };

        Assert.Empty(Examine(liaison));
    }
}
