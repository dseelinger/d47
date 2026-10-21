using D47.Core.Adventures;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>With no stock story running, "choose option one" acts on the one short story whose choice is open, and an abandoned one is not.</summary>
public sealed class TheChoicePhrasesActOnTheStoryWaitingOnAChoiceTests
{
    private static Adventure ShortStory(string key, string name) => AChapterCanOfferAChoiceTests.Chapter() with { Key = key, Name = name, StoryId = null };

    [Fact]
    public void AnAbandonedShortStoryDoesNotStandInTheWayOfTheActiveOnesChoice()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        foreach (var key in new[] { "abandoned", "active" })
        {
            Assert.Null(fixtures.Book.Write("F1", ShortStory(key, key)));
        }

        fixtures.Book.CatchUp([]);
        fixtures.Book.Observe(AChapterCanOfferAChoiceTests.Accept(1), "F1");

        foreach (var key in new[] { "abandoned", "active" })
        {
            fixtures.Book.Offered("F1", key, 1, Accepted.AddMinutes(2));
        }

        Assert.Null(fixtures.Book.Abandon("F1", "abandoned", Accepted.AddMinutes(3)));

        Assert.True(fixtures.Director.ChoiceOpen("F1"));

        var chosen = fixtures.Director.Choose("F1", 0, Accepted.AddMinutes(4));

        Assert.Null(chosen.Refusal);
        Assert.Single(fixtures.Book.Store.Find("F1", "active")!.Choices);
        Assert.Empty(fixtures.Book.Store.Find("F1", "abandoned")!.Choices);
    }

    [Fact]
    public void WithNoChoiceOpenThePhrasesAreNotLive()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        Assert.False(fixtures.Director.ChoiceOpen("F1"));
        Assert.Equal(AdventureChoiceOutcome.NoChoice, fixtures.Director.Choose("F1", 0, Accepted).Refusal);
    }
}
