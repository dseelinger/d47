using D47.App.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The HID reader keeps each axis's report id, link collection, usage and range, and counts a move only when a value
/// differs from the last one that counted by more than 1/32 of the range (#2560).
/// </summary>
public class AStickAxisMoveIsCountedOnTheReaderThreadTests
{
    private const string Col01 = @"\\?\HID#VID_231D&PID_0200&Col01#8&2b3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    private static HidValueCaps Value(
        ushort first, ushort last, byte report = 1, ushort collection = 0, int min = 0, int max = 3200, ushort bits = 16) =>
        new(HidLayout.GenericDesktop, report, false, collection, first, last, min, max, bits);

    private static AxisMoves OneAxis(int min = 0, int max = 3200, ushort bits = 16) =>
        new([new HidAxis(1, 0, 0x30, min, max, bits)]);

    [Fact]
    public void AStickWithXYAndAHatListsTwoAxesWithTheirRangesAndOneHat()
    {
        var layout = HidLayout.From(
            [],
            [
                Value(0x30, 0x31, report: 2, collection: 1, max: 4095),
                Value(HidLayout.HatSwitch, HidLayout.HatSwitch, report: 2, collection: 1, max: 7, bits: 4),
            ]);

        Assert.Equal(
            [new HidAxis(2, 1, 0x30, 0, 4095, 16), new HidAxis(2, 1, 0x31, 0, 4095, 16)],
            layout.Axes);
        Assert.Equal(2, layout.AxisCount);
        Assert.Equal(new HidHat(2, 1, 0, 7), Assert.Single(layout.Hats));
    }

    [Fact]
    public void AnEmptyLogicalRangeIsTheFieldsUnsignedRange()
    {
        var layout = HidLayout.From([], [Value(0x30, 0x30, min: 0, max: -1, bits: 16)]);

        Assert.Equal(new HidAxis(1, 0, 0x30, 0, 65535, 16), Assert.Single(layout.Axes));
    }

    [Fact]
    public void TheFirstValueSetsThePositionAndIsNotAMove()
    {
        var moves = OneAxis();

        moves.Take(0, 2900);

        Assert.Equal(0, moves.Count);
    }

    [Fact]
    public void AChangeOfAThirtySecondOfTheRangeOrLessIsNotAMove()
    {
        var moves = OneAxis();
        moves.Take(0, 1600);

        moves.Take(0, 1700);
        moves.Take(0, 1500);
        Assert.Equal(0, moves.Count);

        moves.Take(0, 1701);
        Assert.Equal(1, moves.Count);
    }

    [Fact]
    public void DriftThatAddsUpPastAThirtySecondCountsOnce()
    {
        var moves = OneAxis();
        moves.Take(0, 1600);

        for (var value = 1620; value <= 1760; value += 20)
        {
            moves.Take(0, value);
        }

        Assert.Equal(1, moves.Count);
    }

    [Fact]
    public void AThrottleAtRestAwayFromCentreIsNotAMove()
    {
        var moves = OneAxis();

        for (var report = 0; report < 50; report++)
        {
            moves.Take(0, report % 2 == 0 ? 2900 : 2903);
        }

        Assert.Equal(0, moves.Count);
    }

    [Fact]
    public void AValueOutsideTheRangeIsIgnored()
    {
        var moves = OneAxis();
        moves.Take(0, 1600);

        moves.Take(0, 60_000);
        moves.Take(0, 1600);

        Assert.Equal(0, moves.Count);
    }

    [Fact]
    public void ACentreZeroAxisJitteringAcrossZeroIsNotAMove()
    {
        var axis = new HidAxis(1, 0, 0x30, -32768, 32767, 16);
        var moves = new AxisMoves([axis]);

        foreach (var raw in new uint[] { 0, 0xFFFF, 1, 0xFFFE, 0 })
        {
            moves.Take(0, HidLayout.Signed(raw, axis));
        }

        Assert.Equal(-1, HidLayout.Signed(0xFFFF, axis));
        Assert.Equal(0, moves.Count);
    }

    [Fact]
    public void AnAxisInAReportWithNoButtonAndNoHatIsRead()
    {
        var layout = HidLayout.From(
            [new HidButtonCaps(HidLayout.ButtonPage, 1, false, 1, 32)],
            [Value(0x30, 0x30, report: 3, max: 255, bits: 8)]);
        var reports = new HidReports(layout.Needed);
        var moves = new AxisMoves(layout.Axes);

        Assert.DoesNotContain((byte)3, layout.Needed);
        Assert.True(reports.Store([1, 0]));
        Assert.True(reports.Store([3, 10]));
        Assert.Equal(0, moves.Read(reports, FirstByte));

        Assert.True(reports.Store([3, 200]));
        Assert.Equal(1, moves.Read(reports, FirstByte));
        Assert.Equal(1, moves.Read(reports, FirstByte));
    }

    [Fact]
    public void TheCountReachesTheReading()
    {
        var devices = new FakeDevices();
        var now = TimeSpan.Zero;
        using var reader = new HidControllers(NullLogger<HidControllers>.Instance, devices, () => now);

        _ = reader.IsSettled;
        devices.Send(new HidState([false], []) { AxisMoves = 3 });
        now = TimeSpan.FromSeconds(2);
        Assert.Equal(3, Assert.Single(reader.Poll()).AxisMoves);

        devices.Send(new HidState([true], []) { AxisMoves = 4 });
        Assert.Equal(4, Assert.Single(reader.Poll()).AxisMoves);
    }

    private static uint? FirstByte(HidAxis axis, byte[] report) => report[1];

    private sealed class FakeDevices : IHidDevices
    {
        private static readonly HidDevice Device = new(
            1, Col01, 0x231D, 0x0200, HidLayout.From([new HidButtonCaps(HidLayout.ButtonPage, 1, false, 1, 1)], []));

        private Action<HidState>? _state;

        public IReadOnlyList<HidDevice> List() => [Device];

        public IDisposable Read(HidDevice device, Action<HidState> state, Action<string> ended)
        {
            _state = state;
            return new Stop();
        }

        public void Send(HidState state) => _state!(state);

        private sealed class Stop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
