using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// "turn off the campaign" stops every campaign surface and keeps the faction, the intents and the chronicle; "turn on
/// the campaign" starts it again as d47 starting does.
/// </summary>
public sealed class TheCampaignCanBeTurnedOffAndOnTests
{
    private const string Path = "campaigns.json";

    private const string Rival = "Lave Purple Gang";

    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly Queue<Func<Task>> _dispatched = new();

    private readonly CampaignFrontsWatch _watch;

    private readonly CampaignFocus _focus;

    private DateTimeOffset? _tick = Now.AddHours(-5);

    public TheCampaignCanBeTurnedOffAndOnTests()
    {
        _store = new CampaignStore(Path, _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);

        _watch = new CampaignFrontsWatch(
            (_, _, _) => Task.FromResult(Report(Place("Diso", 0.50, active: ["Famine"]))),
            () => _state,
            _store,
            null,
            () => _tick,
            null,
            NullLogger<CampaignFrontsWatch>.Instance)
        {
            Dispatch = _dispatched.Enqueue,
        };

        _focus = new CampaignFocus(_store, () => _watch.Snapshot, () => _state, () => _tick, () => Now);
    }

    private void Poll()
    {
        _watch.Poll(Now);

        while (_dispatched.TryDequeue(out var work))
        {
            work().GetAwaiter().GetResult();
        }

        _watch.Poll(Now);
        _focus.Poll();
    }

    private CampaignStore Reloaded()
    {
        var store = new CampaignStore(Path, _files, NullLogger<CampaignStore>.Instance);
        store.Load();
        return store;
    }

    private CapabilityRegistry Registry() =>
        CapabilityRegistry.Build([CampaignCapability.Create(_store, () => _state, fronts: () => _watch.Snapshot, focus: _focus)]);

    private Task<ToolResult> Invoke(string tool, ToolArguments arguments, ToolCaller caller = ToolCaller.Commander) =>
        Registry().InvokeAsync(tool, arguments, caller: caller, cancellationToken: TestContext.Current.CancellationToken);

