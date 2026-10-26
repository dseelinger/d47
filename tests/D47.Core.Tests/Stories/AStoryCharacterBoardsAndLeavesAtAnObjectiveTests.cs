using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A cast member with a span boards or leaves the ship at an objective of the chapter that plays the beat, placed by the
/// chapter writer, applied at the opening of the first chapter past a beat no chapter placed, and folded into the story
/// from the objectives that fired (#1609).
/// </summary>
public sealed class AStoryCharacterBoardsAndLeavesAtAnObjectiveTests
{
    private static readonly StorySpeaker Rider = new()
    {
        Id = "rider",
        Name = "Vale",
        Who = "A courier riding along, dry and patient.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bm_lewis",
        Link = 0.3,
        Aboard = [new("setUp", "midpoint")],
    };

    private static readonly StorySecret Riding = Secret with { Cast = [.. Secret.Cast, Rider] };

    private static readonly Story Picked = new() { Id = Id, Title = Card.Title, PublicLayer = "public" };

    private static readonly string[] ActOne = ["openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo"];

    private static Adventure Chapter(IReadOnlyList<string> aboard, int beats = 4) => new()
    {
        Key = "c",
        Name = "C",
        Aboard = aboard,
        Beats = [.. Enumerable.Range(0, beats).Select(at => new AdventureBeat { Title = $"B{at}", Line = "Go." })],
    };

    private static Adventure WithBeat(Adventure chapter, int index, Func<AdventureBeat, AdventureBeat> change) =>
        chapter with { Beats = [.. chapter.Beats.Select((beat, at) => at == index ? change(beat) : beat)] };

    [Fact]
    public void AMemberWhoBoardsAtAnObjectiveIsAboardFromItOn()
    {
        var chapter = WithBeat(Chapter([]), 2, beat => beat with { Boards = ["rider"] });

        Assert.All(new[] { -1, 0, 1 }, at => Assert.Empty(StoryAboard.At(chapter, at)));
        Assert.All(new[] { 2, 3 }, at => Assert.Equal(["rider"], StoryAboard.At(chapter, at)));
    }

    [Fact]
    public void AMemberWhoLeavesAtAnObjectiveIsAboardUntilIt()
    {
        var chapter = WithBeat(Chapter(["rider"]), 2, beat => beat with { Leaves = ["rider"] });

        Assert.All(new[] { -1, 0, 1 }, at => Assert.Equal(["rider"], StoryAboard.At(chapter, at)));
        Assert.All(new[] { 2, 3 }, at => Assert.Empty(StoryAboard.At(chapter, at)));
    }

    [Fact]
    public void AnOpeningSwitchHoldsFromTheOpeningOn()
    {
        var boarding = Chapter([]) with { OpeningBoards = ["rider"] };
        var leaving = Chapter(["rider"]) with { OpeningLeaves = ["rider"] };

        Assert.All(new[] { -1, 0, 3 }, at => Assert.Equal(["rider"], StoryAboard.At(boarding, at)));
        Assert.All(new[] { -1, 0, 3 }, at => Assert.Empty(StoryAboard.At(leaving, at)));
    }

    [Fact]
    public void AChapterWithNoSwitchesHasItsAboardEverywhere()
    {
        var chapter = Chapter(["rider", "dock-hand"]);

        Assert.All(Enumerable.Range(-1, 6), at => Assert.Equal(["rider", "dock-hand"], StoryAboard.At(chapter, at)));
    }

    [Fact]
    public void ABoardingIsDueOnlyAtAStageThatHandsOutItsBeatAndNotOnceItHappened()
    {
        Assert.Equal([new StorySwitch("rider", true, "setUp")], StoryAboard.Due(Riding, Picked, ActOne));
        Assert.Empty(StoryAboard.Due(Riding, Picked, ["breakIntoTwo", "bStory"]));
        Assert.Empty(StoryAboard.Due(Riding, Picked with { Boarded = ["rider"] }, ActOne));
    }

