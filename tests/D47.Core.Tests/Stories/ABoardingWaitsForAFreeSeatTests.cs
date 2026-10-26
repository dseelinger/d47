using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member's boarding is handed to a chapter only while the ship has a seat for them, and held until it does (#1761).</summary>
public sealed class ABoardingWaitsForAFreeSeatTests
{
    private const string Sidewinder = "Sidewinder";
    private const string Cobra = "CobraMkIII";
    private const string Krait = "Krait_MkII";

    private static StorySpeaker Member(string id, string boards, string? leaves = null) => new()
    {
        Id = id,
        Name = id,
        Who = "A passenger.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bm_lewis",
        Aboard = new(boards, leaves),
    };

    private static StorySecret Cast(params StorySpeaker[] members) => Secret with { Cast = [.. Secret.Cast, .. members] };

    private static readonly Story Picked = new() { Id = Id, Title = Card.Title, PublicLayer = "public" };

    private static readonly string[] ActOne = ["openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo"];

    private static readonly string[] Later = ["breakIntoTwo", "bStory"];

    private static readonly StorySecret Rider = Cast(Member("rider", "setUp"));

    [Fact]
    public void ABoardingWhoseBeatIsHandedWhileFlyingASidewinderIsNotDue()
    {
        Assert.Empty(StoryAboard.Due(Rider, Picked, ActOne, Sidewinder));
    }

    [Fact]
    public void ABoardingPastItsBeatInASidewinderIsHeldNotBoarded()
    {
        var story = StoryAboard.Missed(Picked, Rider, Later, Sidewinder);

        Assert.Empty(story.Boarded);
        Assert.Equal(["rider"], story.AwaitingSeat);
        Assert.Empty(StoryAboard.BySpan(Rider, story));
    }

    [Fact]
    public void AHeldBoardingIsDueInACobraAndIsNotBoardedWithoutAScene()
    {
        var held = Picked with { AwaitingSeat = ["rider"] };

        Assert.Equal([new StorySwitch("rider", true, "setUp", true)], StoryAboard.Due(Rider, held, Later, Cobra));

        var missed = StoryAboard.Missed(held, Rider, Later, Cobra);
        Assert.Empty(missed.Boarded);
        Assert.Equal(["rider"], missed.AwaitingSeat);
    }

    [Fact]
    public void ABoardingWhoseBeatIsHandedIsNotMarkedPassed()
    {
        Assert.Equal([new StorySwitch("rider", true, "setUp")], StoryAboard.Due(Rider, Picked, ActOne, Cobra));
        Assert.Equal([new StorySwitch("rider", true, "setUp")], StoryAboard.Due(Rider, Picked with { AwaitingSeat = ["rider"] }, ActOne, Cobra));
    }

    [Fact]
    public void ASecondBoardingInACobraWithOneAboardIsNotDueAndIsHeldPastItsBeat()
    {
        var two = Cast(Member("rider", "setUp"), Member("pilot", "catalyst"));
        var aboard = Picked with { Boarded = ["rider"] };

        Assert.Empty(StoryAboard.Due(two, aboard, ActOne, Cobra));

        var story = StoryAboard.Missed(aboard, two, ["debate", "breakIntoTwo"], Cobra);
        Assert.Equal(["rider"], story.Boarded);
        Assert.Equal(["pilot"], story.AwaitingSeat);
    }

    [Fact]
    public void TwoBoardingsFitAKraitWithNoneAboardAndOnlyTheEarlierFitsWithOne()
    {
        var two = Cast(Member("rider", "setUp"), Member("pilot", "catalyst"));

        Assert.Equal(["rider", "pilot"], StoryAboard.Due(two, Picked, ActOne, Krait).Select(entry => entry.Id));

        var three = Cast(Member("rider", "setUp"), Member("pilot", "catalyst"), Member("medic", "debate"));

        Assert.Equal(["pilot"], StoryAboard.Due(three, Picked with { Boarded = ["rider"] }, ActOne, Krait).Select(entry => entry.Id));
    }

    [Fact]
    public void HeldBoardingsComeBeforeThoseWhoseBeatIsHanded()
    {
        var two = Cast(Member("rider", "setUp"), Member("pilot", "catalyst"));
        var held = Picked with { AwaitingSeat = ["pilot"] };

        Assert.Equal(["pilot"], StoryAboard.Due(two, held, ActOne, Cobra).Select(entry => entry.Id));
        Assert.Equal(["pilot", "rider"], StoryAboard.Due(two, held, ActOne, Krait).Select(entry => entry.Id));
    }

    [Fact]
    public void AnUnknownHullHoldsNothing()
    {
        Assert.Single(StoryAboard.Due(Rider, Picked, ActOne));
        Assert.Single(StoryAboard.Due(Rider, Picked, ActOne, null));
        Assert.Single(StoryAboard.Due(Rider, Picked, ActOne, "NoSuchHull"));
        Assert.Equal(["rider"], StoryAboard.Missed(Picked, Rider, Later, "NoSuchHull").Boarded);
        Assert.Empty(StoryAboard.Missed(Picked, Rider, Later, "NoSuchHull").AwaitingSeat);
    }

    [Fact]
    public void AHeldMemberWhoseLeavingHasPassedIsNotDue()
    {
        var leaving = Cast(Member("rider", "setUp", "midpoint"));

        Assert.Empty(StoryAboard.Due(leaving, Picked with { AwaitingSeat = ["rider"], Left = ["rider"] }, Later, Cobra));
    }

    [Fact]
    public void FoldingInAHeldBoardingTakesTheMemberOffTheList()
    {
        var chapter = new D47.Core.Adventures.Adventure
        {
            Key = "c",
            Name = "C",
            OpeningBoards = ["rider"],
            Beats = [new D47.Core.Adventures.AdventureBeat { Title = "B0", Line = "Go." }],
        };

        var folded = StoryAboard.Folded(Picked with { AwaitingSeat = ["rider"] }, chapter, fired: 0);

        Assert.Equal(["rider"], folded.Boarded);
        Assert.Empty(folded.AwaitingSeat);
    }

    [Fact]
    public void AStoryWithoutSpansIsUnchangedByTheHull()
    {
        Assert.Empty(StoryAboard.Due(Secret, Picked, ActOne, Sidewinder));
        Assert.Same(Picked, StoryAboard.Missed(Picked, Secret, [], Sidewinder));
        Assert.Equal(Picked.AwaitingSeat, StoryAboard.Missed(Picked, Secret, Later, Sidewinder).AwaitingSeat);
    }

    [Fact]
    public async Task AChapterDrawnInASidewinderHoldsTheBoardingAndAsksForNone()
    {
        var unspanned = Cast(Member("rider", "setUp"));
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
                RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextSpine),
                RoundScriptedLlmProvider.Saying(NextBeats),
                RoundScriptedLlmProvider.Saying(NothingObjected)),
            unspanned);
        fixtures.Director.Game = () => Loaded(0);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        fixtures.Finish("F1", first, Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Chapters.Count);
        Assert.Empty(story.Boarded);
        Assert.Equal(["rider"], story.AwaitingSeat);
        Assert.All(fixtures.Asks, ask => Assert.Empty(ask.Story!.Due!));
    }

    [Fact]
    public void ASavedStoryWithoutAwaitingSeatReadsAsAnEmptyList()
    {
        var read = System.Text.Json.JsonSerializer.Deserialize<Story>("""{"id":"x","title":"T","publicLayer":"p"}""", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.NotNull(read);
        Assert.Empty(read.AwaitingSeat);
    }
}
