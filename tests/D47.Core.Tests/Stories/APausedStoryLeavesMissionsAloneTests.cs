using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Stories.AMissionCarriesTheStoryTests;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A paused or switched-off story gives mission speech no aside, and a mission taken meanwhile keeps its aside for later.</summary>
[Trait("Category", "Integration")]
public sealed class APausedStoryLeavesMissionsAloneTests
{
    [Fact]
    public void AMissionTakenWhileThePausedStoryWaitsSaysNothingNew()
    {
        using var fixtures = PauseSupport.Picked(out var chapter);
        var asides = Asides(fixtures);
        var callout = new MissionCallout { StoryAsides = asides };

        fixtures.Book.Abandon("F1", chapter, Now);
        Assert.Null(fixtures.Director.Tick("F1", Now));

        Assert.Null(fixtures.Director.MissionExcerpt("F1"));
        Assert.Empty(Taken(callout, Now, null, Accept(7, Now)));
        Assert.False(asides.Told(7));
    }

    [Fact]
    public void ALineWithItsOwnFactsIsSaidWithoutTheStoryWhileSwitchedOff()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var callout = new MissionCallout { StoryAsides = Asides(fixtures) };
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));
        state.Apply(Adventures.AdventureFixtures.Event($$"""{ "timestamp":"{{Adventures.AdventureFixtures.Stamp(Now)}}", "event":"Loadout", "Ship":"sidewinder", "ShipID":1, "CargoCapacity":4 }"""));

        fixtures.Director.SetOn("F1", false, Now);

        var line = Assert.Single(Taken(callout, Now, state, Accept(8, Now, cargo: "Gold")));

        Assert.Null(line.StoryAside);
        Assert.Null(FlavourBriefs.For(line, personalityEnabled: true));
    }

    [Fact]
    public void AMissionTakenWhileOffGetsItsAsideOnceTheStoryIsBack()
    {
        using var fixtures = PauseSupport.Picked(out _);
        var asides = Asides(fixtures);
        var board = MissionBoard.Empty.Apply(Accept(7, Now));

        fixtures.Director.SetOn("F1", false, Now);
        Assert.Null(asides.Take(board.For(7)!));

        fixtures.Director.SetOn("F1", true, Now.AddHours(1));
        Assert.NotNull(asides.Take(board.For(7)!));
    }

    [Fact]
    public void WithNoStoryAMissionWithNothingToSayStaysSilent()
    {
        using var fixtures = new StoryFixtures(new Conversation.RoundScriptedLlmProvider());
        var callout = new MissionCallout { StoryAsides = Asides(fixtures) };

        Assert.Empty(Taken(callout, Now, null, Accept(7, Now)));
    }
}
