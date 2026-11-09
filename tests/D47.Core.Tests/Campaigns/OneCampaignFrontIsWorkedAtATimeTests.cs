using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// d47 proposes the top front, the Commander takes, skips or drops it, and a taken front is worked until fresh data
/// shows it resolved.
/// </summary>
public sealed class OneCampaignFrontIsWorkedAtATimeTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly CampaignFocus _focus;

    private CampaignFrontsSnapshot? _snapshot;

    private DateTimeOffset _clock = Now;

    private DateTimeOffset? _tick;

    public OneCampaignFrontIsWorkedAtATimeTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
        _focus = new CampaignFocus(_store, () => _snapshot, () => _state, () => _tick, () => _clock);
    }

    private static FactionSystem War(string name = "Lave") =>
        Place(name, 0.40, active: ["War"], others: [new FactionNeighbour("Lave Purple Gang", 0.39) { ActiveStates = ["War"] }]);

    private static FactionSystem Famine(string name = "Diso") => Place(name, 0.50, active: ["Famine"]);

    private void Hold(DateTimeOffset computedAt, params FactionSystem[] systems)
    {
        var report = Report(systems);

        _snapshot = new CampaignFrontsSnapshot(
            "F1",
            Us,
            report,
            computedAt,
            CampaignFronts.Of(report, _state.Standings, _store.For("F1"), computedAt),
            computedAt,
            null,
            null);
    }

    [Fact]
    public void TheTopFrontIsProposedAndNothingIsWritten()
    {
        Hold(Now, Famine(), War());

        _focus.Poll();

        Assert.Equal(FrontKind.Conflict, _focus.Proposal!.Front.Kind);
        Assert.Equal("Lave", _focus.Proposal.Front.System);
        Assert.Null(_store.For("F1").Chosen);
        Assert.Empty(_store.For("F1").Chronicle);
    }

    [Fact]
    public void NothingIsProposedWhenTheFactionHasNoFronts()
    {
        Hold(Now, Place("Lave", 0.60));

        _focus.Poll();

        Assert.Null(_focus.Proposal);
    }

    [Fact]
    public void TakingTheProposalChoosesItAndItSurvivesARestart()
    {
        Hold(Now, Famine(), War());
        _focus.Poll();

        var taken = _focus.Accept();

        Assert.Equal("Lave", taken!.System);
        Assert.Null(_focus.Proposal);

        var reloaded = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        var chosen = reloaded.For("F1").Chosen!;

        Assert.Equal((FrontKind.Conflict, "Lave", Now), (chosen.Kind, chosen.System, chosen.Since));
        Assert.Empty(chosen.Ticks);
    }

    [Fact]
    public void NothingIsProposedWhileAFrontIsChosen()
    {
        Hold(Now, Famine(), War());
        _focus.Poll();
        _focus.Accept();

        _focus.Poll();

        Assert.Null(_focus.Proposal);
        Assert.Null(_focus.Next());
    }

    [Fact]
    public void NextFrontProposesTheNextAndWrapsToTheTop()
    {
        Hold(Now, Famine(), War());
        _focus.Poll();

        Assert.Equal("Diso", _focus.Next()!.Front.System);
        Assert.Equal("Lave", _focus.Next()!.Front.System);
    }

    [Fact]
    public void TheProposalStaysTheSameAcrossPolls()
    {
        Hold(Now, Famine(), War());
        _focus.Poll();
        _focus.Next();

        _focus.Poll();

        Assert.Equal("Diso", _focus.Proposal!.Front.System);
    }

    [Fact]
    public void AFrontCanBeTakenByKindAndSystem()
    {
        Hold(Now, Famine(), War());

        Assert.Equal(FrontKind.BadState, _focus.Take(FrontKind.BadState, "diso")!.Kind);
        Assert.Equal(FrontKind.BadState, _store.For("F1").Chosen!.Kind);
    }

    [Fact]
    public void AFrontThatIsNotThereIsNotTaken()
    {
        Hold(Now, War());

        Assert.Null(_focus.Take(FrontKind.BadState, "Lave"));
        Assert.Null(_focus.Take(null, "Diso"));
        Assert.Null(_store.For("F1").Chosen);
    }

    [Fact]
    public void DroppingTheFrontClearsItAndWritesNoChronicle()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");

        Assert.True(_focus.Drop());
        Assert.Null(_store.For("F1").Chosen);
        Assert.Empty(_store.For("F1").Chronicle);
        Assert.False(_focus.Drop());
    }

    [Fact]
    public void NamingAnotherFactionClearsTheChosenFront()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");

        _store.SetFaction("F1", "Diso Gold Cartel", clearIntents: false);

        Assert.Null(_store.For("F1").Chosen);
    }

    [Fact]
    public void AFrontThatEndsInFreshDataIsWrittenToTheChronicleAndCleared()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");
        _tick = Now.AddHours(1);
        _focus.Poll();

        Hold(Now.AddHours(2), Place("Lave", 0.60));
        _focus.Poll();

        var campaign = _store.For("F1");

        Assert.Null(campaign.Chosen);

        var entry = Assert.Single(campaign.Chronicle);

        Assert.Equal("Conflict", entry.Kind);
        Assert.Equal("Lave", entry.System);
        Assert.Equal("Resolved", entry.Outcome);
        Assert.Equal([Now.AddHours(1)], entry.Ticks);
    }

    [Fact]
    public void AnEndedConflictRecordsWhoWonAndAgainstWhom()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");
        _state.Standings = _state.Standings.Apply(Jump(
            "Lave",
            Now.AddHours(1),
            $"{Faction(Us, 0.50)},{Faction("Lave Purple Gang", 0.30)}",
            $$"""{ "WarType":"war", "Status":"", "Faction1":{ "Name":"{{Us}}", "Stake":"", "WonDays":4 }, "Faction2":{ "Name":"Lave Purple Gang", "Stake":"", "WonDays":2 } }"""));
        Hold(Now.AddHours(2), Place("Lave", 0.50));
        _focus.Poll();

        var entry = Assert.Single(_store.For("F1").Chronicle);

        Assert.Equal("Won", entry.Outcome);
        Assert.Equal("Lave Purple Gang", entry.Opponent);
    }

    [Fact]
    public void FrontsComputedBeforeTheChoiceDoNotResolveIt()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");

        Hold(Now, Place("Lave", 0.60));
        _focus.Poll();

        Assert.NotNull(_store.For("F1").Chosen);
    }

    [Fact]
    public void AFailedFetchWithNoReportResolvesNothing()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");

        _snapshot = new CampaignFrontsSnapshot("F1", Us, null, null, [], Now.AddHours(2), "down", Now.AddHours(2));
        _focus.Poll();

        Assert.NotNull(_store.For("F1").Chosen);
    }

    [Fact]
    public void AThinMarginHiddenByAConflictIsNotResolved()
    {
        var thin = Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.35), new FactionNeighbour("Others", 0.25)]);

        Hold(Now, thin);
        Assert.Equal(FrontKind.ThinMargin, _focus.Take(null, "Lave")!.Kind);

        Hold(Now.AddHours(2), War());
        _focus.Poll();

        Assert.NotNull(_store.For("F1").Chosen);
        Assert.Empty(_store.For("F1").Chronicle);

        Hold(Now.AddHours(3), Place("Lave", 0.60));
        _focus.Poll();

        Assert.Null(_store.For("F1").Chosen);
        Assert.Single(_store.For("F1").Chronicle);
    }

    [Fact]
    public void ATickIsNotedOnceOnTheChosenFront()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");
        _tick = Now.AddHours(1);

        _focus.Poll();
        _focus.Poll();

        Assert.Equal([Now.AddHours(1)], _store.For("F1").Chosen!.Ticks);
    }

    [Fact]
    public void TheProposalIsHeldWhereAPageCanReadItAndTheTakePhraseExistsOnlyWhileItIs()
    {
        Hold(Now, War());

        Assert.DoesNotContain(Phrases(), command => command.Phrase == "take it");

        _focus.Poll();

        Assert.Equal("Lave", _focus.Proposal!.Front.System);
        Assert.Contains(Phrases(), command => command.Phrase == "take it" && command.ToolName == CampaignCapability.TakeFrontTool);
    }

    [Fact]
    public void ThePhrasesNameAFrontByItsKindAndSystem()
    {
        Hold(Now, War(), Famine());

        var war = Assert.Single(Phrases(), command => command.Phrase == "take the war in Lave");

        Assert.Equal("Lave", war.Arguments["system"]);
        Assert.Equal("Conflict", war.Arguments["kind"]);
        Assert.Contains(Phrases(), command => command.Phrase == "take the front in Diso");
    }

    [Fact]
    public void TheTakeNextAndDropToolsAreProtectedAndTheActionToolIsNot()
    {
        var tools = Tools().ToDictionary(tool => tool.Name);

        Assert.True(tools[CampaignCapability.TakeFrontTool].Protected);
        Assert.True(tools[CampaignCapability.NextFrontTool].Protected);
        Assert.True(tools[CampaignCapability.DropFrontTool].Protected);
        Assert.False(tools[CampaignCapability.NextActionTool].Protected);
    }

    [Fact]
    public void ATakeByKindAndSystemAnswersWithTheNextAction()
    {
        Hold(Now, War());

        var result = Call(CampaignCapability.TakeFrontTool, ("system", "Lave"), ("kind", "Conflict"));

        Assert.StartsWith("Working the conflict in Lave. Fight for " + Us + " in a conflict zone in Lave.", result.Content, StringComparison.Ordinal);
        Assert.Contains("Trade does not count in a war", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNextActionToolAnswersOneSentenceAndAtMostOneNotCounting()
    {
        Hold(Now, War());
        _focus.Take(null, "Lave");

        var result = Call(CampaignCapability.NextActionTool);

        Assert.Equal(
            $"Fight for {Us} in a conflict zone in Lave. Trade does not count in a war (guide p.61).",
            result.Content);
    }

    [Fact]
    public void WithNoFrontChosenTheNextActionToolNamesTheProposal()
    {
        Hold(Now, War());
        _focus.Poll();

        Assert.StartsWith("No front is chosen. D47 proposes the conflict in Lave", Call(CampaignCapability.NextActionTool).Content, StringComparison.Ordinal);
    }

    [Fact]
    public void NextFrontAndDropAnswerThroughTheirTools()
    {
        Hold(Now, Famine(), War());
        _focus.Poll();

        Assert.Contains("bad state in Diso", Call(CampaignCapability.NextFrontTool).Content, StringComparison.Ordinal);

        Call(CampaignCapability.TakeFrontTool);

        Assert.Equal("The front is dropped.", Call(CampaignCapability.DropFrontTool).Content);
        Assert.Equal("No front is chosen.", Call(CampaignCapability.DropFrontTool).Content);
    }

    private IEnumerable<DynamicCommand> Phrases() =>
        CampaignCapability.Phrases(_store, () => _state, null, null, _focus, () => _snapshot);

    private IReadOnlyList<ToolDefinition> Tools() =>
        CampaignCapability.Create(_store, () => _state, fronts: () => _snapshot, focus: _focus).Tools;

    private ToolResult Call(string name, params (string Key, string Value)[] arguments) =>
        Tools().Single(tool => tool.Name == name)
            .Handler(new ToolArguments(arguments.ToDictionary(pair => pair.Key, pair => pair.Value)), CancellationToken.None)
            .GetAwaiter().GetResult();
}
