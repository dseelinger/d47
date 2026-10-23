using D47.App.Input;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A HID collection's buttons, hats and axes are counted so that <c>HotasReading.Describe()</c> gives the text
/// the <c>RawGameController</c> reader gave, which stored switches match on (#1512).
/// </summary>
public class AHidCollectionIsCountedAsWindowsCountedItTests
{
    private static HidButtonCaps Buttons(byte report, ushort first, ushort last) =>
        new(HidLayout.ButtonPage, report, false, first, last);

    private static HidValueCaps Value(ushort page, ushort first, ushort last, byte report = 1, int min = 0, int max = 65535) =>
        new(page, report, false, 0, first, last, min, max);

    [Fact]
    public void AThrottleBlockWithAVendorValueHasSevenAxes()
    {
        var layout = HidLayout.From(
            [Buttons(1, 1, 32)],
            [Value(HidLayout.GenericDesktop, 0x30, 0x36), Value(0xFF00, 0x01, 0x01)]);

        Assert.Equal((32, 0, 7), (layout.ButtonCount, layout.Hats.Count, layout.AxisCount));
    }

    [Fact]
    public void AStickWithAHatCountsTheHatApartFromTheAxes()
    {
        var layout = HidLayout.From(
            [Buttons(0, 1, 21)],
            [Value(HidLayout.GenericDesktop, 0x30, 0x35), Value(HidLayout.GenericDesktop, 0x36, 0x36), Value(HidLayout.GenericDesktop, HidLayout.HatSwitch, HidLayout.HatSwitch, max: 7)]);

        Assert.Equal((21, 1, 7), (layout.ButtonCount, layout.Hats.Count, layout.AxisCount));
    }

    [Fact]
    public void PedalsHaveAxesAndNothingElse()
    {
        var layout = HidLayout.From([], [Value(HidLayout.GenericDesktop, 0x30, 0x32)]);

        Assert.Equal((0, 0, 3), (layout.ButtonCount, layout.Hats.Count, layout.AxisCount));
        Assert.Empty(layout.Needed);
    }

    [Fact]
    public void AnAliasedUsageIsCountedOnce()
    {
        var layout = HidLayout.From(
            [Buttons(1, 1, 8), new HidButtonCaps(HidLayout.ButtonPage, 1, true, 1, 8)],
            [Value(HidLayout.GenericDesktop, 0x30, 0x30), new HidValueCaps(HidLayout.GenericDesktop, 1, true, 0, 0x31, 0x31, 0, 255)]);

        Assert.Equal((8, 1), (layout.ButtonCount, layout.AxisCount));
    }

    [Fact]
    public void ButtonIndexIsTheUsageLessOne()
    {
        var buttons = new bool[4];

        HidLayout.Hold(buttons, [1, 4, 5, 0]);

        Assert.Equal([true, false, false, true], buttons);
    }

    [Theory]
    [InlineData(0, 0, 7, 1)]
    [InlineData(2, 0, 7, 3)]
    [InlineData(7, 0, 7, 8)]
    [InlineData(8, 0, 7, 0)]
    [InlineData(15, 0, 7, 0)]
    [InlineData(1, 1, 8, 1)]
    [InlineData(0, 1, 8, 0)]
    [InlineData(1, 0, 3, 3)]
    [InlineData(3, 0, 3, 7)]
    public void AHatValueIsTheSwitchPositionNumber(long value, int min, int max, int position) =>
        Assert.Equal(position, HidLayout.Position(value, new HidHat(1, 0, min, max)));

    [Fact]
    public void AReadingWaitsForEveryReportThatCarriesButtonsOrHats()
    {
        var layout = HidLayout.From(
            [Buttons(1, 1, 32)],
            [Value(HidLayout.GenericDesktop, HidLayout.HatSwitch, HidLayout.HatSwitch, report: 2, max: 7), Value(0xFF00, 1, 1, report: 3)]);
        var reports = new HidReports(layout.Needed);

        Assert.False(reports.Store([3, 0]));
        Assert.False(reports.Store([1, 0]));
        Assert.True(reports.Store([2, 0]));
    }
}
