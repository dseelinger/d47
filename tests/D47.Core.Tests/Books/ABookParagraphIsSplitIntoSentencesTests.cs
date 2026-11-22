using D47.Core.Books;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class ABookParagraphIsSplitIntoSentencesTests
{
    [Fact]
    public void TitlesAndInitialsStayInsideTheirSentence()
    {
        Assert.Equal(
            ["Mr. Hale met H. G. Wells at the inn.", "They talked until dawn."],
            BookSentences.Split("Mr. Hale met H. G. Wells at the inn. They talked until dawn."));
    }

    [Fact]
    public void QuestionsExclamationsAndClosingQuotesEndASentence()
    {
        Assert.Equal(
            ["“Who goes there?”", "No answer came!", "So I waited."],
            BookSentences.Split("“Who goes there?” No answer came! So I waited."));
    }

    [Fact]
    public void AFullStopBeforeALowerCaseWordEndsNoSentence()
    {
        Assert.Equal(["It was 3 p.m. and raining, i.e. a bad day."], BookSentences.Split("It was 3 p.m. and raining, i.e. a bad day."));
    }

    [Fact]
    public void APronounIEndsASentence()
    {
        Assert.Equal(["So did I.", "Then we left."], BookSentences.Split("So did I. Then we left."));
    }

    [Fact]
    public void AParagraphIsSplitThroughItsSentencesProperty()
    {
        Assert.Equal(["One.", "Two."], new BookParagraph("One. Two.").Sentences);
    }
}
