using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AStateGatedMissionIsDrawnOnlyWithinReachTests
{
    private static DrawableActivity Row(string family) =>
        ChapterActivities.All.Single(row => row.Family == family);

    private static bool Drawable(string family, IReadOnlySet<string>? inReach) =>
        ChapterDraw.Drawable(Row(family), (ActivityGate)255, stationsInReach: true, refused: null, stateFamiliesInReach: inReach);

    [Fact]
    public void ARebootRowIsRefusedWhenNoFamilyIsInReach()
    {
        Assert.False(Drawable("Mission_OnFoot_Reboot", new HashSet<string>()));
    }

    [Fact]
    public void ARebootRowIsDrawnWhenItsFamilyIsInReach()
    {
        Assert.True(Drawable("Mission_OnFoot_Reboot", new HashSet<string> { "Mission_OnFoot_Reboot" }));
    }

    [Fact]
    public void ASalvageRowIsDrawnWhateverIsInReach()
    {
        Assert.True(Drawable("Mission_Salvage", new HashSet<string>()));
    }

    [Fact]
    public void NoReachListLeavesARebootRowDrawable()
    {
        Assert.True(Drawable("Mission_OnFoot_Reboot", null));
    }
}
