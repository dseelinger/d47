using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A rank objective may name the Imperial or the Federal navy, 1 to 14, and is worded by rank title.</summary>
public sealed class ARankObjectiveAcceptsANavyTests
{
    private static Adventure Navy(string navy, int rank) => new()
    {
        Key = "navy-rank",
        Name = "Navy Rank",
        Source = AdventureSource.Generated,
        Beats = [Beat("Promotion", "finale", new AdventureTrigger { Kind = TriggerKind.Rank, Career = navy, Rank = rank }, "Well.")],
        AcceptedAt = Accepted,
    };

    [Fact]
    public void AnEmpireObjectiveFiresOnAPromotionToThatRank()
    {
        var standing = AdventureFold.Apply(AdventureFold.Start(Navy("Empire", 7)), Promotion("Empire", 7, Accepted.AddMinutes(1)));

        Assert.True(standing.IsDone);
        Assert.Empty(AdventureValidation.Problems(Navy("Empire", 7)));
    }

    [Fact]
    public void ANavyAcceptsOneToFourteenAndNoMore()
    {
        Assert.Empty(AdventureValidation.Problems(Navy("Federation", 14)));
        Assert.Contains(AdventureValidation.Problems(Navy("Federation", 15)), problem => problem.Contains("ranks run 1 to 14"));
    }

    [Fact]
    public void TheNavyWordsMatchTheJournalKeys()
    {
        Assert.Equal("Empire", Careers.Match("Empire"));
        Assert.Equal("Empire", Careers.Match("Imperial Navy"));
        Assert.Equal("Empire", Careers.Match("Imperial"));
        Assert.Equal("Federation", Careers.Match("Federation"));
        Assert.Equal("Federation", Careers.Match("Federal Navy"));
        Assert.Equal("Federation", Careers.Match("Federal"));
        Assert.Equal("Imperial Navy", Careers.Word("Empire"));
        Assert.Equal("Federal Navy", Careers.Word("Federation"));
    }

    [Fact]
    public void ANavyRankIsDescribedByItsTitle()
    {
        Assert.Equal("reach Baron in the Imperial Navy", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Empire", Rank = 7 }.Describe());
        Assert.Equal("reach Admiral in the Federal Navy", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Federation", Rank = 14 }.Describe());
    }

    [Fact]
    public async Task ANavyAlreadyAtTheTopIsRefusedNamingItsTopRank()
    {
        const string navy = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Imperial Navy", "rank": 14, "line": "It balances."}
            ]}
            """;

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(navy),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(navy, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(navy, 2)));

        var generator = AdventureGeneratorTests.Generator(
            provider, new AdventureGeneratorTests.Galaxy(), 1, """{ "timestamp":"2026-08-22T11:00:02Z", "event":"Promotion", "Empire":14 }""");

        var outcome = await generator.GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Accepted.AddHours(1), CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("where the Commander is already King", outcome.Refusal);
    }
}
