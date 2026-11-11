using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class AnOutfittingItemKnowsItsSlotKindTests
{
    private static ShipSlotKind? KindOf(string text)
    {
        Assert.True(OutfittingItem.TryParse(text, out var item, out var failure), failure);

        return item.SlotKind;
    }

    [Theory]
    [InlineData("4A Power Plant")]
    [InlineData("4A Frame Shift Drive (SCO)")]
    [InlineData("3D Sensors")]
    public void ACoreModuleIsCore(string text) => Assert.Equal(ShipSlotKind.Core, KindOf(text));

    [Theory]
    [InlineData("4A Shield Generator")]
    [InlineData("3A Collector Limpet Controller")]
    [InlineData("5H Guardian FSD Booster")]
    [InlineData("4E Cargo Rack")]
    [InlineData("Fuel Scoop")]
    [InlineData("Fuel Tank")]
    public void AnOptionalModuleIsOptional(string text) => Assert.Equal(ShipSlotKind.Optional, KindOf(text));

    [Fact]
    public void AWeaponIsAHardpointModule() => Assert.Equal(ShipSlotKind.Hardpoint, KindOf("2D Beam Laser"));

    [Fact]
    public void AUtilityModuleIsUtility() => Assert.Equal(ShipSlotKind.Utility, KindOf("0I Point Defence"));

    [Fact]
    public void AShipHasNoSlotKind() => Assert.Null(KindOf("Python Mk II"));
}
