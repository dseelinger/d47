using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class SentenceSplitterTests
{
    /// <summary>Feeds text the way a provider does — in arbitrary fragments.</summary>
    private static List<string> StreamIn(string text, int chunkSize)
    {
        var splitter = new SentenceSplitter();
        var spoken = new List<string>();

        for (var i = 0; i < text.Length; i += chunkSize)
        {
            spoken.AddRange(splitter.Push(text.Substring(i, Math.Min(chunkSize, text.Length - i))));
        }

        if (splitter.Flush() is { } tail)
        {
            spoken.Add(tail);
        }

        return spoken;
    }

    [Fact]
    public void ASentenceIsEmittedAsSoonAsItClosesRatherThanAtEndOfTurn()
    {
        var splitter = new SentenceSplitter();

        // The space after the stop is the deciding character, so the first sentence is out
        // the moment it arrives — before the rest of the reply has been generated at all.
        Assert.Equal(
            ["You are in Shinrarta Dezhra."],
            splitter.Push("You are in Shinrarta Dezhra. "));

        // And the second one waits, because nothing yet proves its stop was terminal.
        Assert.Empty(splitter.Push("The station is Jameson Memorial."));
        Assert.Equal("The station is Jameson Memorial.", splitter.Flush());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(1000)]
    public void TheSplitIsTheSameHoweverTheDeltasHappenToLand(int chunkSize)
    {
        const string reply =
            "You are in Sol. Fuel is at 62 percent. Shall I plot a route?";

        Assert.Equal(
            ["You are in Sol.", "Fuel is at 62 percent.", "Shall I plot a route?"],
            StreamIn(reply, chunkSize));
    }

    [Fact]
    public void ADecimalPointIsNotASentenceBoundary()
    {
        Assert.Equal(
            ["Jump range is 12.5 light years."],
            StreamIn("Jump range is 12.5 light years.", 4));
    }

    [Fact]
    public void ACommanderAbbreviationDoesNotEndASentence()
    {
        Assert.Equal(
            ["Cmdr. Jameson is in the wing."],
            StreamIn("Cmdr. Jameson is in the wing.", 5));
    }

    [Fact]
    public void ASystemDesignationSurvivesIntact()
    {
        Assert.Equal(
            ["Col 285 Sector AB-C d1.2 is unexplored."],
            StreamIn("Col 285 Sector AB-C d1.2 is unexplored.", 6));
    }

    [Fact]
    public void AnEllipsisIsOneBoundaryNotThree()
    {
        Assert.Equal(
            ["Scanning...", "Nothing on the sensors."],
            StreamIn("Scanning... Nothing on the sensors.", 3));
    }

    [Fact]
    public void ClosingPunctuationStaysWithTheSentenceItCloses()
    {
        Assert.Equal(
            ["He said \"hardpoints deployed.\"", "I would not."],
            StreamIn("He said \"hardpoints deployed.\" I would not.", 7));
    }

    [Fact]
    public void ANewlineEndsASentenceWithoutATerminator()
    {
        Assert.Equal(
            ["Fuel: 62 percent", "Hull: 100 percent"],
            StreamIn("Fuel: 62 percent\nHull: 100 percent", 4));
    }

    [Fact]
    public void TheLastSentenceArrivesOnFlushBecauseNothingElseCanProveItWasLast()
    {
        var splitter = new SentenceSplitter();

        Assert.Empty(splitter.Push("Docking granted."));

        Assert.Equal("Docking granted.", splitter.Flush());
        Assert.Null(splitter.Flush());
    }

    /// <summary>
    /// The failure this guards is silence: a model that never emits a terminator would leave
    /// the whole reply buffered until the turn ended, which is the thing the splitter exists
    /// to prevent.
    /// </summary>
    [Fact]
    public void ARunOnIsBrokenAtPunctuationRatherThanBufferedForever()
    {
        var runOn = string.Join(", ", Enumerable.Repeat("and then another thing happened", 20));

        var splitter = new SentenceSplitter();
        var spoken = splitter.Push(runOn);

        Assert.NotEmpty(spoken);
        Assert.All(spoken, sentence => Assert.True(sentence.Length <= 320));
        Assert.DoesNotContain(spoken, sentence => sentence.EndsWith("an", StringComparison.Ordinal));
    }

    [Fact]
    public void WhitespaceOnlyOutputProducesNothingToSpeak()
    {
        var splitter = new SentenceSplitter();

        Assert.Empty(splitter.Push("   \n  \n "));
        Assert.Null(splitter.Flush());
    }

    [Fact]
    public void MultipleBoundariesInOneDeltaAllComeOut()
    {
        var splitter = new SentenceSplitter();

        var spoken = splitter.Push("Shields up. Hull at 40 percent. Get out of here now! ");

        Assert.Equal(
            ["Shields up.", "Hull at 40 percent.", "Get out of here now!"],
            spoken);
    }
}
