using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A line watching for a tech broker unlock ticks once, and proposing it brings its cost lines (#2645).</summary>
public class ATechBrokerUnlockLineTicksItselfTests
{
    private const string Booster = "Guardian FSD Booster";

    private static readonly DateTimeOffset Set = DateTimeOffset.Parse("2026-10-07T19:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void TheLineTicksOnTheBrokerEventForItsRowAndNotAnotherRows()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Unlock Guardian FSD Booster", Unlock(Booster));

        Apply(game, checklists, Broker("2026-10-07T19:10:00Z", "Hpt_PlasmaShockCannon_Gimbal_Large"));
        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
        Assert.Empty(checklists.Drain());

        Apply(game, checklists, Broker("2026-10-07T19:20:00Z", "Int_GuardianFSDBooster_Size3"));

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        Assert.Equal(
            "Ticked \"Unlock Guardian FSD Booster\": you unlocked Guardian FSD Booster.",
            Assert.Single(checklists.Drain()).Text);
    }

    [Fact]
    public void ARowAlreadyKnownUnlockedWhenTheWatchIsAcceptedTicksAtOnce()
    {
        var (game, checklists) = Started(new MemoryInstall());
        game.Apply(Broker("2026-10-07T18:30:00Z", "Int_GuardianFSDBooster_Size1"));
        var line = Save(checklists, "Unlock Guardian FSD Booster", Unlock(Booster));

        checklists.Poll(announce: false);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
    }

    [Fact]
    public void ARowNotKnownUnlockedStaysOpen()
    {
        var (_, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Unlock Guardian FSD Booster", Unlock(Booster));

        checklists.Poll(announce: false);

        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
    }

    [Theory]
    [InlineData("No Such Module", null, null, "not a tech broker unlock")]
    [InlineData(Booster, 3, null, "neither")]
    [InlineData(Booster, null, "Sol", "neither")]
    public void AnUnlockThatCannotMeanAnythingIsRefused(string subject, int? quantity, string? place, string expected)
    {
        var watch = new ChecklistWatch(ChecklistWatchKind.Unlock, subject) { Quantity = quantity, Place = place, Since = Set };

        Assert.Contains(
            expected,
            ChecklistValidation.WatchProblem("The line", ChecklistItemKind.Authored, watch) ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProposingTheBoosterBringsItsFiveCostLinesWithTheirWatches()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var checklists = surface.ChecklistService;

        var result = await Call(surface, "Unlock Guardian FSD Booster");

        Assert.False(result.IsError);
        var waiting = Assert.Single(checklists.Proposals.Pending);
        Assert.Equal("Add \"Unlock Guardian FSD Booster\" and 5 cost lines to the custom list", waiting.Summary);

        checklists.Accept();

        var lines = checklists.Document.Items.ToDictionary(item => item.Text, item => item.Watch!);
        Assert.Equal(6, lines.Count);
        Assert.Equal(ChecklistWatchKind.Unlock, lines["Unlock Guardian FSD Booster"].Kind);
        AssertCost(lines, "Have 1 Guardian Module Blueprint Segment", ChecklistWatchKind.Have, 1);
        AssertCost(lines, "Have 21 Guardian Power Cell", ChecklistWatchKind.Have, 21);
        AssertCost(lines, "Have 21 Guardian Technology Component", ChecklistWatchKind.Have, 21);
        AssertCost(lines, "Have 24 Focus Crystals", ChecklistWatchKind.Have, 24);
        AssertCost(lines, "Buy 8 HN Shock Mount", ChecklistWatchKind.Buy, 8);
        Assert.Null(lines["Buy 8 HN Shock Mount"].Place);
    }

    [Fact]
    public async Task ACostLineAlreadyOnTheListIsNotAddedTwice()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var checklists = surface.ChecklistService;
        checklists.AddNote(ChecklistScope.Universal, "Have 21 Guardian Power Cell");

        await Call(surface, "Unlock Guardian FSD Booster");
        checklists.Accept();

        Assert.Equal(6, checklists.Document.Items.Count);
        Assert.Single(checklists.Document.Items, item => item.Text == "Have 21 Guardian Power Cell");
    }

    [Fact]
    public async Task ADeclinedUnlockProposalLeavesNothing()
    {
        var surface = TestSurface.For(new MemoryInstall());

        await Call(surface, "Unlock Guardian FSD Booster");
        surface.ChecklistService.Decline();

        Assert.Empty(surface.ChecklistService.Document.Items);
    }

    private static void AssertCost(
        Dictionary<string, ChecklistWatch> lines, string text, ChecklistWatchKind kind, int quantity)
    {
        Assert.Equal(kind, lines[text].Kind);
        Assert.Equal(quantity, lines[text].Quantity);
    }

    private static Task<ToolResult> Call(TestSurface surface, string text) =>
        surface.Registry.InvokeAsync(
            "add_to_checklist",
            new ToolArguments(new Dictionary<string, string>
            {
                ["text"] = text,
                ["tick_when"] = "unlock",
                ["subject"] = Booster,
            }),
            TestContext.Current.CancellationToken);

    private static ChecklistWatch Unlock(string subject) =>
        new(ChecklistWatchKind.Unlock, subject) { Since = Set };

    private static void Apply(GameStateStore game, ChecklistService checklists, JournalEvent journalEvent)
    {
        game.Apply(journalEvent);
        checklists.Poll(events: [journalEvent]);
    }

    private static JournalEvent Broker(string when, string symbol) => Event(
        $$"""{"timestamp":"{{when}}","event":"TechnologyBroker","BrokerType":"guardian","ItemsUnlocked":[{"Name":"{{symbol}}"}]}""");

    private static GameStateStore Commander()
    {
        var game = new GameStateStore();
        game.Apply(Event("""{"timestamp":"2026-10-07T18:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        return game;
    }

    private static (GameStateStore Game, ChecklistService Checklists) Started(MemoryInstall install)
    {
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game);
        checklists.Poll(announce: false);
        return (game, checklists);
    }

    private static ChecklistItem Save(ChecklistService checklists, string text, ChecklistWatch watch)
    {
        var item = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = text,
            Watch = watch,
        };

        checklists.List.Save([ChecklistDocument.For("F1", "Jameson") with { Items = [item] }]);
        return item;
    }

    private static ChecklistItem Find(ChecklistService checklists, ChecklistItem item) =>
        Assert.Single(checklists.Document.Items, candidate => candidate.Id.Same(item.Id));

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
