using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using D47.Core.Tests.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A dock objective at a station with smaller pads than the ship aboard needs is said once per ship.</summary>
public sealed class AnObjectiveTheShipCannotDockAtIsSaidTests
{
    private const string ChapterKey = "the-anchorage";

    private static AdventureTrigger DockAt(PadSize? largest) => new()
    {
        Kind = TriggerKind.Dock,
        MarketId = Anchorage,
        System = "Dyson's Hollow",
        Station = "Ritter Hub",
        LargestPad = largest,
    };

    private static StoryFixtures ChapterDockingAt(PadSize? largest)
    {
        var fixtures = StoryFixtures.ScriptedWith();

        fixtures.SaveRunning([ChapterKey]);
        Assert.Null(fixtures.Book.Write("F1", StoryFixtures.GeneratedChapter(ChapterKey, "The Anchorage", Id, DebtSpine,
            Beat("The Hub", "catalyst", DockAt(largest), "To one name."))));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        return fixtures;
    }

    [Fact]
    public void ALargeShipIsToldOnceAcrossManyTicks()
    {
        var said = new List<string>();
        using var fixtures = ChapterDockingAt(PadSize.Medium);
        fixtures.Director.Says += said.Add;

        for (var tick = 0; tick < 5; tick++)
        {
            fixtures.Director.WarnPadMismatch("F1", "type9");
        }

        Assert.Equal(
            ["Ritter Hub has medium pads at most, and your Type-9 Heavy needs a large one. Swap to a ship that fits, or say \"never ask this\" for a different objective."],
            said);
    }

    [Fact]
    public void AShipThatFitsSaysNothingAndSwappingBackDoesNotRepeatTheLine()
    {
        var said = new List<string>();
        using var fixtures = ChapterDockingAt(PadSize.Medium);
        fixtures.Director.Says += said.Add;

        fixtures.Director.WarnPadMismatch("F1", "type9");
        fixtures.Director.WarnPadMismatch("F1", "python");
        fixtures.Director.WarnPadMismatch("F1", "type9");

        Assert.Single(said);
    }

    [Fact]
    public void ADifferentTooLargeShipIsToldAgain()
    {
        var said = new List<string>();
        using var fixtures = ChapterDockingAt(PadSize.Small);
        fixtures.Director.Says += said.Add;

        fixtures.Director.WarnPadMismatch("F1", "python");
        fixtures.Director.WarnPadMismatch("F1", "type9");

        Assert.Equal(2, said.Count);
    }

    [Fact]
    public void ATriggerWithNoKnownPadSaysNothing()
    {
        var said = new List<string>();
        using var fixtures = ChapterDockingAt(null);
        fixtures.Director.Says += said.Add;

        fixtures.Director.WarnPadMismatch("F1", "type9");

        Assert.Empty(said);
    }

    [Fact]
    public void AShortStoryPointsToItsOwnPage()
    {
        var said = new List<string>();
        using var fixtures = StoryFixtures.ScriptedWith();
        var adventure = LanternRoute() with { Beats = [Beat("The Hub", "catalyst", DockAt(PadSize.Medium), "To one name.")] };

        Assert.Null(fixtures.Book.Write("F1", adventure));
        Assert.Null(fixtures.Book.Begin("F1", adventure.Key, Now));
        fixtures.Director.Says += said.Add;

        fixtures.Director.WarnPadMismatch("F1", "type9");

        Assert.EndsWith("or use Never ask this on its page.", Assert.Single(said));
    }

    [Fact]
    public void ASavedTriggerStillEqualsItselfAfterAReloadAndAnOldOneHasNoPad()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(MemoryInstall.FakeRoot, "pad-warning", "adventures.json");
        var adventure = LanternRoute() with { Beats = [Beat("The Hub", "catalyst", DockAt(PadSize.Medium), "To one name.")] };

        Assert.Null(new AdventureBook(new AdventureStore(path, files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance).Write("F1", adventure));

        var reread = new AdventureStore(path, files, NullLogger<AdventureStore>.Instance);
        reread.Poll();

        var stored = reread.Find("F1", adventure.Key)!;
        Assert.Equal(adventure.Beats, stored.Beats);
        Assert.Equal(PadSize.Medium, stored.Beats[0].Trigger.LargestPad);
        Assert.Null(AdventureTriggerWithoutAPad().LargestPad);
    }

    private static AdventureTrigger AdventureTriggerWithoutAPad() => DockAt(null);
}
