using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ChatterTopicsDifferBetweenLaunchesTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Removed =
    [
        "a pilot's licence exam",
        "a shift that will not end",
        "a manifest flagged for review",
        "a beacon out of sync",
    ];

    private static int FirstVariant(int offset)
    {
        var callout = new NpcChatterCallout(new NearbyFight())
        {
            Interval = TimeSpan.FromMinutes(20),
            Longest = TimeSpan.FromMinutes(20),
            Settle = TimeSpan.Zero,
            VariantOffset = offset,
        };

        CalloutContext At(DateTimeOffset now) =>
            new(now, false, null, GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip }, NavRoute.None, []);

        Assert.Empty(callout.Examine(At(T0)));
        var marker = Assert.Single(callout.Examine(At(T0 + TimeSpan.FromMinutes(21))));

        return marker.Variant!.Value;
    }

    [Fact]
    public void EveryChatterListHasAtLeastTwelveEntriesAndNoneOfTheOverusedOnes()
    {
        foreach (var list in new[] { NpcChatter.Topics, NpcChatter.Openings, NpcChatter.ControllerSubjects, NpcChatter.ControllerOpenings })
        {
            Assert.True(list.Length >= 12);
            Assert.Empty(list.Intersect(Removed));
            Assert.Equal(list.Length, list.Distinct().Count());
        }
    }

    [Fact]
    public void NoOffsetLeavesTheFirstExchangeAtVariantZero()
    {
        Assert.Equal(0, FirstVariant(0));
    }

    [Fact]
    public void AnOffsetMovesTheFirstExchangeToADifferentTopic()
    {
        Assert.Equal(12345, FirstVariant(12345));

        string TopicLine(int offset) => NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: FirstVariant(offset));

        Assert.NotEqual(TopicLine(0), TopicLine(12345));
    }

    [Fact]
    public void TheOverusedLinesAreBannedFromPassersbyChatter()
    {
        var text = NpcChatter.Instruction(NpcChatterKind.Passersby);

        Assert.Contains("is this shift ever going to end", text, StringComparison.Ordinal);
        Assert.Contains("I could be wrong", text, StringComparison.Ordinal);
    }
}
