using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// The arithmetic behind "Search whichever tab you are looking at" (Phase 12).
/// <para>
/// It is in Core, and so is this, because the awkward parts of that feature are all offsets:
/// the current hit surviving the log growing underneath it, stepping wrapping at both ends, and
/// a query that matches nothing. None of them need a control to be wrong.
/// </para>
/// </summary>
public class TextSearchTests
{
    [Fact]
    public void EveryHitIsFoundInOrderAndIgnoringCase()
    {
        var found = TextSearch.Find("Fuel at Fixture Anchorage. FUEL is low.", "fuel");

        Assert.Equal([0, 27], found.Select(match => match.Start));
        Assert.All(found, match => Assert.Equal(4, match.Length));
    }

    /// <summary>
    /// Non-overlapping, resuming past the end of each hit. Four overlapping hits in "aaaa"
    /// would step the reader through the same text twice and count it as four.
    /// </summary>
    [Fact]
    public void HitsDoNotOverlap()
    {
        Assert.Equal(2, TextSearch.Find("aaaa", "aa").Count);
    }

    [Theory]
    [InlineData(null, "fuel")]
    [InlineData("Fuel is low", null)]
    [InlineData("Fuel is low", "")]
    [InlineData("", "fuel")]
    [InlineData("Fuel is low", "shields")]
    public void NothingToFindIsNoHitsRatherThanAFailure(string? text, string? query)
    {
        Assert.Empty(TextSearch.Find(text, query));
    }

    /// <summary>
    /// The current hit is held as an offset, so a line arriving at the end of the log leaves it
    /// naming the same piece of text. Held as an index it would name whatever the third hit had
    /// become.
    /// </summary>
    [Fact]
    public void AHitStaysTheSameHitWhenThePageGrowsUnderneathIt()
    {
        const string Log = "fuel\nshields\nfuel\n";

        var before = TextSearch.Find(Log, "fuel");
        var current = 1;
        var offset = before[current].Start;

        var after = TextSearch.Find(Log + "fuel\nfuel\n", "fuel");

        Assert.Equal(4, after.Count);
        Assert.Equal(current, TextSearch.Track(after, offset));
        Assert.Equal(offset, after[TextSearch.Track(after, offset)].Start);
    }

    /// <summary>
    /// A hit that is no longer there hands back the nearest one after it, rather than sending
    /// the reader back to the top of a page they had scrolled through.
    /// </summary>
    [Fact]
    public void AVanishedHitResolvesToTheNearestOneAfterIt()
    {
        var matches = TextSearch.Find("fuel .......... fuel", "fuel");

        Assert.Equal(1, TextSearch.Track(matches, 5));
        Assert.Equal(1, TextSearch.Track(matches, 999));
        Assert.Equal(-1, TextSearch.Track([], 0));
    }

    [Fact]
    public void SteppingWrapsAtBothEnds()
    {
        Assert.Equal(1, TextSearch.Step(3, 0, 1));
        Assert.Equal(0, TextSearch.Step(3, 2, 1));
        Assert.Equal(2, TextSearch.Step(3, 0, -1));
        Assert.Equal(-1, TextSearch.Step(0, -1, 1));
    }

    [Fact]
    public void TheCountIsSaidTheWayASearchBoxSaysIt()
    {
        Assert.Equal("1 of 17", TextSearch.Describe(17, 0));
        Assert.Equal("3 of 17", TextSearch.Describe(17, 2));
        Assert.Equal("no matches", TextSearch.Describe(0, -1));
    }
}
