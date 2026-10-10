using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The boundary the phase turns on: proposing is model-callable and committing is not, into
/// two different files so the boundary is inspectable by looking at <c>data/</c>.
/// </summary>
public class ChecklistTrustBoundaryTests
{
    private static CapabilityRegistry Registry(MemoryInstall install) => TestSurface.For(install).Registry;

    [Fact]
    public async Task TheModelCannotAcceptItsOwnProposal()
    {
        var install = new MemoryInstall();

        var result = await Registry(install)
            .InvokeAsync(
                "accept_proposal",
                ToolArguments.Empty,
                TestContext.Current.CancellationToken,
                ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Contains("not something I can do on my own", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCommanderCanAcceptThroughTheSameTool()
    {
        var install = new MemoryInstall();

        // The panel and the model-free keyword router are this caller.
        var result = await Registry(install)
            .InvokeAsync("accept_proposal", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
    }

    [Fact]
    public void CommittingIsNeverAdvertised()
    {
        var install = new MemoryInstall();
        var registry = Registry(install);

        var advertised = ToolSurface.All(registry)
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Not caution — arithmetic.
        Assert.DoesNotContain("accept_proposal", advertised);
        Assert.DoesNotContain("decline_proposal", advertised);

        // And the reading half is advertised, or the capability would be useless.
        Assert.Contains("get_checklist", advertised);
        Assert.Contains("add_to_checklist", advertised);
    }

    [Fact]
    public void AcceptingIsStillReachableByVoiceThroughTheModelFreeRouter()
    {
        var install = new MemoryInstall();

        // A protected tool is unreachable from the tool surface by design, so without a declared phrase it
        // could not be set by voice at all — and nothing would report that.
        var match = new KeywordRouter(Registry(install)).MatchToolCommand("accept the proposal");

        Assert.NotNull(match);
        Assert.Equal("accept_proposal", match.ToolName);
    }

    [Fact]
    public async Task AProposalNeverTouchesTheCommandersOwnFile()
    {
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        // Two files, and only one of them moved.
        Assert.True(TestSurface.FilesFor(install.Paths).Stat(checklists.Proposals.Path) is not null);
        Assert.False(TestSurface.FilesFor(install.Paths).Stat(checklists.List.Path) is not null);
        Assert.Empty(checklists.Document.Items);

        await Task.CompletedTask;
    }

    [Fact]
    public void AcceptingMovesItAcrossAndTakesItOffTheProposalsFile()
    {
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        checklists.Accept();

        Assert.Single(checklists.Document.Items);
        Assert.Equal("buy limpets", checklists.Document.Items[0].Text);
        Assert.Empty(checklists.Proposals.Pending);
    }

    [Fact]
    public void DecliningLeavesTheListUntouched()
    {
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        checklists.Decline();

        Assert.Empty(checklists.Document.Items);
        Assert.Empty(checklists.Proposals.Pending);
    }

    [Fact]
    public void ProposalsAreBoundedSoOneRunawayCallerCannotBuryTheOneBeingAnswered()
    {
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        for (var n = 0; n < ChecklistLimits.MaxPendingProposals; n++)
        {
            checklists.ProposeAdd(ChecklistScope.Universal, [$"line {n}"]);
        }

        var refused = checklists.ProposeAdd(ChecklistScope.Universal, ["one too many"]);

        Assert.Contains("Accept or decline those first", refused, StringComparison.Ordinal);
        Assert.Equal(ChecklistLimits.MaxPendingProposals, checklists.Proposals.Pending.Count);
    }

    [Fact]
    public void ProposingThatAComputedItemIsDoneStatesTheJournalInsteadOfAsking()
    {
        var install = new MemoryInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Grade = 5 };

        checklists.List.Save(
        [
            ChecklistDocument.For(string.Empty) with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = ChecklistKeys.For(intent),
                        Scope = ChecklistScope.Ship(12),
                        Kind = ChecklistItemKind.Derived,
                        Source = ChecklistSource.EngineeringPlan,
                        Text = "Grade 5 on MainEngines",
                        Intent = intent,
                    },
                ],
            },
        ]);

        var said = checklists.ProposeChange("Grade 5 on MainEngines", ProposalKind.Complete);

        // Observing rather than asserting.
        Assert.Contains("worked out from your journal", said, StringComparison.Ordinal);
        Assert.Empty(checklists.Proposals.Pending);
    }
}
