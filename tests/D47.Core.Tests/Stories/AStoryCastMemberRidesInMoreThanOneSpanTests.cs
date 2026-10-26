using System.IO.Compression;
using System.Text;
using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member's <c>aboard</c> may be a list of spans: they board, leave and board again, one switch at a time (#1759).</summary>
public sealed class AStoryCastMemberRidesInMoreThanOneSpanTests
{
    private static StorySpeaker Member(string id, params StorySpeakerAboard[] spans) => new()
    {
        Id = id,
        Name = id,
        Who = "A passenger.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bm_lewis",
        Aboard = spans,
    };

    private static readonly StorySecret Twice = Secret with
    {
        Cast = [.. Secret.Cast, Member("rider", new("funAndGames", "midpoint"), new("badGuysCloseIn", null))],
    };

    private static readonly Story Picked = new() { Id = Id, Title = Card.Title, PublicLayer = "public" };

    private static readonly StorySwitch FirstBoarding = new("rider", true, "funAndGames");

    private static readonly StorySwitch FirstLeaving = new("rider", false, "midpoint");

    private static readonly StorySwitch SecondBoarding = new("rider", true, "badGuysCloseIn");

    private static Adventure Chapter(string key, IReadOnlyList<string>? boards = null, IReadOnlyList<string>? leaves = null, int leavesAt = 0) => new()
    {
        Key = key,
        Name = key,
        Beats =
        [
            .. Enumerable.Range(0, 3).Select(at => new AdventureBeat
            {
                Title = $"B{at}",
                Line = "Go.",
                Boards = at == 0 ? boards ?? [] : [],
                Leaves = at == leavesAt ? leaves ?? [] : [],
            }),
        ],
    };

    [Fact]
    public void AMemberBoardsLeavesAndBoardsAgainAndIsAboardAtTheFinale()
    {
        var story = Picked;
        Assert.Empty(StoryAboard.BySpan(Twice, story));
        Assert.Equal([FirstBoarding], StoryAboard.Due(Twice, story, ["funAndGames"]));

        story = StoryAboard.Folded(story, Twice, Chapter("one", boards: ["rider"]), fired: 1);
        Assert.Equal(["rider"], StoryAboard.BySpan(Twice, story));
        Assert.Equal([FirstLeaving], StoryAboard.Due(Twice, story, ["midpoint"]));

        story = StoryAboard.Folded(story, Twice, Chapter("two", leaves: ["rider"]), fired: 1);
        Assert.Empty(StoryAboard.BySpan(Twice, story));
        Assert.Equal([SecondBoarding], StoryAboard.Due(Twice, story, ["badGuysCloseIn"]));

        story = StoryAboard.Folded(story, Twice, Chapter("three", boards: ["rider"]), fired: 1);
        Assert.Equal(["rider"], StoryAboard.BySpan(Twice, StoryAboard.Missed(story, Twice, ["finale"])));
        Assert.Equal(
            [FirstBoarding with { Chapter = "one" }, FirstLeaving with { Chapter = "two" }, SecondBoarding with { Chapter = "three" }],
            story.Switched);
    }

    [Fact]
    public void ThreeMembersWhoseLastSpansOpenBeforeTheFinaleAreAllAboardAtIt()
    {
        var three = Secret with
        {
            Cast =
            [
                Member("first", new("setUp", "catalyst"), new("midpoint", null)),
                Member("second", new(null, "debate"), new("badGuysCloseIn", null)),
                Member("third", new("themeStated", "bStory"), new("funAndGames", "allIsLost"), new("darkNightOfTheSoul", null)),
            ],
        };
        var finale = StoryClues.BeatKeys(Picked.Pacing, StoryStage.Finale, true, 1);

        Assert.Equal(["first", "second", "third"], StoryAboard.BySpan(three, StoryAboard.Missed(Picked, three, finale)));
        Assert.Empty(StoryAboard.Due(three, StoryAboard.Missed(Picked, three, finale), finale));
    }

    [Fact]
    public void ASecondBoardingIsDueOnlyOnceTheFirstLeavingHasHappened()
    {
        var aboard = Picked with { Switched = [FirstBoarding] };

        Assert.Empty(StoryAboard.Due(Twice, Picked, ["badGuysCloseIn"]));
        Assert.Empty(StoryAboard.Due(Twice, aboard, ["badGuysCloseIn"]));
        Assert.Equal([FirstLeaving], StoryAboard.Due(Twice, aboard, ["midpoint", "badGuysCloseIn"]));
        Assert.Equal([SecondBoarding], StoryAboard.Due(Twice, aboard with { Switched = [FirstBoarding, FirstLeaving] }, ["badGuysCloseIn"]));
    }

    [Fact]
    public void AStorySavedWithBoardedAndLeftCarriesOverAsTheFirstSpan()
    {
        var boarded = Read("""{"id":"x","title":"T","publicLayer":"p","boarded":["rider"]}""");
        var left = Read("""{"id":"x","title":"T","publicLayer":"p","boarded":["rider"],"left":["rider"]}""");

        Assert.Empty(boarded.Switched);
        Assert.Equal(["rider"], StoryAboard.BySpan(Twice, boarded));
        Assert.Equal([FirstBoarding], StoryAboard.CarriedOver(boarded, Twice).Switched);
        Assert.Null(StoryAboard.CarriedOver(boarded, Twice).Boarded);

        Assert.Empty(StoryAboard.BySpan(Twice, left));
        Assert.Equal([SecondBoarding], StoryAboard.Due(Twice, left, ["badGuysCloseIn"]));
        Assert.Equal([FirstBoarding, FirstLeaving], StoryAboard.CarriedOver(left, Twice).Switched);
    }

