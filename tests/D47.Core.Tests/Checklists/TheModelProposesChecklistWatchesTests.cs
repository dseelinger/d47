using D47.Core.Capabilities;
using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>The model can propose a watch on a new or an existing line; only the Commander's yes arms it (#1057).</summary>
public class TheModelProposesChecklistWatchesTests
{
    private const string Station = "Archambeau's Serenity +";

    private static readonly DateTimeOffset Asked = DateTimeOffset.Parse("2026-10-10T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset Agreed = Asked.AddMinutes(10);

    private static Task<ToolResult> Call(TestSurface surface, string tool, params (string Name, string Value)[] arguments) =>
        surface.Registry.InvokeAsync(
            tool,
            new ToolArguments(arguments.ToDictionary(pair => pair.Name, pair => pair.Value)),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task AnAcceptedAddKeepsItsWatchAndStartsCountingFromTheYes()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var checklists = surface.ChecklistService;

        var result = await Call(
            surface,
            "add_to_checklist",
            ("text", "Buy 5 Hydrogen Fuel"),
            ("tick_when", "buy"),
            ("subject", "Hydrogen Fuel"),
            ("place", Station),
            ("quantity", "5"));

        Assert.False(result.IsError);
        var waiting = Assert.Single(checklists.Proposals.Pending);
        Assert.Equal(
            $"Add \"Buy 5 Hydrogen Fuel\" to the custom list, ticked when you buy 5 Hydrogen Fuel at {Station}",
            waiting.Summary);

        checklists.Accept();

        var line = Assert.Single(checklists.Document.Items);
        var watch = Assert.IsType<ChecklistWatch>(line.Watch);
        Assert.Equal(ChecklistWatchKind.Buy, watch.Kind);
        Assert.Equal("Hydrogen Fuel", watch.Subject);
        Assert.Equal(Station, watch.Place);
        Assert.Equal(5, watch.Quantity);
        Assert.Equal(0, watch.Progress);
        Assert.NotEqual(default, watch.Since);
    }

    [Fact]
    public async Task ADeclinedAddLeavesNothingOnTheList()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var checklists = surface.ChecklistService;

        await Call(surface, "add_to_checklist", ("text", "Go to LTT 7786"), ("tick_when", "arrive"), ("subject", "LTT 7786"));
        checklists.Decline();

        Assert.Empty(checklists.Document.Items);
        Assert.Empty(checklists.Proposals.Pending);
    }

    [Fact]
    public async Task AnAcceptedWatchArmsTheExistingLineFromTheMomentOfAcceptance()
    {
        var clock = Asked;
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths, now: () => clock);
        var line = checklists.AddNote(ChecklistScope.Universal, "CAPI probe 2: dock at Archambeau's Serenity + in LTT 7786").Subject!.Value;

        checklists.ProposeChange(
            "CAPI probe 2",
            ProposalKind.Watch,
            new ChecklistWatch(ChecklistWatchKind.Dock, Station) { Place = "LTT 7786" });

        var waiting = Assert.Single(checklists.Proposals.Pending);
        Assert.Equal(
            $"Tick \"CAPI probe 2: dock at Archambeau's Serenity + in LTT 7786\" when you dock at {Station} in LTT 7786",
            waiting.Summary);
        Assert.Null(checklists.Document.Find(line)!.Watch);

        clock = Agreed;
        checklists.Accept();

        var watch = Assert.IsType<ChecklistWatch>(checklists.Document.Find(line)!.Watch);
        Assert.Equal(ChecklistWatchKind.Dock, watch.Kind);
        Assert.Equal(Agreed, watch.Since);
    }

    [Fact]
    public async Task ARefusedConditionRecordsNoProposal()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var checklists = surface.ChecklistService;

        var result = await Call(
            surface,
            "add_to_checklist",
            ("text", "Dock at Jameson Memorial"),
            ("tick_when", "dock"),
            ("subject", "Jameson Memorial"),
            ("quantity", "3"));

        Assert.Contains("quantity", result.Content, StringComparison.Ordinal);
        Assert.Empty(checklists.Proposals.Pending);
        Assert.Empty(checklists.Document.Items);
    }

    [Fact]
    public void TwoWatchProposalsForOneLineWithDifferentConditionsAreNotDuplicates()
    {
        var checklists = TestSurface.Checklists(new MemoryInstall().Paths, now: () => Asked);
        checklists.AddNote(ChecklistScope.Universal, "CAPI probe 3");

        checklists.ProposeChange("CAPI probe 3", ProposalKind.Watch, new ChecklistWatch(ChecklistWatchKind.Buy, "Hydrogen Fuel") { Quantity = 5 });
        checklists.ProposeChange("CAPI probe 3", ProposalKind.Watch, new ChecklistWatch(ChecklistWatchKind.Buy, "Hydrogen Fuel") { Quantity = 6 });

        Assert.Equal(2, checklists.Proposals.Pending.Count);
    }

    [Fact]
    public async Task AWatchProposalSurvivesBeingReloadedFromItsFile()
    {
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths, now: () => Asked);
        checklists.AddNote(ChecklistScope.Universal, "CAPI probe 3");
        checklists.ProposeChange("CAPI probe 3", ProposalKind.Watch, new ChecklistWatch(ChecklistWatchKind.Arrive, "LTT 7786"));

        var reloaded = TestSurface.Checklists(install.Paths, now: () => Asked);

        reloaded.Proposals.Poll();
        Assert.Equal(ProposalKind.Watch, Assert.Single(reloaded.Proposals.Pending).Kind);
        await Task.CompletedTask;
    }
}
