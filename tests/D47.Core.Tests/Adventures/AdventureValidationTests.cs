using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The trigger vocabulary is closed and the prose is free (list.md Phase 47). A step naming an
/// event that does not exist is refused by name with the reason.
/// </summary>
public class AdventureValidationTests
{
    [Fact]
    public void AWellFormedAdventureHasNoProblems()
    {
        Assert.Empty(AdventureValidation.Problems(LanternRoute()));
        Assert.Empty(AdventureValidation.NotReady(LanternRoute()));
    }

    [Fact]
    public void ARankBeatNamingNoCareerIsRefusedWithTheCareers()
    {
        var adventure = LanternRoute() with
        {
            Beats = [Beat("Promotion", "finale", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Piracy", Rank = 3 }, "Well.")],
        };

        var problem = Assert.Single(AdventureValidation.Problems(adventure));

        Assert.Contains("Beat 1 (Promotion)", problem);
        Assert.Contains("Piracy", problem);
        Assert.Contains("Exploration", problem);
    }

    [Fact]
    public void ARankOutsideTheLadderIsRefused()
    {
        var adventure = LanternRoute() with
        {
            Beats = [Beat("Promotion", null, new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Combat", Rank = 9 }, "Well.")],
        };

        Assert.Contains(AdventureValidation.Problems(adventure), problem => problem.Contains("ranks run 1 to 8"));
    }

    [Fact]
    public void ABeatWithNeitherANameNorAnIdGoesNowhere()
    {
        var adventure = LanternRoute() with
        {
            Beats = [Beat("Somewhere", null, new AdventureTrigger { Kind = TriggerKind.Arrive }, "Here.")],
        };

        Assert.Contains(AdventureValidation.Problems(adventure), problem => problem.Contains("names no system"));
    }

    [Fact]
    public void ANamedButUnresolvedPlaceIsAProblemForBeginAndNotForStoring()
    {
        var adventure = LanternRoute() with
        {
            Beats = [Beat("Somewhere", null, new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Ossen's Lantern" }, "Here.")],
        };

        Assert.Empty(AdventureValidation.Problems(adventure));

        var reason = Assert.Single(AdventureValidation.NotReady(adventure));

        Assert.Contains("Beat 1 (Somewhere)", reason);
        Assert.Contains("Ossen's Lantern", reason);
    }

    [Fact]
    public void LimitsAreStatedInCharacters()
    {
        var adventure = LanternRoute() with
        {
            Beats = [Beat("Long", null, new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1 }, new string('x', AdventureLimits.MaxLineLength + 1))],
        };

        var problem = Assert.Single(AdventureValidation.Problems(adventure));

        Assert.Contains($"{AdventureLimits.MaxLineLength + 1} characters", problem);
    }

    [Fact]
    public void TooManyBeatsIsRefused()
    {
        var beat = Beat("B", null, new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1 }, "L");
        var adventure = LanternRoute() with { Beats = [.. Enumerable.Repeat(beat, AdventureLimits.MaxBeats + 1)] };

        Assert.Contains(AdventureValidation.Problems(adventure), problem => problem.Contains("at most 12"));
    }

    [Theory]
    [InlineData("arrive", TriggerKind.Arrive)]
    [InlineData("DOCK", TriggerKind.Dock)]
    [InlineData(" scan ", TriggerKind.Scan)]
    public void TheFiveKindsParseByWord(string word, TriggerKind expected)
    {
        Assert.True(AdventureValidation.TryKind(word, out var kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("deliver")]
    [InlineData("kill")]
    [InlineData("")]
    [InlineData("7")]
    public void AnythingElseIsNotAKind(string word)
    {
        Assert.False(AdventureValidation.TryKind(word, out _));
    }

    [Fact]
    public void CareersMatchByJournalKeyOrSpokenWord()
    {
        Assert.Equal("Explore", Careers.Match("exploration"));
        Assert.Equal("Explore", Careers.Match("Explore"));
        Assert.Equal("Soldier", Careers.Match("Mercenary"));
        Assert.Null(Careers.Match("Federation"));
    }

    [Fact]
    public void KeysAreSlugs()
    {
        Assert.Equal("the-lantern-route", AdventureValidation.Key("The Lantern Route!"));
        Assert.Equal("veyl-3-c", AdventureValidation.Key("  Veyl 3 c  "));
    }

    [Fact]
    public void TriggersDescribeThemselvesByNameAndFallBackToTheId()
    {
        Assert.Equal("arrive at Ossen's Lantern", new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Ossen's Lantern" }.Describe());
        Assert.Equal("arrive at system 42", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 42 }.Describe());
        Assert.Equal("dock at Maren Anchorage in Dyson's Hollow", new AdventureTrigger { Kind = TriggerKind.Dock, Station = "Maren Anchorage", System = "Dyson's Hollow" }.Describe());
        Assert.Equal("reach Exploration rank 6", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Explore", Rank = 6 }.Describe());
    }
}
