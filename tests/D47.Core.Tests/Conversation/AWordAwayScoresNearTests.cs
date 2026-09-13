using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>One word inserted, dropped or swapped, against a phrase long enough to carry it.</summary>
public class AWordAwayScoresNearTests
{
    [Fact]
    public void OneWordInsertedScoresNear() =>
        Assert.Equal(PhraseMatch.Near, PhraseScore.Score("focus the game", "focus on the game"));

    [Fact]
    public void ATwoWordPhraseIsNeverNear() =>
        Assert.Null(PhraseScore.Score("lights of", "lights on"));

    [Fact]
    public void AOneWordUtteranceIsNeverNear() =>
        Assert.Null(PhraseScore.Score("elite", "focus on the game"));

    [Fact]
    public void RankPutsEquivalentBeforeNearInDeclarationOrder()
    {
        string[] phrases = ["focus on the game", "focus game", "set focus to elite"];

        var ranked = PhraseScore.Rank("set focus on elite", phrases);

        Assert.Equal(
            [("set focus to elite", PhraseMatch.Equivalent), ("focus on the game", PhraseMatch.Near)],
            ranked.Select(entry => (entry.Phrase, entry.Match)));
    }
}
