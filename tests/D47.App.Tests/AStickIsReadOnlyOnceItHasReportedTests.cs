using D47.App.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The HID reader settles on the device list and on each collection's first report, so a collection is never
/// read as "nothing held" before its first report (#1512).
/// </summary>
public class AStickIsReadOnlyOnceItHasReportedTests
{
    private const string Col01 = @"\\?\HID#VID_4098&PID_BD65&Col01#8&1a2b3c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string Col02 = @"\\?\HID#VID_4098&PID_BD65&Col02#8&1a2b3c&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    private static readonly HidLayout Block = HidLayout.From([new HidButtonCaps(HidLayout.ButtonPage, 1, false, 1, 32)], []);

    private TimeSpan _now;

    private HidControllers Reader(FakeDevices devices) =>
        new(NullLogger<HidControllers>.Instance, devices, () => _now);

    private static HidDevice Device(string path, nint handle = 1) => new(handle, path, 0x4098, 0xBD65, Block);

    private static HidState Holding(params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HidState(buttons, []);
    }

    [Fact]
    public void TheListMustHoldStillBeforeAnythingIsRead()
    {
        var devices = new FakeDevices(Device(Col01));
        using var reader = Reader(devices);

        Assert.False(reader.IsSettled);
        devices.Send(Col01, Holding(3));

        _now = TimeSpan.FromMilliseconds(1000);
        Assert.False(reader.IsSettled);
        Assert.Empty(reader.Poll());

        _now = TimeSpan.FromMilliseconds(1600);
        Assert.True(reader.IsSettled);
        Assert.Equal([3], Assert.Single(reader.Poll()).Held());
    }

    [Fact]
    public void ACollectionWithNoReportHoldsTheSettleUntilItGivesUp()
    {
        var devices = new FakeDevices(Device(Col01), Device(Col02, 2));
        using var reader = Reader(devices);

        _ = reader.IsSettled;
        devices.Send(Col01, Holding(3));

        _now = TimeSpan.FromSeconds(3);
        Assert.False(reader.IsSettled);

        _now = TimeSpan.FromSeconds(6.5);
        Assert.True(reader.IsSettled);

        var reading = Assert.Single(reader.Poll());
        Assert.Equal("hid:VID_4098&PID_BD65&COL01:b32", reading.Id);
    }

    [Fact]
    public void ACollectionThatReportsLateIsReadFromItsReport()
    {
        var devices = new FakeDevices(Device(Col01), Device(Col02, 2));
        using var reader = Reader(devices);

        _ = reader.IsSettled;
        devices.Send(Col01, Holding(3));
        _now = TimeSpan.FromSeconds(7);
        _ = reader.IsSettled;

        devices.Send(Col02, Holding(25));

        Assert.Contains(reader.Poll(), reading => reading.Id.EndsWith("COL02:b32", StringComparison.Ordinal) && reading.IsHeld(25));
    }

    [Fact]
    public void ACollectionThatCannotBeOpenedIsLeftOutAndIsNotAFault()
    {
        var devices = new FakeDevices(Device(Col01), Device(Col02, 2));
        using var reader = Reader(devices);

        _ = reader.IsSettled;
        devices.Send(Col01, Holding());
        devices.End(Col02);

        _now = TimeSpan.FromSeconds(2);
        Assert.True(reader.IsSettled);
        Assert.Single(reader.Poll());
        Assert.Null(reader.Fault);
        Assert.Null(reader.Unavailable);
    }

    [Fact]
    public void AReplugStartsTheCollectionAgainAndWaitsForItsReport()
    {
        var devices = new FakeDevices(Device(Col01));
        using var reader = Reader(devices);

        _ = reader.IsSettled;
        devices.Send(Col01, Holding(3));
        _now = TimeSpan.FromSeconds(2);
        Assert.Single(reader.Poll());

        devices.Listed = [Device(Col01, 9)];
        _now = TimeSpan.FromSeconds(2.1);
        Assert.Empty(reader.Poll());

        _now = TimeSpan.FromSeconds(4);
        Assert.False(reader.IsSettled);

        devices.Send(Col01, Holding(4));
        Assert.Equal([4], Assert.Single(reader.Poll()).Held());
        Assert.True(devices.Stopped);
    }

    [Fact]
    public void NoControllersIsSaidOnceTheWaitIsOver()
    {
        using var reader = Reader(new FakeDevices());

        Assert.False(reader.IsSettled);
        Assert.Null(reader.Unavailable);

        _now = TimeSpan.FromSeconds(7);
        Assert.True(reader.IsSettled);
        Assert.NotNull(reader.Unavailable);
        Assert.Null(reader.Fault);
    }

    [Fact]
    public void AskingWhyNothingIsReadDoesNotListTheControllers()
    {
        var devices = new FakeDevices();
        using var reader = Reader(devices);

        _now = TimeSpan.FromSeconds(7);
        _ = reader.IsSettled;
        var lists = devices.Lists;

        Assert.NotNull(reader.Unavailable);
        Assert.Equal(lists, devices.Lists);
    }

    [Fact]
    public void ARawInputFailureIsAFault()
    {
        using var reader = Reader(new FakeDevices { Throws = true });

        Assert.True(reader.IsSettled);
        Assert.NotNull(reader.Fault);
        Assert.Equal(reader.Fault, reader.Unavailable);
        Assert.Empty(reader.Poll());
    }

    private sealed class FakeDevices(params HidDevice[] listed) : IHidDevices
    {
        private readonly Dictionary<nint, (Action<HidState> State, Action<string> Ended)> _readers = new();

        public IReadOnlyList<HidDevice> Listed { get; set; } = listed;

        public bool Throws { get; init; }

        public bool Stopped { get; private set; }

        public int Lists { get; private set; }

        public IReadOnlyList<HidDevice> List()
        {
            Lists++;
            return Throws ? throw new DllNotFoundException("hid.dll") : Listed;
        }

        public IDisposable Read(HidDevice device, Action<HidState> state, Action<string> ended)
        {
            _readers[device.Handle] = (state, ended);
            return new Stop(() => Stopped = true);
        }

        public void Send(string path, HidState state) => _readers[Handle(path)].State(state);

        public void End(string path) => _readers[Handle(path)].Ended("could not be opened (error 5)");

        private nint Handle(string path) => Listed.Single(device => device.Path == path).Handle;

        private sealed class Stop(Action stop) : IDisposable
        {
            public void Dispose() => stop();
        }
    }
}