    [Fact]
    public void ALeavingIsDueForAMemberAboardBySpanOrBoardingInTheSameChapter()
    {
        var short1 = Riding with { Cast = [Rider with { Aboard = [new("breakIntoTwo", "midpoint")] }] };
        var always = Riding with { Cast = [Rider with { Aboard = [new(null, "midpoint")] }] };

        Assert.Equal(
            [new StorySwitch("rider", true, "breakIntoTwo"), new StorySwitch("rider", false, "midpoint")],
            StoryAboard.Due(short1, Picked, ["breakIntoTwo", "midpoint"]));
        Assert.Empty(StoryAboard.Due(short1, Picked, ["midpoint"]));
        Assert.Equal([new StorySwitch("rider", false, "midpoint")], StoryAboard.Due(always, Picked, ["midpoint"]));
        Assert.Empty(StoryAboard.Due(always, Picked with { Left = ["rider"] }, ["midpoint"]));
    }

    [Fact]
    public void AChapterPastADueBoardingOpensWithTheMemberAboardAndPastADueLeavingWithout()
    {
        var pastBoarding = StoryAboard.Missed(Picked, Riding, ["breakIntoTwo", "bStory"]);
        var pastLeaving = StoryAboard.Missed(Picked, Riding, ["badGuysCloseIn"]);

        Assert.Equal(["rider"], StoryAboard.BySpan(Riding, pastBoarding));
        Assert.Empty(StoryAboard.BySpan(Riding, pastLeaving));
        Assert.Equal([new StorySwitch("rider", true, "setUp"), new StorySwitch("rider", false, "midpoint")], pastLeaving.Switched);
        Assert.Same(Picked, StoryAboard.Missed(Picked, Riding, []));
        Assert.Empty(StoryAboard.BySpan(Riding, StoryAboard.Missed(Picked, Riding, ActOne)));
    }

    [Fact]
    public void ABeatEveryFinaleChapterIsHandedIsNeverAppliedAsMissed()
    {
        var finale = Riding with { Cast = [Rider with { Aboard = [new("breakIntoThree", null)] }] };

        Assert.Empty(StoryAboard.Missed(Picked, finale, ["finalImage"]).Switched);
    }

    [Fact]
    public void ABoardingOnAFiredObjectiveCarriesToTheNextChaptersOpening()
    {
        var chapter = WithBeat(Chapter([]), 1, beat => beat with { Boards = ["rider"] });
        var folded = StoryAboard.Folded(Picked, Riding, chapter, fired: 2);

        Assert.Equal(["rider"], StoryAboard.BySpan(Riding, StoryAboard.Missed(folded, Riding, ["breakIntoTwo", "bStory"])));
        Assert.Empty(StoryAboard.Due(Riding, folded, ActOne));
    }

    [Fact]
    public void ABoardingOnAnObjectiveThatNeverFiredIsStillDueOrAppliedAtTheOpening()
    {
        var chapter = WithBeat(Chapter([]), 1, beat => beat with { Boards = ["rider"] });
        var folded = StoryAboard.Folded(Picked, Riding, chapter, fired: 1);

        Assert.Empty(folded.Switched);
        Assert.Equal([new StorySwitch("rider", true, "setUp")], StoryAboard.Due(Riding, folded, ActOne));
        Assert.Equal(["rider"], StoryAboard.BySpan(Riding, StoryAboard.Missed(folded, Riding, ["breakIntoTwo", "bStory"])));
    }

    [Fact]
    public void OnlyDueSwitchesAreKeptAndALeaveAtOrBeforeTheBoardingIsDropped()
    {
        var due = new[] { new StorySwitch("rider", true, "setUp"), new StorySwitch("rider", false, "midpoint") };
        var chapter = Chapter([]) with { OpeningLeaves = ["rider"] };
        chapter = WithBeat(chapter, 0, beat => beat with { Leaves = ["rider"], Boards = ["dock-hand"] });
        chapter = WithBeat(chapter, 1, beat => beat with { Boards = ["RIDER"], Leaves = ["rider"] });
        chapter = WithBeat(chapter, 2, beat => beat with { Boards = ["rider"] });

        var placed = StoryAboard.Placed(chapter, due);

        Assert.Empty(placed.OpeningLeaves);
        Assert.Equal(["rider"], placed.Beats[1].Boards);
        Assert.All(placed.Beats, beat => Assert.Empty(beat.Leaves));
        Assert.All(placed.Beats.Where((_, at) => at != 1), beat => Assert.Empty(beat.Boards));

        var later = StoryAboard.Placed(WithBeat(placed, 3, beat => beat with { Leaves = ["rider"] }), due);
        Assert.Equal(["rider"], later.Beats[3].Leaves);
    }

