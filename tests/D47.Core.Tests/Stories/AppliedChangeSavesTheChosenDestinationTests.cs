using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class AppliedChangeSavesTheChosenDestinationTests
{
    private const string ChapterKey = "the-hard-way";

    private static readonly DateTimeOffset ChangedAt = Now.AddHours(1);

    private static readonly AdventureHeading First = new("Tau Ceti", 1234, new StarPosition(5300, 0, 0));

    private static readonly AdventureHeading Second = new("Wolf 359", 4321, new StarPosition(-2000, 0, 0));

    private const string ABountyAndADock = """
        {"opening": "x", "reply": "A bounty does the same.", "beats": [
          {"title": "Another Way", "function": "midpoint", "kind": "bounty", "count": 2, "line": "Two more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private const string NoPlace = """{"home": false, "system": null}""";

    [Fact]
    public async Task AnAppliedChangeThatNamedAPlaceSavesIt()
    {
        using var fixtures = AtTheBond();
        var director = Director(fixtures, First);

        await Propose(director);

        Assert.Null(director.ApplyObjective("F1"));
        Assert.Equal(First, fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task ADiscardedChangeSavesNothing()
    {
        using var fixtures = AtTheBond();
        var director = Director(fixtures, First);

        await Propose(director);
        director.DiscardObjective("F1");

        Assert.Null(fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task ALaterChangeThatNamedNoPlaceLeavesIt()
    {
        using var fixtures = AtTheBond();
        fixtures.Stories.Update("F1", Id, story => story with { ChosenDestination = First });

        var director = Director(fixtures, null);

        await Propose(director);

        Assert.Null(director.ApplyObjective("F1"));
        Assert.Equal(First, fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task ALaterChangeThatNamedAnotherPlaceReplacesIt()
    {
        using var fixtures = AtTheBond();
        fixtures.Stories.Update("F1", Id, story => story with { ChosenDestination = First });
        var director = Director(fixtures, Second);

        await Propose(director);

        Assert.Null(director.ApplyObjective("F1"));
        Assert.Equal(Second, fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    private static Task<ObjectiveProposalOutcome> Propose(StoryDirector director) =>
        director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

    /// <summary>A director over the fixtures' story and book whose writer reports <paramref name="heading"/>.</summary>
    private static StoryDirector Director(StoryFixtures fixtures, AdventureHeading? heading)
    {
        var generator = AdventureGeneratorTests.Generator(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NoPlace), RoundScriptedLlmProvider.Saying(ABountyAndADock)), new AdventureGeneratorTests.Galaxy());

        return new StoryDirector(
            fixtures.Stories,
            fixtures.Book,
            () => StoryFixtures.Catalog,
            async (ask, now, cancellationToken) => (await generator.GenerateAsync(ask, now, cancellationToken)) with { Heading = heading },
            () => fixtures.Here,
            _ => { },
            NullLogger.Instance)
        {
            Backstory = () => null,
            Clock = () => ChangedAt,
        };
    }

    private static AdventureTrigger Counted(TriggerKind kind, int count) => new() { Kind = kind, Count = count };

    private static StoryFixtures AtTheBond()
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", ChapterKey],
            CluesGiven = 2,
        });

        Assert.Null(fixtures.Book.Write("F1", new Adventure
        {
            Key = ChapterKey,
            Name = "The Hard Way",
            Source = AdventureSource.Generated,
            Written = Now,
            Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed to you.", Ending = "It is forgiven." },
            Opening = "Go.",
            StoryId = Id,
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("First Blood", "catalyst", Counted(TriggerKind.Bounty, 1), "One."),
                Beat("The Wing", "midpoint", Counted(TriggerKind.Bond, 3), "Three bonds."),
                Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."),
            ],
        }));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        return fixtures;
    }
}
