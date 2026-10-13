using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// The recap is built from the objectives fired in every chapter, the clues given and where the story waits; never an
/// objective still to come, the current objective's line, or the hidden layer beyond the clues given.
/// </summary>
public sealed class ARecapSaysOnlyWhatHasHappenedTests
{
    private const string ChapterKey = "the-hard-way";

    private static readonly DateTimeOffset PickedAt = Now.AddDays(-30);

    private static AdventureTrigger Counted(TriggerKind kind, int count) => new() { Kind = kind, Count = count };

    /// <summary>Chapter one, archived with both its objectives fired.</summary>
    private static Adventure ChapterOne() => new()
    {
        Key = "chapter-one",
        Name = "The First Light",
        Source = AdventureSource.Generated,
        Written = PickedAt,
        AcceptedAt = PickedAt,
        StoryId = Id,
        Beats =
        [
            Beat("The Song", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "A song came over the comms."),
            Beat("The Beacon", "resolution", new AdventureTrigger { Kind = TriggerKind.Beacon }, "The beacon answered the song."),
        ],
    };

    /// <summary>Chapter two: an arrival, two bounties, three kill bonds and an arrival.</summary>
    private static Adventure ChapterTwo() => new()
    {
        Key = ChapterKey,
        Name = "The Hard Way",
        Source = AdventureSource.Generated,
        Written = Now,
        Opening = "Go.",
        StoryId = Id,
        Beats =
        [
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("First Blood", "catalyst", Counted(TriggerKind.Bounty, 1), "One."),
            Beat("Second Blood", "debate", Counted(TriggerKind.Bounty, 1), "Two."),
            Beat("The Wing", "midpoint", Counted(TriggerKind.Bond, 3), "Three bonds."),
            Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "The last line."),
        ],
    };

    /// <summary>A story on chapter two, two clues given, chapter one archived and three of chapter two's objectives fired.</summary>
    private static StoryFixtures OnChapterTwo()
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = PickedAt,
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", ChapterKey],
            CluesGiven = 2,
        });

        fixtures.Director.Archive.Append(new ArchivedChapter(
            "F1", Id, PickedAt, ChapterOne(), [PickedAt.AddDays(1), PickedAt.AddDays(2)], ["FSDJump", "DataScanned"]));

        Assert.Null(fixtures.Book.Write("F1", ChapterTwo()));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));

        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        fixtures.Book.Observe(Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(3))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");

        return fixtures;
    }

    private static string Prompt(RecapAsk ask) => string.Join("\n", ask.Speaker, ask.AboutMe, ask.Instruction);

    [Fact]
    public void ThePromptHoldsTheFiredObjectivesOfEveryChapterAndNoOther()
    {
        using var fixtures = OnChapterTwo();

        var prompt = Prompt(StoryRecap.Ask(fixtures.Director.Recap("F1")!, "Commander Test."));

        Assert.Contains("A song came over the comms.", prompt);
        Assert.Contains("The beacon answered the song.", prompt);
        Assert.Contains("Scoop here.", prompt);
        Assert.Contains("Two.", prompt);
        Assert.DoesNotContain("Three bonds.", prompt);
        Assert.DoesNotContain("The Wing", prompt);
        Assert.DoesNotContain("The last line.", prompt);
        Assert.Contains(Card.InYourWords, prompt);
    }

    [Fact]
    public void ThePromptNamesWhereTheStoryWaitsAsThePlaceAndTheAct()
    {
        using var fixtures = OnChapterTwo();
        var waiting = fixtures.Book.Standing("F1", ChapterKey)!.CurrentBeat!.Trigger;

        var prompt = Prompt(StoryRecap.Ask(fixtures.Director.Recap("F1")!, "Commander Test."));

        Assert.Contains($"Where it waits now: Next: {waiting.Describe()}.", prompt);
    }

    [Fact]
    public void ThePromptHoldsTheCluesGivenAndNothingElseOfTheHiddenLayer()
    {
        using var fixtures = OnChapterTwo();

        var prompt = Prompt(StoryRecap.Ask(fixtures.Director.Recap("F1")!, "Commander Test."));

        Assert.Contains(Secret.Clues[0].Text, prompt);
        Assert.Contains(Secret.Clues[1].Text, prompt);
        Assert.DoesNotContain(Secret.Clues[2].Text, prompt);
        Assert.DoesNotContain(Secret.Secret, prompt);
        Assert.DoesNotContain(Secret.End, prompt);
    }

    [Fact]
    public void WithoutAModelTheLastFiredLineAndTheHandOffAreSaid()
    {
        using var fixtures = OnChapterTwo();
        var standing = fixtures.Book.Standing("F1", ChapterKey)!;
        var facts = fixtures.Director.Recap("F1")!;

        Assert.Equal($"Two. {StoryRecap.HandOff(standing.CurrentBeat!.Trigger, standing.Counted)}", StoryRecap.Sayable(null, facts));
        Assert.StartsWith("Two. Next: ", StoryRecap.Sayable(null, facts), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecapInTheFirstPersonIsNotSpoken()
    {
        using var fixtures = OnChapterTwo();
        var facts = fixtures.Director.Recap("F1")!;

        Assert.Equal(StoryRecap.Fixed(facts), StoryRecap.Sayable("I watched Commander Test hunt two bounties.", facts));
        Assert.Equal(StoryRecap.Fixed(facts), StoryRecap.Sayable("We followed the song to the beacon.", facts));
        Assert.Equal(
            "Commander Test followed the song to the beacon.",
            StoryRecap.Sayable(" Commander Test followed the song to the beacon. ", facts));
    }

    [Fact]
    public void APausedStoryIsStillRecapped()
    {
        using var fixtures = OnChapterTwo();
        fixtures.Stories.Update("F1", Id, story => story.Paused(Now));

        var facts = fixtures.Director.Recap("F1")!;

        Assert.True(facts.Paused);
        Assert.Contains("The Commander has paused it.", StoryRecap.Ask(facts, null).Instruction);
    }

    [Fact]
    public void WithNoStoryThereIsNoRecap()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        Assert.Null(fixtures.Director.Recap("F1"));
    }

    /// <summary>A short story of two arrivals, the first saying <paramref name="arrival"/>.</summary>
    private static Adventure ShortStory(string key, string arrival) => new()
    {
        Key = key,
        Name = key,
        Source = AdventureSource.Generated,
        Written = Now,
        Spine = new AdventureSpine { Premise = $"The premise of {key}.", Turn = "The turn of it." },
        Beats =
        [
            Beat("There", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, arrival),
            Beat("Docked", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "Never said."),
        ],
    };

    [Fact]
    public void WithoutAStockStoryTheShortStoryWhoseObjectiveFiredLastIsRecapped()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        Assert.Null(fixtures.Book.Write("F1", ShortStory("older", "Arrived first.")));
        Assert.Null(fixtures.Book.Begin("F1", "older", Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");

        Assert.Null(fixtures.Book.Write("F1", ShortStory("newer", "Arrived second.")));
        Assert.Null(fixtures.Book.Begin("F1", "newer", Now.AddMinutes(2)));
        fixtures.Book.Observe(Jump(1, Now.AddMinutes(3)), "F1");
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(4)), "F1");

        var facts = fixtures.Director.Recap("F1")!;
        var prompt = StoryRecap.Ask(facts, null).Instruction;

        Assert.Equal("newer", facts.Title);
        Assert.Contains("Premise: The premise of newer.", prompt);
        Assert.Contains("Arrived second.", prompt);
        Assert.DoesNotContain("Never said.", prompt);
        Assert.DoesNotContain("The turn of it.", prompt);
        Assert.Equal("older", fixtures.Director.Recap("F1", "older")!.Title);
    }
}
