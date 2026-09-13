using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Asking about the Elite rank is not an instruction, and must not score against anything.</summary>
public class AQuestionAboutEliteScoresNothingTests
{
    private static IReadOnlyList<string> BuiltinPhrases()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        return [.. PhraseBook.From(registry, []).Entries.Select(entry => entry.Phrase)];
    }

    [Theory]
    [InlineData("what is my elite rank in combat")]
    [InlineData("am I elite yet")]
    [InlineData("how far off elite am I in exploration")]
    public void ScoresNothingAgainstAnyBuiltinPhrase(string asked)
    {
        var phrases = BuiltinPhrases();

        Assert.Empty(PhraseScore.Rank(asked, phrases));
    }

    [Fact]
    public void AOneWordUtteranceScoresNothingAgainstAnyBuiltinPhrase()
    {
        var phrases = BuiltinPhrases();

        Assert.Empty(PhraseScore.Rank("elite", phrases));
    }
}
