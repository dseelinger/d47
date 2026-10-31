using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>"mark buy a fuel scoop as done" and "remove buy a fuel scoop" propose the change without the model, for a line of the Commander's own (#1957).</summary>
public class AChecklistLineNamedInFullIsChangedWithoutTheModelTests
{
    [Fact]
    public void AnOpenLineOfTheirOwnCanBeMarkedDoneOrRemoved()
    {
        var checklists = TestSurface.Checklists(new MemoryInstall().Paths);
        checklists.AddNote(ChecklistScope.Universal, "buy a fuel scoop");

        var phrases = ChecklistCapability.Phrases(checklists).ToDictionary(command => command.Phrase);

        Assert.Equal("done", phrases["mark buy a fuel scoop as done"].Arguments["change"]);
        Assert.Equal("remove", phrases["remove buy a fuel scoop"].Arguments["change"]);
        Assert.All(phrases.Values, command => Assert.Equal("buy a fuel scoop", command.Arguments["item"]));
        Assert.All(phrases.Values, command => Assert.Equal("propose_checklist_change", command.ToolName));
    }

    [Fact]
    public void AFinishedLineCanBeRemovedButNotMarkedDoneAgain()
    {
        var checklists = TestSurface.Checklists(new MemoryInstall().Paths);
        checklists.AddNote(ChecklistScope.Universal, "buy a fuel scoop");
        checklists.ProposeChange("buy a fuel scoop", ProposalKind.Complete);
        checklists.Accept();

        var phrases = ChecklistCapability.Phrases(checklists).Select(command => command.Phrase).ToList();

        Assert.Contains("remove buy a fuel scoop", phrases);
        Assert.DoesNotContain("mark buy a fuel scoop as done", phrases);
    }

    [Fact]
    public void TwoLinesWithTheSameWordsGetNoPhrase()
    {
        var checklists = TestSurface.Checklists(new MemoryInstall().Paths);
        checklists.AddNote(ChecklistScope.Universal, "buy a fuel scoop");
        checklists.AddNote(ChecklistScope.System("Shinrarta Dezhra"), "Buy a fuel scoop.");

        Assert.Empty(ChecklistCapability.Phrases(checklists));
    }

    [Fact]
    public async Task MarkingALineDoneAsksAndYesTicksIt()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.ChecklistService.AddNote(ChecklistScope.Universal, "buy a fuel scoop");
        var model = FakeLlmProvider.Answering("From the model.");
        var loop = Loop(surface, model);

        var asked = await Said(loop, "mark buy a fuel scoop as done");
        await Said(loop, "yes");

        Assert.Equal(0, model.CallCount);
        Assert.Contains("buy a fuel scoop", asked, StringComparison.Ordinal);
        Assert.True(Assert.Single(surface.ChecklistService.Document.Items).IsComplete);
    }

    [Fact]
    public async Task RemovingALineAsksAndYesTakesItOff()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.ChecklistService.AddNote(ChecklistScope.Universal, "buy a fuel scoop");
        var model = FakeLlmProvider.Answering("From the model.");
        var loop = Loop(surface, model);

        await Said(loop, "remove buy a fuel scoop");
        await Said(loop, "yes");

        Assert.Equal(0, model.CallCount);
        Assert.Empty(surface.ChecklistService.Document.Items);
    }

    [Theory]
    [InlineData("remove fuel scoop")]
    [InlineData("remove buy limpets")]
    public async Task WordsThatAreNotAWholeLineReachTheModel(string said)
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.ChecklistService.AddNote(ChecklistScope.Universal, "buy a fuel scoop");
        var model = FakeLlmProvider.Answering("From the model.");

        await Said(Loop(surface, model), said);

        Assert.Equal(1, model.CallCount);
    }

    private static TurnLoop Loop(TestSurface surface, FakeLlmProvider model) =>
        new(
            surface.Registry,
            new KeywordRouter(surface.Registry, () => ChecklistCapability.Phrases(surface.ChecklistService)),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            model,
            clock: new InstantClock());

    private static async Task<string> Said(TurnLoop loop, string said)
    {
        var text = new System.Text.StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(said, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
        }

        return text.ToString();
    }
}