    private static ToolArguments On(bool on) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { ["on"] = on ? "true" : "false" });

    [Fact]
    public void TheOffFlagSurvivesARestartOnACampaignThatHoldsNothingElse()
    {
        Assert.True(_store.SetOff("F2", off: true));

        Assert.True(Reloaded().For("F2").Off);
        Assert.Contains("\"off\": true", _files.ReadText(Path), StringComparison.Ordinal);

        Assert.True(_store.SetOff("F2", off: false));

        Assert.False(Reloaded().For("F2").Off);
        Assert.DoesNotContain("\"off\"", _files.ReadText(Path), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOffCampaignIsNotEmpty()
    {
        Assert.True(Campaign.None.IsEmpty);
        Assert.False((Campaign.None with { Off = true }).IsEmpty);
    }

    [Fact]
    public void AnOffCampaignHasNoFactionWhetherNamedOrTheSquadrons()
    {
        var off = Campaign.None with { Off = true };

        Assert.Null(off.FactionOr("Squadron Faction"));
        Assert.Null((off with { Faction = "Named" }).FactionOr("Squadron Faction"));
        Assert.Null((off with { Faction = "Named" }).FactionOr(null));
    }

    [Fact]
    public void WhileOffNoReportIsHeldAndNoneIsFetchedAfterATick()
    {
        Poll();
        Assert.NotNull(_watch.Snapshot);

        _store.SetOff("F1", off: true);
        _watch.Poll(Now);

        Assert.Null(_watch.Snapshot);

        _tick = Now;
        _watch.Poll(Now.AddMinutes(1));
        _watch.Poll(Now.AddHours(1));

        Assert.Empty(_dispatched);
        Assert.Null(_watch.Snapshot);
    }

    [Fact]
    public async Task AFetchUnderWayWhenTurnedOffIsDropped()
    {
        _watch.Poll(Now);
        _store.SetOff("F1", off: true);

        await _dispatched.Dequeue()();
        _watch.Poll(Now);

        Assert.Null(_watch.Snapshot);
        Assert.Empty(_dispatched);
    }

    [Fact]
    public void WhileOffNothingIsProposedAndThereIsNoNextAction()
    {
        Poll();
        Assert.NotNull(_focus.Proposal);
        Assert.NotNull(_focus.Accept());
        Assert.NotNull(_focus.NextAction());

        _store.SetOff("F1", off: true);
        Poll();

        Assert.Null(_focus.Proposal);
        Assert.Null(_focus.NextAction());
    }

    [Fact]
    public void TurningOffDropsTheChosenFrontAndKeepsTheRest()
    {
        _store.SetIntent("F1", "Diso", SystemIntent.Grow);
        _store.Choose("F1", FrontKind.BadState, "Diso", Now);
        var grew = 0;
        _store.ChronicleGrew += _ => grew++;

        _store.SetOff("F1", off: true);

        var campaign = Reloaded().For("F1");
        Assert.Null(campaign.Chosen);
        Assert.Empty(campaign.Chronicle);
        Assert.Equal(0, grew);
        Assert.Equal(Us, campaign.Faction);
        Assert.Equal(SystemIntent.Grow, campaign.Intents["Diso"]);
    }

    [Fact]
    public void AProposalHeldFromBeforeTheSwitchCannotBeTaken()
    {
        Poll();
        Assert.NotNull(_focus.Proposal);

        _store.SetOff("F1", off: true);

        Assert.Null(_focus.Accept());
        Assert.Null(_store.For("F1").Chosen);
    }

    [Fact]
    public void TurningOnAgainResumesFromTheKeptFaction()
    {
        _store.SetOff("F1", off: true);
        Poll();
        Assert.Null(_watch.Snapshot);

        _store.SetOff("F1", off: false);
        Poll();

        Assert.Equal(Us, _watch.Snapshot!.Faction);
        Assert.NotNull(_focus.Proposal);
    }

    [Fact]
    public void ADayWonWhileOffIsNotBroadcastWhenTheCampaignIsTurnedOnAgain()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        IReadOnlyList<Front> fronts = [War(1, 1)];
        var arcs = new CampaignArcCallout(new NearbyFight())
        {
            Campaigns = _store,
            Fronts = () => _watch.Snapshot is { } held ? held with { Fronts = fronts } : null,
            Voice = (_, _) => new PinnedVoice("edge", "en-GB-RyanNeural") { Link = CampaignArcs.Link },
        };

        Poll();
        Examine(arcs, priming: true);
        Assert.Empty(Examine(arcs));

        _store.SetOff("F1", off: true);
        Poll();
        Assert.Empty(Examine(arcs));

        fronts = [War(1, 2)];
        Assert.Empty(Examine(arcs));

        _store.SetOff("F1", off: false);
        _watch.Poll(Now);
        Assert.Empty(Examine(arcs));

        Poll();
        Assert.Empty(Examine(arcs));

        fronts = [War(1, 3)];
        Assert.Equal(CampaignArcCallout.KeyPrefix + "broadcast", Assert.Single(Examine(arcs)).Key);
    }

    [Fact]
    public void ThePhrasesReachTheSwitchOnTheKeywordPath()
    {
        var router = new KeywordRouter(Registry());

        var off = router.MatchToolCommand("turn off the campaign");
        var on = router.MatchToolCommand("turn on the campaign");

        Assert.Equal(CampaignCapability.SwitchTool, off?.ToolName);
        Assert.Equal(false, off!.Arguments.Boolean("on"));
        Assert.Equal(CampaignCapability.SwitchTool, on?.ToolName);
        Assert.Equal(true, on!.Arguments.Boolean("on"));
    }

    [Fact]
    public async Task TheModelIsRefusedTheSwitch()
    {
        var tool = CampaignCapability.Create(_store, () => _state).Tools.Single(tool => tool.Name == CampaignCapability.SwitchTool);
        Assert.True(tool.Protected);
        Assert.False(string.IsNullOrWhiteSpace(tool.Exercise));

        var refused = await Invoke(CampaignCapability.SwitchTool, On(false), ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.False(_store.For("F1").Off);
    }

    [Fact]
    public async Task TheCommanderTurnsItOffAndOnAndHearsSo()
    {
        _store.Choose("F1", FrontKind.BadState, "Diso", Now);

        var off = await Invoke(CampaignCapability.SwitchTool, On(false));

        Assert.False(off.IsError);
        Assert.StartsWith("The campaign is off", off.Content, StringComparison.Ordinal);
        Assert.Contains("Diso is dropped", off.Content, StringComparison.Ordinal);
        Assert.True(_store.For("F1").Off);
        Assert.Equal("The campaign is already off.", (await Invoke(CampaignCapability.SwitchTool, On(false))).Content);

        var on = await Invoke(CampaignCapability.SwitchTool, On(true));

        Assert.Equal("The campaign is on again.", on.Content);
        Assert.False(_store.For("F1").Off);
        Assert.Equal("The campaign is already on.", (await Invoke(CampaignCapability.SwitchTool, On(true))).Content);
    }

    [Fact]
    public async Task WhileOffTheOtherToolsSaySoAndChangeNothing()
    {
        _store.SetOff("F1", off: true);

        var intent = new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = "Diso", ["intent"] = "Grow" });
        var faction = new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = "Diso Gold Cartel" });

        (string Tool, ToolArguments Arguments)[] calls =
        [
            (CampaignCapability.GetTool, ToolArguments.Empty),
            (CampaignCapability.FrontsTool, ToolArguments.Empty),
            (CampaignCapability.ChronicleTool, ToolArguments.Empty),
            (CampaignCapability.NextActionTool, ToolArguments.Empty),
            (CampaignCapability.TakeFrontTool, ToolArguments.Empty),
            (CampaignCapability.NextFrontTool, ToolArguments.Empty),
            (CampaignCapability.DropFrontTool, ToolArguments.Empty),
            (CampaignCapability.SetFactionTool, faction),
            (CampaignCapability.SetIntentTool, intent),
        ];

        foreach (var (tool, arguments) in calls)
        {
            var result = await Invoke(tool, arguments);

            Assert.False(result.IsError, tool);
            Assert.StartsWith("The campaign is off.", result.Content, StringComparison.Ordinal);
        }

        var campaign = _store.For("F1");
        Assert.Equal(Us, campaign.Faction);
        Assert.Empty(campaign.Intents);
    }

    private static Front War(int ours, int theirs) =>
        new(FrontKind.Conflict, "Lave", $"War against {Rival}, active.", SystemIntent.Hold, true, Rival, "", ours - theirs, Now, false)
        {
            Conflict = new FactionConflict("Lave", "War", true, OpponentKind.Named, Rival, [], true, ours, theirs, "", ""),
        };

    private List<Announcement> Examine(CampaignArcCallout arcs, bool priming = false) =>
        [.. arcs.Examine(new CalloutContext(Now, priming, _state, new GameStatus { Flags = StatusFlags.Docked }, NavRoute.None, []))];
}