    [Fact]
    public void FoldingTheSameChapterTwiceRecordsNothingNew()
    {
        var both = Chapter("one", boards: ["rider"], leaves: ["rider"], leavesAt: 2);

        var once = StoryAboard.Folded(Picked, Twice, both, fired: 3);
        var twice = StoryAboard.Folded(once, Twice, both, fired: 3);

        Assert.Equal([FirstBoarding with { Chapter = "one" }, FirstLeaving with { Chapter = "one" }], once.Switched);
        Assert.Equal(once.Switched, twice.Switched);
        Assert.Empty(StoryAboard.BySpan(Twice, twice));

        var boardingOnly = StoryAboard.Folded(Picked, Twice, both, fired: 1);
        Assert.Equal(once.Switched, StoryAboard.Folded(boardingOnly, Twice, both, fired: 3).Switched);
    }

    [Fact]
    public void ALaterChapterThatBoardsAndLeavesAgainRecordsTheSecondSpan()
    {
        var first = StoryAboard.Folded(Picked, Twice, Chapter("one", boards: ["rider"], leaves: ["rider"], leavesAt: 2), fired: 3);
        var second = StoryAboard.Folded(first, Twice, Chapter("two", boards: ["rider"]), fired: 1);

        Assert.Equal(SecondBoarding with { Chapter = "two" }, second.Switched[^1]);
        Assert.Equal(["rider"], StoryAboard.BySpan(Twice, second));
    }

    [Fact]
    public void ASpanWhoseLeavingPassesWhileItsBoardingIsHeldIsSkipped()
    {
        var held = Picked with { AwaitingSeat = ["rider"] };

        var story = StoryAboard.Missed(held, Twice, ["badGuysCloseIn"], "Sidewinder");

        Assert.Equal([FirstLeaving], story.Switched);
        Assert.Empty(story.AwaitingSeat);
        Assert.Equal([SecondBoarding with { Seats = 2 }], StoryAboard.Due(Twice, story, ["badGuysCloseIn"], "Sidewinder"));
    }

    [Fact]
    public void AHeldBoardingIsHandedOnlyWhileItIsTheMembersNextSwitch()
    {
        var story = StoryAboard.Missed(Picked with { AwaitingSeat = ["rider"] }, Twice, ["finale"], "CobraMkIII");

        Assert.Equal([FirstLeaving, SecondBoarding], story.Switched);
        Assert.Equal(["rider"], StoryAboard.BySpan(Twice, story));
        Assert.Empty(StoryAboard.Due(Twice, story, ["finale"], "CobraMkIII"));
    }

    [Fact]
    public void AStoryWhoseMembersHaveOneSpanEachBehavesAsBefore()
    {
        var once = Secret with { Cast = [.. Secret.Cast, Member("rider", new StorySpeakerAboard("setUp", "midpoint"))] };
        var folded = StoryAboard.Folded(Picked, once, Chapter("one", boards: ["rider"]), fired: 1);

        Assert.Equal(["rider"], StoryAboard.BySpan(once, folded));
        Assert.Empty(StoryAboard.Due(once, folded, ["setUp"]));
        Assert.Empty(StoryAboard.BySpan(once, StoryAboard.Folded(folded, once, Chapter("two", leaves: ["rider"]), fired: 1)));
        Assert.Empty(StoryAboard.Folded(folded, once, Chapter("three", boards: ["rider"]), fired: 1).Switched.Skip(1));
    }

    [Fact]
    public void AHiddenEntryWithOneSpanObjectOrAListOfTwoUnsealsWithNoFaults()
    {
        var one = Unsealed("""{"boards": "midpoint", "leaves": "allIsLost"}""");
        var two = Unsealed("""[{"boards": null, "leaves": "catalyst"}, {"boards": "midpoint", "leaves": null}]""");

        Assert.Equal([new StorySpeakerAboard("midpoint", "allIsLost")], one.Cast[0].Aboard);
        Assert.Equal([new StorySpeakerAboard(null, "catalyst"), new StorySpeakerAboard("midpoint", null)], two.Cast[0].Aboard);
        Assert.Empty(new StoryCatalog([Card], () => [one]).Faults());
        Assert.Empty(new StoryCatalog([Card], () => [two]).Faults());
    }

    private static Story Read(string json) =>
        JsonSerializer.Deserialize<Story>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static StorySecret Unsealed(string aboard)
    {
        var tree = JsonSerializer.SerializeToNode(new[] { Secret }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsArray();
        tree[0]!["cast"]!.AsArray()[0]!["aboard"] = System.Text.Json.Nodes.JsonNode.Parse(aboard);

        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(Encoding.UTF8.GetBytes(tree.ToJsonString()));
        }

        return StoryCatalog.Unseal(Convert.ToBase64String(packed.ToArray())).Single();
    }
}
