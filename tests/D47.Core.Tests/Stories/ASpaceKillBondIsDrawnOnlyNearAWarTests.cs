using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Stories;

public sealed class ASpaceKillBondIsDrawnOnlyNearAWarTests
{
    private static readonly AdventureTrigger Bonds = new() { Kind = TriggerKind.Bond, Count = 3 };

    private static bool Drawable(bool warInReach) =>
        ChapterDraw.Drawable(
            ChapterActivities.All.First(row => row.Kind == TriggerKind.Bond),
            (ActivityGate)255,
            stationsInReach: true,
            refused: null,
            warInReach: warInReach);

    [Fact]
    public void TheBondRowIsRefusedWithNoWarInReach() => Assert.False(Drawable(false));

    [Fact]
    public void TheBondRowIsDrawnWithAWarInReach() => Assert.True(Drawable(true));

    [Fact]
    public void TheBondHandOffSaysWhereBondsAreEarned() =>
        Assert.Contains(AdventureTrigger.BondWhere, Bonds.HandOff(), StringComparison.Ordinal);

    [Fact]
    public void TheNowLineSaysWhereBondsAreEarned()
    {
        var route = LanternRoute(Accepted);
        var adventure = route with { Beats = [Beat("The Count", "catalyst", Bonds, "Done.")], StoryId = "a-stock-story" };

        var now = AdventureContext.NowLine(AdventureFold.Start(adventure));

        Assert.Contains(AdventureTrigger.BondWhere, now, StringComparison.Ordinal);
    }
}
