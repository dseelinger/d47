using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class ABilingualReplyIsSplitAtItsMarkerTests
{
    [Fact]
    public void TheLinesBeforeAndAfterTheMarkerAreTheSpokenPartAndTheTranslation()
    {
        var (spoken, translation) = BilingualReply.Split("ビーコンは静かになった。\n///\nThe beacon went quiet.");

        Assert.Equal("ビーコンは静かになった。", spoken);
        Assert.Equal("The beacon went quiet.", translation);
    }

    [Fact]
    public void AReplyWithNoMarkerIsAllSpokenWithNoTranslation()
    {
        var (spoken, translation) = BilingualReply.Split("  ビーコンは静かになった。 ");

        Assert.Equal("ビーコンは静かになった。", spoken);
        Assert.Null(translation);
    }

    [Fact]
    public void AMarkerWithNothingAfterItLeavesNoTranslation()
    {
        var (spoken, translation) = BilingualReply.Split("ビーコン。\n///\n  ");

        Assert.Equal("ビーコン。", spoken);
        Assert.Null(translation);
    }

    [Fact]
    public void AMarkerWithNothingBeforeItLeavesNothingToSpeak()
    {
        var (spoken, translation) = BilingualReply.Split("///\nThe beacon went quiet.");

        Assert.Equal(string.Empty, spoken);
        Assert.Equal("The beacon went quiet.", translation);
    }

    [Fact]
    public void AMarkerInsideASentenceDoesNotSplit()
    {
        const string reply = "ビーコン /// 静か\nThe beacon /// quiet.";

        var (spoken, translation) = BilingualReply.Split(reply);

        Assert.Equal(reply, spoken);
        Assert.Null(translation);
    }

    [Fact]
    public void WindowsLineEndingsSplitToo()
    {
        var (spoken, translation) = BilingualReply.Split("はい。\r\n///\r\nYes.");

        Assert.Equal("はい。", spoken);
        Assert.Equal("Yes.", translation);
    }
}
