using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A line watching for a held quantity of a ship material ticks once and stays ticked (#2644).</summary>
public class AMaterialCountLineTicksItselfTests
{
    private static readonly DateTimeOffset Set = DateTimeOffset.Parse("2026-10-07T19:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void TheLineTicksAtTheQuantityAndNotBelowIt()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Have 21 Guardian Power Cells", Have("Guardian Power Cell", 21));

        Apply(game, checklists, Snapshot("2026-10-07T19:10:00Z", 20));

        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
        Assert.Equal(20, Find(checklists, line).Watch!.Progress);
        Assert.Empty(checklists.Drain());

        Apply(game, checklists, Collected("2026-10-07T19:20:00Z", 1));

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        Assert.Equal(
            "Ticked \"Have 21 Guardian Power Cells\": you held 21 Guardian Power Cell.",
            Assert.Single(checklists.Drain()).Text);
    }

    [Fact]
    public void TheLineDoesNotTickBeforeASnapshotHasArrived()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Have 2 Guardian Power Cells", Have("Guardian Power Cell", 2));

        Apply(game, checklists, Collected("2026-10-07T19:10:00Z", 5));

        Assert.False(game.Active!.Materials.SnapshotSeen);
        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
    }

    [Fact]
    public void AConditionAlreadyMetTicksOnTheNextTickWithNoMaterialEvent()
    {
        var (game, checklists) = Started(new MemoryInstall());
        game.Apply(Snapshot("2026-10-07T18:30:00Z", 30));
        var line = Save(checklists, "Have 21 Guardian Power Cells", Have("Guardian Power Cell", 21));

        checklists.Poll(announce: false);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
    }

    [Fact]
    public void SpendingAfterwardsLeavesTheLineTicked()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Have 21 Guardian Power Cells", Have("Guardian Power Cell", 21));

        Apply(game, checklists, Snapshot("2026-10-07T19:10:00Z", 21));
        Apply(game, checklists, Event(
            """{"timestamp":"2026-10-07T19:20:00Z","event":"MaterialDiscarded","Category":"Manufactured","Name":"guardian_powercell","Count":21}"""));

        Assert.Equal(0, game.Active!.Materials.CountOf("guardian_powercell"));
        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
    }

    [Fact]
    public void TheLineSaysHowFarItHasGot()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Have 21 Guardian Power Cells", Have("Guardian Power Cell", 21));

        Apply(game, checklists, Snapshot("2026-10-07T19:10:00Z", 14));

        Assert.Equal(
            "Ticks when you hold 21 Guardian Power Cell · 14 of 21 so far",
            ChecklistWatching.Describe(Find(checklists, line).Watch!));
    }

    [Theory]
    [InlineData("HN Shock Mount", 5, "buy watch")]
    [InlineData("Guardian Power Cell", null, "how many")]
    [InlineData("No Such Thing Anywhere", 5, "not a ship material")]
    public void AHoldingThatCannotMeanAnythingIsRefused(string subject, int? quantity, string expected)
    {
        var watch = new ChecklistWatch(ChecklistWatchKind.Have, subject) { Quantity = quantity, Since = Set };

        Assert.Contains(
            expected,
            ChecklistValidation.WatchProblem("The line", ChecklistItemKind.Authored, watch) ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AHoldingWithAPlaceIsRefusedAndAShipMaterialBySymbolIsKept()
    {
        var placed = Have("Guardian Power Cell", 3) with { Place = "Sol" };

        Assert.Contains("takes none", ChecklistValidation.WatchProblem("The line", ChecklistItemKind.Authored, placed), StringComparison.Ordinal);
        Assert.Null(ChecklistValidation.WatchProblem("The line", ChecklistItemKind.Authored, Have("guardian_powercell", 3)));
    }

    private static ChecklistWatch Have(string subject, int quantity) =>
        new(ChecklistWatchKind.Have, subject) { Quantity = quantity, Since = Set };

    private static void Apply(GameStateStore game, ChecklistService checklists, JournalEvent journalEvent)
    {
        game.Apply(journalEvent);
        checklists.Poll(events: [journalEvent]);
    }

    private static JournalEvent Snapshot(string when, int cells) => Event(
        $$"""
        {"timestamp":"{{when}}","event":"Materials","Raw":[],"Encoded":[],
         "Manufactured":[{"Name":"guardian_powercell","Name_Localised":"Guardian Power Cell","Count":{{cells}}}]}
        """);

    private static JournalEvent Collected(string when, int count) => Event(
        $$"""
        {"timestamp":"{{when}}","event":"MaterialCollected","Category":"Manufactured","Name":"guardian_powercell",
         "Name_Localised":"Guardian Power Cell","Count":{{count}}}
        """);

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
