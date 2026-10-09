using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>While a story runs, speech about a mission carries an excerpt of its public layer, once per mission, and the chapter writer is told the missions held.</summary>
public sealed class AMissionCarriesTheStoryTests
{
    public static JournalEvent Accept(long id, DateTimeOffset at, string? cargo = null) => AdventureFixtures.Event(AcceptLine(id, at, cargo));

    public static string AcceptLine(long id, DateTimeOffset at, string? cargo = null) =>
        $$"""{ "timestamp":"{{AdventureFixtures.Stamp(at)}}", "event":"MissionAccepted", "Faction":"Ossen Union", "Name":"Mission_Courier", "LocalisedName":"Courier {{id}}", "DestinationSystem":"Dyson's Hollow", "DestinationStation":"Maren Anchorage", "MissionID":{{id}}, "Reward":50000{{(cargo is null ? string.Empty : $", \"Commodity\":\"${cargo}_Name;\", \"Commodity_Localised\":\"{cargo}\", \"Count\":40")}} }""";

    public static List<Announcement> Taken(MissionCallout callout, DateTimeOffset at, CommanderGameState? state, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(at, false, state, GameStatus.Unknown, NavRoute.None, events))];

    internal static StoryMissionAsides Asides(StoryFixtures fixtures)
    {
        var asides = fixtures.Director.MissionAsides;
        asides.Excerpt = () => fixtures.Director.MissionExcerpt("F1");
        return asides;
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AMissionTakenWithNothingElseToSayGetsALineForTheModelToWrite()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var callout = new MissionCallout { StoryAsides = Asides(fixtures) };

        var line = Assert.Single(Taken(callout, Now, null, Accept(7, Now)));

        Assert.Equal($"{MissionCallout.AcceptedKey}.7", line.Key);
        Assert.Equal(string.Empty, line.Text);
        Assert.Contains("Courier 7 for Ossen Union, to Maren Anchorage, Dyson's Hollow", line.StoryAside!.Mission);
        Assert.Contains("\"The Test Story\" — Quiet test.", line.StoryAside.Excerpt);
        Assert.Contains(Card.InYourWords, line.StoryAside.Excerpt);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheAcceptBriefKeepsTheFactsAndCarriesTheExcerpt()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var callout = new MissionCallout { StoryAsides = Asides(fixtures) };
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));
        state.Apply(AdventureFixtures.Event($$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Loadout", "Ship":"sidewinder", "ShipID":1, "CargoCapacity":4 }"""));

        var line = Assert.Single(Taken(callout, Now, state, Accept(8, Now, cargo: "Gold")));
        var brief = FlavourBriefs.For(line, personalityEnabled: true)!;

        Assert.Equal("That's 40 tons against a 4-ton hold. Ten trips, or a bigger ship.", line.Text);
        Assert.Contains(line.Text, brief.Instruction);
        Assert.Contains(line.StoryAside!.Excerpt, brief.Instruction);
        Assert.Contains(FlavourBriefs.MissionUnchanged, brief.Instruction);
        Assert.True(new RewordChance(new Random(1)).ShouldReword(line, rewordPercent: 0));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void EachMissionHasAtMostOneAside()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var asides = Asides(fixtures);
        var callout = new MissionCallout { StoryAsides = asides };
        var board = MissionBoard.Empty.Apply(Accept(7, Now)).Apply(Accept(9, Now));
        var first = board.For(7)!;
        var second = board.For(9)!;

        Assert.NotNull(Assert.Single(Taken(callout, Now, null, Accept(7, Now))).StoryAside);

        Assert.Null(asides.Take(first));

        var both = asides.Take([first, second])!;
        Assert.DoesNotContain("Courier 7", both.Mission);
        Assert.Contains("Courier 9", both.Mission);
        Assert.Null(asides.Take([first, second]));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void MissionSceneChatterIsToldTheStory()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var asides = Asides(fixtures);
        var beats = new MissionBeats();
        var accepted = Accept(7, Now);
        beats.Fold(Now, [accepted], MissionBoard.Empty.Apply(accepted));
        var beat = Assert.Single(beats.Due(Now + MissionBeats.Window));

        var told = NpcChatter.SceneInstruction(beat, "A courier between rivals.", aside: asides.Take(beat.Missions!));
        var plain = NpcChatter.SceneInstruction(beat, "A courier between rivals.");

        Assert.Contains("\"The Test Story\"", told);
        Assert.Contains("bear on neither the scenario nor the story", told);
        Assert.DoesNotContain("The Test Story", plain);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheNarratorTiesTheNewestUntoldMissionToTheStory()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var gap = TimeSpan.FromMinutes(30);
        var narrator = new NarratorCallout(new NearbyFight())
        {
            Interval = gap,
            Longest = gap,
            StoryRunning = () => fixtures.Director.IsRunning("F1"),
            StoryAsides = Asides(fixtures),
        };
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));
        state.Apply(Accept(7, Now));
        state.Apply(Accept(9, Now.AddMinutes(5)));

        _ = narrator.Examine(Lull(Now, state)).ToArray();
        var narration = Assert.Single(narrator.Examine(Lull(Now + gap * 3, state)));

        Assert.Contains("Courier 9", narration.StoryAside!.Mission);
        Assert.Contains(narration.StoryAside.Excerpt, FlavourBriefs.For(narration, personalityEnabled: true)!.Instruction);

        var next = Assert.Single(narrator.Examine(Lull(Now + gap * 6, state)));
        Assert.Contains("Courier 7", next.StoryAside!.Mission);

        Assert.Null(Assert.Single(narrator.Examine(Lull(Now + gap * 9, state))).StoryAside);
    }

    [Fact]
    public async Task TheChapterWriterIsToldTheMissionsHeld()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere));

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), 1, AcceptLine(7, Now))
            .GenerateAsync(
                new AdventureAsk(AdventureReach.Session, AdventureLength.Short, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 2, 10, 5)),
                Now,
                CancellationToken.None);

        var prompt = provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("The missions the Commander holds now:\n- Courier 7 for Ossen Union, to Maren Anchorage, Dyson's Hollow", prompt.ReplaceLineEndings("\n"));
        Assert.Contains("an \"arrive\" objective in its system, or a \"dock\" objective at its station", prompt);
    }

    private static CalloutContext Lull(DateTimeOffset now, CommanderGameState state) =>
        new(now, false, state, GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip }, NavRoute.None, [], null);
}
