using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The boundary the phase turns on (list.md Phase 17): <b>proposing is model-callable and
/// committing is not</b>, into two different files so the boundary is inspectable by looking at
/// <c>data/</c>.
/// <para>
/// Journal text and in-game comms are untrusted (architecture.md §7), so anything the model can
/// call, a hostile in-game message can attempt to invoke. The worst that should achieve here is a
/// proposal that gets declined.
/// </para>
/// </summary>
public class ChecklistTrustBoundaryTests
{
    private static CapabilityRegistry Registry(TempInstall install) => TestSurface.For(install).Registry;

    [Fact]
    public async Task TheModelCannotAcceptItsOwnProposal()
    {
        using var install = new TempInstall();

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
        using var install = new TempInstall();

        // The panel and the model-free keyword router are this caller. Same tool, same handler,
        // different answer — because protected is a property of the caller rather than of the
        // modality.
        var result = await Registry(install)
            .InvokeAsync("accept_proposal", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
    }

    [Fact]
    public void CommittingIsNeverAdvertised()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        var advertised = ToolProfiles.All(registry)
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Not caution — arithmetic. A tool that would refuse every call it received still costs
        // the cached prefix on every turn of the session (architecture.md §6).
        Assert.DoesNotContain("accept_proposal", advertised);
        Assert.DoesNotContain("decline_proposal", advertised);

        // And the reading half is advertised, or the capability would be useless.
        Assert.Contains("get_checklist", advertised);
        Assert.Contains("add_to_checklist", advertised);
    }

    [Fact]
    public void AcceptingIsStillReachableByVoiceThroughTheModelFreeRouter()
    {
        using var install = new TempInstall();

        // A protected tool is unreachable from the tool surface by design, so without a declared
        // phrase it could not be set by voice at all — and nothing would report that.
        var match = new KeywordRouter(Registry(install)).MatchToolCommand("accept the proposal");

        Assert.NotNull(match);
        Assert.Equal("accept_proposal", match.ToolName);
    }

    [Fact]
    public async Task AProposalNeverTouchesTheCommandersOwnFile()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        // Two files, and only one of them moved. This is the assertion a Commander can make for
        // themselves by opening data\.
        Assert.True(File.Exists(checklists.Proposals.Path));
        Assert.False(File.Exists(checklists.List.Path));
        Assert.Empty(checklists.Document.Items);

        await Task.CompletedTask;
    }

    [Fact]
    public void AcceptingMovesItAcrossAndTakesItOffTheProposalsFile()
    {
        using var install = new TempInstall();
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
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        checklists.Decline();

        Assert.Empty(checklists.Document.Items);
        Assert.Empty(checklists.Proposals.Pending);
    }

    [Fact]
    public void ProposalsAreBoundedSoOneRunawayCallerCannotBuryTheOneBeingAnswered()
    {
        using var install = new TempInstall();
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
        using var install = new TempInstall();
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

        // Observing rather than asserting. There is nothing here for the Commander to agree to,
        // so nothing is proposed.
        Assert.Contains("worked out from your journal", said, StringComparison.Ordinal);
        Assert.Empty(checklists.Proposals.Pending);
    }
}