    [Fact]
    public async Task AnAnswersSwitchIsReadOntoItsObjectiveThroughTheThenJoin()
    {
        const string answer = """
            {"opening": "Again.", "reply": "Here.", "openingBoards": [], "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back.",
               "boards": ["dock-hand"], "leaves": ["rider"], "then": []},
              {"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined.", "boards": ["RIDER"], "leaves": ["rider"],
               "then": [{"kind": "bounty", "count": 3, "boards": ["rider"]}]}
            ]}
            """;
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(answer), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(
                AdventureReach.Session,
                Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 2, 10, 5)
                {
                    Due = [new StorySwitch("rider", true, "setUp"), new StorySwitch("rider", false, "midpoint")],
                }),
            Now,
            CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var beats = outcome.Draft!.Beats;
        Assert.Equal(2, beats[1].Steps.Count);
        Assert.Equal(["rider"], beats[1].Boards);
        Assert.Empty(beats[0].Boards);
        Assert.All(beats, beat => Assert.Empty(beat.Leaves));
    }

    [Fact]
    public async Task ABoardingOnAFiredObjectiveIsHeardInTheCockpitAndOpensTheNextChapter()
    {
        var boarding = BeatsToTheBeacon.Replace("\"line\": \"Scoop here.\"}", "\"line\": \"Scoop here.\", \"boards\": [\"rider\"]}", StringComparison.Ordinal);
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(boarding),
                RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextSpine),
                RoundScriptedLlmProvider.Saying(NextBeats),
                RoundScriptedLlmProvider.Saying(NothingObjected)),
            Riding);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        var chapter = fixtures.Book.Store.Find("F1", first)!;
        Assert.Empty(chapter.Aboard!);
        Assert.Equal(["rider"], chapter.Beats[0].Boards);
        Assert.Contains("\"boards\": [\"rider\"]", fixtures.Provider.Requests[1].Prompt.History[0].Text, StringComparison.Ordinal);
        Assert.Contains("Vale (\"rider\") comes aboard at the setUp beat.", fixtures.Provider.Requests[1].Prompt.History[0].Text, StringComparison.Ordinal);

        var reopened = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, NullLogger<AdventureStore>.Instance);
        Assert.True(reopened.Poll());
        Assert.Equal(["rider"], reopened.Find("F1", first)!.Beats[0].Boards);

        Assert.Empty(fixtures.Director.CastAboard("F1"));
        Assert.Equal(0.3, fixtures.Director.LineVoice("F1", "rider")!.Pinned!.Link);
        Assert.Null(fixtures.Director.LineVoice("F1", "rider", 0)!.Pinned!.Link);

        fixtures.Book.Observe(AdventureFixtures.Jump(chapter.Beats[0].Trigger.SystemAddress!.Value, Now.AddMinutes(1)), "F1");

        Assert.Equal(["rider"], fixtures.Director.CastAboard("F1"));
        Assert.Null(fixtures.Director.LineVoice("F1", "rider")!.Pinned!.Link);
        Assert.Equal(0.3, fixtures.Director.LineVoice("F1", "rider", -1)!.Pinned!.Link);

        fixtures.Finish("F1", first, Now.AddMinutes(2));
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Chapters.Count);
        Assert.Equal([new StorySwitch("rider", true, "setUp", Chapter: first)], story.Switched);
        Assert.Null(story.Boarded);
        Assert.DoesNotContain("\"boarded\"", fixtures.Files.ReadText(fixtures.StoryPath)!, StringComparison.Ordinal);
        Assert.Equal(["rider"], fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.Aboard);
        Assert.Contains("Aboard the Commander's ship as this chapter opens: Vale.", fixtures.Provider.Requests[4].Prompt.History[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABoardingNoChapterPlacedIsAppliedAtTheOpeningOfTheFirstChapterPastIt()
    {
        var unspanned = Riding with { Cast = [.. Secret.Cast, Rider with { Aboard = [new("setUp", null)] }] };
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
                RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextSpine),
                RoundScriptedLlmProvider.Saying(NextBeats),
                RoundScriptedLlmProvider.Saying(NothingObjected)),
            unspanned);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        Assert.Equal(["rider"], Ids(Beat(fixtures.Provider.Requests[1].ResponseSchema!), "boards"));
        Assert.False(Beat(fixtures.Provider.Requests[1].ResponseSchema!).TryGetProperty("leaves", out _));

        fixtures.Finish("F1", first, Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Chapters.Count);
        Assert.Equal([new StorySwitch("rider", true, "setUp")], story.Switched);
        Assert.Equal(["rider"], fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.Aboard);
        Assert.False(Beat(fixtures.Provider.Requests[4].ResponseSchema!).TryGetProperty("boards", out _));
        Assert.DoesNotContain("openingBoards", fixtures.Provider.Requests[4].ResponseSchema!.SchemaJson, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAdventureFileAndAStoryFileWithoutTheFieldsLoadAsBefore()
    {
        var files = new MemoryFileSystem();
        var folder = Path.Combine(MemoryInstall.FakeRoot, "d47-switches", Guid.NewGuid().ToString("N"));
        var adventures = Path.Combine(folder, "adventures.json");
        var stories = Path.Combine(folder, "story.json");
        var plain = new Adventure
        {
            Key = "plain",
            Name = "Plain",
            Source = AdventureSource.Generated,
            Beats =
            [
                new AdventureBeat
                {
                    Title = "The Lantern",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
                    Line = "Scoop here.",
                },
            ],
        };

        Assert.Null(new AdventureStore(adventures, files, NullLogger<AdventureStore>.Instance).Save("F1", plain));
        StoryStore.Open(stories, files, NullLogger<StoryStore>.Instance).Save("F1", Picked with { Chapters = ["plain"] });

        var adventureText = files.ReadText(adventures)!;
        Assert.DoesNotContain("boards", adventureText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leaves", adventureText, StringComparison.OrdinalIgnoreCase);

        var storyJson = JsonNode.Parse(files.ReadText(stories)!)!;
        var saved = storyJson["commanders"]![0]!["stories"]![0]!.AsObject();
        saved.Remove("boarded");
        saved.Remove("left");
        files.WriteText(stories, storyJson.ToJsonString());

        var reopened = new AdventureStore(adventures, files, NullLogger<AdventureStore>.Instance);
        Assert.True(reopened.Poll());
        var loaded = reopened.Find("F1", "plain")!;
        Assert.Empty(loaded.OpeningBoards);
        Assert.Empty(loaded.OpeningLeaves);
        Assert.All(loaded.Beats, beat => Assert.Empty(beat.Boards));
        Assert.Equal(plain.Beats.Count, loaded.Beats.Count);

        var story = StoryStore.Open(stories, files, NullLogger<StoryStore>.Instance).Current("F1")!;
        Assert.Equal(["plain"], story.Chapters);
        Assert.Empty(story.Switched);
        Assert.Null(story.Boarded);
        Assert.Null(story.Left);
    }

    [Fact]
    public void ASavedStoryKeepsItsSwitchesThroughTheStore()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(MemoryInstall.FakeRoot, "d47-switches", Guid.NewGuid().ToString("N"), "story.json");
        IReadOnlyList<StorySwitch> switched = [new("rider", true, "setUp", Chapter: "plain"), new("rider", false, "midpoint")];

        StoryStore.Open(path, files, NullLogger<StoryStore>.Instance).Save("F1", Picked with { Switched = switched });

        Assert.Equal(switched, StoryStore.Open(path, files, NullLogger<StoryStore>.Instance).Current("F1")!.Switched);
        Assert.DoesNotContain("passed", files.ReadText(path)!, StringComparison.Ordinal);
    }

    private static JsonElement Beat(D47.Core.Conversation.LlmResponseSchema schema) =>
        JsonDocument.Parse(schema.SchemaJson).RootElement.GetProperty("properties").GetProperty("beats").GetProperty("items").GetProperty("properties");

    private static IEnumerable<string?> Ids(JsonElement beat, string name) =>
        beat.GetProperty(name).GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString());
}
