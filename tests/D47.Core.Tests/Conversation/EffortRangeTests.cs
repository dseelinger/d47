using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The floor and the ceiling (Phase 54). The router's own answer is untouched by any
/// of this — <see cref="EffortRouterTests"/> stays as it was, which is the evidence the change
/// is additive.
/// </summary>
public class EffortRangeTests
{
    /// <summary>
    /// Decision 3 of the phase, asserted rather than assumed: a settings file written before
    /// the bounds existed behaves exactly as it did.
    /// </summary>
    [Theory]
    [InlineData(ThinkingEffort.Low)]
    [InlineData(ThinkingEffort.Medium)]
    [InlineData(ThinkingEffort.High)]
    [InlineData(ThinkingEffort.Xhigh)]
    [InlineData(ThinkingEffort.Max)]
    public void BothBoundsUnsetChangesNothing(ThinkingEffort chosen)
    {
        Assert.Equal(chosen, ThinkingEffortRange.Clamp(chosen, floor: null, ceiling: null));
    }

    [Fact]
    public void TheFloorLiftsWhatTheRouterAskedFor()
    {
        Assert.Equal(
            ThinkingEffort.High,
            ThinkingEffortRange.Clamp(ThinkingEffort.Low, ThinkingEffort.High, ceiling: null));
    }

    [Fact]
    public void TheFloorLeavesAnythingAboveItAlone()
    {
        Assert.Equal(
            ThinkingEffort.Max,
            ThinkingEffortRange.Clamp(ThinkingEffort.Max, ThinkingEffort.High, ceiling: null));
    }

    [Fact]
    public void TheCeilingLowersWhatTheRouterAskedFor()
    {
        Assert.Equal(
            ThinkingEffort.Medium,
            ThinkingEffortRange.Clamp(ThinkingEffort.Max, floor: null, ThinkingEffort.Medium));
    }

    [Fact]
    public void EqualBoundsPinEveryTurnToTheOneRung()
    {
        foreach (var chosen in ThinkingEffortRange.Ladder)
        {
            Assert.Equal(
                ThinkingEffort.Medium,
                ThinkingEffortRange.Clamp(chosen, ThinkingEffort.Medium, ThinkingEffort.Medium));
        }
    }

    /// <summary>
    /// <c>Math.Clamp</c> throws when the minimum exceeds the maximum, and a hand-edited
    /// settings file can say exactly that. Core must never throw on a settings file.
    /// </summary>
    [Fact]
    public void AFloorAboveTheCeilingDoesNotThrow()
    {
        var pinned = ThinkingEffortRange.Clamp(ThinkingEffort.Low, ThinkingEffort.Max, ThinkingEffort.Low);

        Assert.Equal(ThinkingEffort.Low, pinned);
        Assert.Equal(
            ThinkingEffort.Max,
            ThinkingEffortRange.Clamp(ThinkingEffort.Max, ThinkingEffort.Max, ThinkingEffort.Low));
    }

    /// <summary>
    /// The rows and the clamp read one ladder. If these ever disagree the picker offers a rung
    /// the clamp cannot reach, which is the failure the shared static exists to prevent.
    /// </summary>
    [Fact]
    public void TheLadderIsTheEnumsDeclarationOrder()
    {
        Assert.Equal(
            [
                ThinkingEffort.Low,
                ThinkingEffort.Medium,
                ThinkingEffort.High,
                ThinkingEffort.Xhigh,
                ThinkingEffort.Max,
            ],
            ThinkingEffortRange.Ladder);

        Assert.Equal(["Low", "Medium", "High", "Xhigh", "Max"], ThinkingEffortRange.Names);
    }

    [Fact]
    public void ARowValueRoundTripsThroughItsName()
    {
        foreach (var effort in ThinkingEffortRange.Ladder)
        {
            Assert.Equal(effort, ThinkingEffortRange.Parse(ThinkingEffortRange.Name(effort)));
        }
    }

    /// <summary>
    /// A number is not a rung. <c>Enum.TryParse</c> accepts one and would hand the clamp a
    /// value outside the ladder; a hand-edited settings file is where that comes from.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("enormous")]
    [InlineData("7")]
    [InlineData("-1")]
    public void TextThatNamesNoRungParsesToNothing(string? value)
    {
        Assert.Null(ThinkingEffortRange.Parse(value));
    }

    [Fact]
    public void ARowValueIsReadWhateverItsCase()
    {
        Assert.Equal(ThinkingEffort.Xhigh, ThinkingEffortRange.Parse("xhigh"));
        Assert.Equal(ThinkingEffort.Xhigh, ThinkingEffortRange.Parse("XHIGH"));
    }

    /// <summary>
    /// Each row truncates against the other bound, so the picker cannot offer a floor above the
    /// ceiling in the first place. The clamp still handles it, because a settings file is not
    /// written only by the picker.
    /// </summary>
    [Fact]
    public void EachRowOffersOnlyTheRungsTheOtherBoundAllows()
    {
        Assert.Equal(["High", "Xhigh", "Max"], ThinkingEffortRange.NamesFrom(ThinkingEffort.High));
        Assert.Equal(["Low", "Medium", "High"], ThinkingEffortRange.NamesUpTo(ThinkingEffort.High));

        Assert.Equal(ThinkingEffortRange.Names, ThinkingEffortRange.NamesFrom(ThinkingEffortRange.Lowest));
        Assert.Equal(ThinkingEffortRange.Names, ThinkingEffortRange.NamesUpTo(ThinkingEffortRange.Highest));
    }
}
