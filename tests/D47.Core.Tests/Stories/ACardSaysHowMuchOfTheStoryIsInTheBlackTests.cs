using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A card's <c>black</c> is none, part or most of the sheet's beats in the hidden entry's spans, and the catalogue filter keeps the stories with any (#1844).</summary>
public sealed class ACardSaysHowMuchOfTheStoryIsInTheBlackTests
{
    private static StorySecret Spanning(params StoryBlackSpan[] spans) => Secret with { Black = spans };

    [Fact]
    public void AnEntryWithNoSpansIsNone() =>
        Assert.Equal(StoryBlackAmount.None, StoryBlack.Amount(Secret, StoryPacing.OneYear));

    [Fact]
    public void ASpanOverLessThanHalfTheSheetIsPart() =>
        Assert.Equal(StoryBlackAmount.Part, StoryBlack.Amount(Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint")), StoryPacing.OneYear));

    [Fact]
    public void ASpanOverMoreThanHalfTheSheetIsMost() =>
        Assert.Equal(StoryBlackAmount.Most, StoryBlack.Amount(Spanning(new StoryBlackSpan("setUp", null)), StoryPacing.OneYear));

    [Fact]
    public void AWholeStorySpanIsMost() =>
        Assert.Equal(StoryBlackAmount.Most, StoryBlack.Amount(Spanning(new StoryBlackSpan(null, null)), StoryPacing.ThreeDays));

    [Fact]
    public void ACardWithoutTheFieldReadsAsNone()
    {
        Assert.Null(Card.Black);
        Assert.Equal(StoryBlackAmount.None, Card.InTheBlack);
        Assert.Equal(StoryBlackAmount.None, (Card with { Black = "sideways" }).InTheBlack);
    }

    [Fact]
    public void ACardThatDisagreesWithTheSpansIsRefused()
    {
        var secret = Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint"));

        Assert.Contains(StoryCatalog.Faults(Card with { Black = "most" }, secret), fault => fault.Contains("black is most", StringComparison.Ordinal));
        Assert.DoesNotContain(StoryCatalog.Faults(Card with { Black = "part" }, secret), fault => fault.Contains("black is", StringComparison.Ordinal));
        Assert.DoesNotContain(StoryCatalog.Faults(Card, secret), fault => fault.Contains("black is", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFilterKeepsOnlyStoriesInTheBlack()
    {
        var cards = new[] { Card, Card with { Id = "p", Black = "part" }, Card with { Id = "m", Black = "most" }, Card with { Id = "n", Black = "none" } };
        var filter = new StoryFilter(InTheBlack: true);

        Assert.Equal(["p", "m"], cards.Where(filter.Matches).Select(card => card.Id));
        Assert.False(filter.IsDefault);
        Assert.NotEqual(new StoryFilter(), filter);
    }
}
