using D47.Core.Storage;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// A transcriber writes numbers as digits, percentages with "%" and some compounds as two words; the
/// router folds the utterance and the declared phrase the same way before comparing them.
/// </summary>
public class HoweverATranscriptWritesItTheCommandRoutesTests
{
    [Theory]
    [InlineData("25", "twenty five")]
    [InlineData("0", "zero")]
    [InlineData("100", "one hundred")]
    [InlineData("101", "101")]
    [InlineData("twenty-five", "twenty five")]
    [InlineData("night-vision", "night vision")]
    [InlineData("75%", "seventy five percent")]
    [InlineData("fifty per cent", "fifty percent")]
    [InlineData("Super Cruise", "supercruise")]
    [InlineData("heatsink", "heat sink")]
    [InlineData("hard points", "hardpoints")]
    [InlineData("E.C.M.", "ecm")]
    [InlineData("throttle to the 50", "throttle to fifty")]
    public void TheFoldWritesOneSpelling(string said, string folded)
    {
        Assert.Equal(folded, KeywordRouter.Folded(said), StringComparer.OrdinalIgnoreCase);
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData("Throttle to 25.", "throttle to twenty-five")]
    [InlineData("50 percent", "fifty per cent")]
    [InlineData("75%", "seventy-five per cent")]
    [InlineData("Super Cruise", "supercruise")]
    [InlineData("Engage super cruise.", "engage supercruise")]
    [InlineData("heatsink", "heat sink")]
    [InlineData("Drop a heatsink.", "drop a heat sink")]
    [InlineData("Deploy hard points.", "deploy hardpoints")]
    public void TheWrittenFormReachesWhatTheSpelledFormReaches(string said, string spelled)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        var declared = router.MatchToolCommand(spelled);

        Assert.NotNull(declared);
        Assert.Null(router.MatchSetting(said));

        var heard = router.MatchToolCommand(said);

        Assert.NotNull(heard);
        Assert.Equal(declared.CapabilityId, heard.CapabilityId);
        Assert.Equal(declared.ToolName, heard.ToolName);
        Assert.Equal(
            declared.Arguments.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            heard.Arguments.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>A taught wording that folds to a built-in phrase would be shadowed by it, so it is refused.</summary>
    [Trait("Category", "Integration")]
    [Fact]
    public void ATaughtWordingThatFoldsToABuiltInPhraseClashes()
    {
        using var install = new TempInstall();
        var book = PhraseBook.From(TestSurface.For(install).Registry, []);
        var store = new LearnedPhrasesStore(
            Path.Combine(install.Root, "phrases.json"), new DiskFileSystem(), NullLogger<LearnedPhrasesStore>.Instance);

        var clash = store.FindClash("F1", ["throttle to 50"], "half throttle please", book);

        Assert.NotNull(clash);
        Assert.Equal(PhraseClashKind.BookPhrase, clash.Kind);
        Assert.Equal("throttle to fifty", clash.StandsFor);
    }

    /// <summary>The near-miss scorer folds the same way, so it agrees with the exact match.</summary>
    [Fact]
    public void TheScorerCallsTheWrittenFormEquivalent()
    {
        Assert.Equal(PhraseMatch.Equivalent, PhraseScore.Score("throttle to 25", "throttle to twenty-five"));
        Assert.Equal(PhraseMatch.Equivalent, PhraseScore.Score("75%", "seventy-five per cent"));
    }
}
