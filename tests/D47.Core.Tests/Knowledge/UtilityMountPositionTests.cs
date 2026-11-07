using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AUtilityMountSaysWhereItSitsTests
{
    private static ShipSlot Mount(MountHeight? height, MountLength? length) =>
        new("anaconda", "TinyHardpoint3", ShipSlotKind.Utility, 0, [], height, length);

    [Fact]
    public void BothPositionsShowInTheNameAndTheColumn()
    {
        var slot = Mount(MountHeight.Top, MountLength.Fore);

        Assert.Equal("Utility Mount 3 (top, fore)", slot.Describe());
        Assert.Equal("3 (top, fore)", slot.Short());
    }

    [Fact]
    public void OnePositionShowsAlone()
    {
        Assert.Equal("Utility Mount 3 (bottom)", Mount(MountHeight.Bottom, null).Describe());
        Assert.Equal("3 (aft)", Mount(null, MountLength.Aft).Short());
    }

    [Fact]
    public void AMountWithNoRowReadsAsItAlwaysDid()
    {
        var slot = Mount(null, null);

        Assert.Equal("Utility Mount 3", slot.Describe());
        Assert.Equal("3", slot.Short());
    }

    [Fact]
    public void ARowOnlyChangesAUtilityMount()
    {
        var hardpoint = new ShipSlot("anaconda", "LargeHardpoint1", ShipSlotKind.Hardpoint, 4, []);

        Assert.Equal("Large Hardpoint 1", hardpoint.Describe());
    }
}
