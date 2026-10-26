using D47.App.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The HID reader counts each press as its reports arrive, so a press between two polls is not lost (#1617).</summary>
public class AStickPressBetweenTwoPollsIsCountedTests
{
    private const string Col01 = @"\\?\HID#VID_4098&PID_BD65&Col01#8&1a2b3c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    private static readonly HidLayout Block = HidLayout.From([new HidButtonCaps(HidLayout.ButtonPage, 1, false, 1, 32)], []);

    private TimeSpan _now;

    private static HidState Holding(params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HidState(buttons, []);
    }

    private HidControllers Settled(FakeDevices devices)
    {
        var reader = new HidControllers(NullLogger<HidControllers>.Instance, devices, () => _now);

        _ = reader.IsSettled;
        devices.Send(Holding());
        _now = TimeSpan.FromSeconds(2);

        return reader;
    }

    [Fact]
    public void ADownAndAnUpReportBetweenTwoPollsCountOnePress()
    {
        var devices = new FakeDevices();
        using var reader = Settled(devices);

        Assert.Equal(0, Assert.Single(reader.Poll()).PressCount(7));

        devices.Send(Holding(7));
        devices.Send(Holding());

        var reading = Assert.Single(reader.Poll());
        Assert.False(reading.IsHeld(7));
        Assert.Equal(1, reading.PressCount(7));
    }

    [Fact]
    public void TheFirstReportCountsNothing()
    {
        var devices = new FakeDevices();
        using var reader = new HidControllers(NullLogger<HidControllers>.Instance, devices, () => _now);

        _ = reader.IsSettled;
        devices.Send(Holding(7));
        _now = TimeSpan.FromSeconds(2);

        Assert.Equal(0, Assert.Single(reader.Poll()).PressCount(7));
    }

    [Fact]
    public void AButtonHeldAcrossReportsCountsOnce()
    {
        var devices = new FakeDevices();
        using var reader = Settled(devices);

        devices.Send(Holding(7));
        devices.Send(Holding(7, 8));
        devices.Send(Holding(7));

        Assert.Equal(1, Assert.Single(reader.Poll()).PressCount(7));
    }

    [Fact]
    public void EachReadingHasItsOwnCopyOfTheCounts()
    {
        var devices = new FakeDevices();
        using var reader = Settled(devices);

        var first = Assert.Single(reader.Poll());
        var second = Assert.Single(reader.Poll());

        Assert.NotSame(first.Presses, second.Presses);
    }

    private sealed class FakeDevices : IHidDevices
    {
        private static readonly HidDevice Device = new(1, Col01, 0x4098, 0xBD65, Block);

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
