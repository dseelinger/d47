using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Some phrases only mean what they say when they were spoken.</summary>
public class SpokenOnlyKeywordsTests
{
    private static KeywordRouter Router(TempInstall install) =>
        new(TestSurface.For(install).Registry);

    [Theory]
    [InlineData("can you hear me")]
    [InlineData("are you listening")]
    public void ASpokenOnlyPhraseIsIgnoredWhenItWasTyped(string input)
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).Match(input, InputSource.Typed));
    }

    [Theory]
    [InlineData("can you hear me")]
    [InlineData("are you listening")]
    public void ASpokenOnlyPhraseReachesTheReportWhenItWasSpoken(string input)
    {
        using var install = new TempInstall();

        var match = Router(install).Match(input, InputSource.Spoken);

        Assert.NotNull(match);
        Assert.Equal("get_listening_status", match.ToolName);
    }

    /// <summary>
    /// The spoken set is added rather than substituted, so nothing that worked by typing stops working
    /// by being said.
    /// </summary>
    [Theory]
    [InlineData("what microphone am I on", InputSource.Typed)]
    [InlineData("what microphone am I on", InputSource.Spoken)]
    [InlineData("what is my push to talk key", InputSource.Typed)]
    [InlineData("what is my push to talk key", InputSource.Spoken)]
    public void TheOrdinaryVocabularyMatchesWhicheverWayItArrived(string input, InputSource source)
    {
        using var install = new TempInstall();

        Assert.NotNull(Router(install).Match(input, source));
    }

    /// <summary>
    /// Typed is the default, so a caller that forgets to say how the input arrived gets the
    /// conservative answer rather than the microphone report.
    /// </summary>
    [Fact]
    public void TheDefaultSourceIsTyped()
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).Match("can you hear me"));
    }
}
