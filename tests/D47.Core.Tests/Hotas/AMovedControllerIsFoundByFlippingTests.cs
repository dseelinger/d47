using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>Flipping a switch whose device has a new id points the switches learned on it at that id (#1157).</summary>
public class AMovedControllerIsFoundByFlippingTests
{
    private const string Throttle = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static HotasReading On(string id, params int[] held) => Sized(id, 32, held);

    private static HotasReading Sized(string id, int count, params int[] held)
    {
        var buttons = new bool[count];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = id, VendorId = 0x4098, ProductId = 0xBD65, Buttons = buttons };
    }

    private static SwitchMapping Switch(string name, string id, int first, int second, string device = Throttle) => new()
    {
        Name = name,
        DeviceId = id,
        Device = device,
        Positions =
        [
            new SwitchPosition(first, "landing_gear", DesiredState.On),
            new SwitchPosition(second, "landing_gear", DesiredState.Off),
        ],
    };

    /// <summary>Gear and lights learned on A, a hook on C; A has gone and B has appeared.</summary>
    private static List<SwitchMapping> Stored() =>
    [
        Switch("gear", "A", 8, 9),
        Switch("lights", "A", 10, 11),
        Switch("hook", "C", 2, 3),
    ];

    private static FindResult Feed(LostDeviceFinder finder, IReadOnlyList<SwitchMapping> stored, FakeHotasReader reader) =>
        finder.Poll(reader.Poll(), stored);

    [Fact]
    public void AFlipOnTheNewIdPointsEverySwitchFromTheOldIdThereAndLeavesTheRest()
    {
        var finder = new LostDeviceFinder();
        var stored = Stored();
        var reader = new FakeHotasReader();

        Assert.Null(Feed(finder, stored, reader.Set(On("B", 8, 10), On("C", 2))).Found);

        var found = Feed(finder, stored, reader.Set(On("B", 9, 10), On("C", 2))).Found;

        Assert.NotNull(found);
        Assert.Equal("A", found.OldId);
        Assert.Equal("B", found.NewId);

        var moved = found.Apply(stored);

        Assert.Equal(["B", "B", "C"], moved.Select(mapping => mapping.DeviceId));
        Assert.Equal(2, found.Count(stored));
        Assert.Equal($"Found on {Throttle} — 2 switches re-pointed.", found.Says(2));
    }

    [Fact]
    public void AFrameBetweenTwoPositionsIsNotARelease()
    {
        var finder = new LostDeviceFinder();
        var stored = Stored();
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(On("B", 8), On("C", 2)));

        // Neither of the gear switch's buttons is held for one poll as it travels.
        Assert.Null(Feed(finder, stored, reader.Set(On("B"), On("C", 2))).Found);
        Assert.NotNull(Feed(finder, stored, reader.Set(On("B", 9), On("C", 2))).Found);
    }

    [Fact]
    public void AnUnsettledReaderIsIgnoredRatherThanReadAsEverythingLetGo()
    {
        var finder = new LostDeviceFinder();
        var stored = Stored();
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(On("B", 8), On("C", 2)));

        reader.IsSettled = false;
        Assert.Same(FindResult.None, Feed(finder, stored, reader));

        reader.IsSettled = true;
        Assert.Same(FindResult.None, Feed(finder, stored, reader.Set(On("B", 8), On("C", 2))));
        Assert.NotNull(Feed(finder, stored, reader.Set(On("B", 9), On("C", 2))).Found);
    }

    [Fact]
    public void ADeviceThatDescribesItselfDifferentlyIsRefusedAsA4x32Change()
    {
        var finder = new LostDeviceFinder();
        var stored = Stored();
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(Sized("B", 128, 8), On("C", 2)));
        var result = Feed(finder, stored, reader.Set(Sized("B", 128, 9), On("C", 2)));

        Assert.Null(result.Found);
        Assert.Contains("4x32", result.Refused["gear"], StringComparison.Ordinal);
    }

    [Fact]
    public void AFlipThatMovesAButtonTheSwitchDoesNotStoreIsRefused()
    {
        var finder = new LostDeviceFinder();
        var stored = Stored();
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(On("B", 8), On("C", 2)));
        var result = Feed(finder, stored, reader.Set(On("B", 9, 20), On("C", 2)));

        Assert.Null(result.Found);
        Assert.Contains("button 20", result.Refused["gear"], StringComparison.Ordinal);
    }

    [Fact]
    public void ADeviceAWorkingSwitchUsesIsNotTheMissingOne()
    {
        var finder = new LostDeviceFinder();
        var stored = Stored();
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(On("C", 2, 8)));
        var result = Feed(finder, stored, reader.Set(On("C", 2, 9)));

        Assert.Null(result.Found);
        Assert.Contains("hook", result.Refused["gear"], StringComparison.Ordinal);
    }

    [Fact]
    public void AFlipThatFitsTwoMissingDevicesChangesNothing()
    {
        var finder = new LostDeviceFinder();
        List<SwitchMapping> stored = [Switch("gear", "A", 8, 9), Switch("cargo", "D", 8, 9)];
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(On("B", 8)));
        var result = Feed(finder, stored, reader.Set(On("B", 9)));

        Assert.Null(result.Found);
        Assert.Contains("Flip a different switch", result.Refused["gear"], StringComparison.Ordinal);
        Assert.Contains("Flip a different switch", result.Refused["cargo"], StringComparison.Ordinal);
    }

    [Fact]
    public void ASwitchWithNoDeviceTextMovesAloneAndTakesTheDevicesText()
    {
        var finder = new LostDeviceFinder();
        List<SwitchMapping> stored = [Switch("gear", "A", 8, 9, device: string.Empty), Switch("lights", "A", 10, 11)];
        var reader = new FakeHotasReader();

        Feed(finder, stored, reader.Set(On("B", 8)));
        var found = Feed(finder, stored, reader.Set(On("B", 9))).Found;

        Assert.NotNull(found);

        var moved = found.Apply(stored);

        Assert.Equal(["B", "A"], moved.Select(mapping => mapping.DeviceId));
        Assert.Equal(Throttle, moved[0].Device);
        Assert.Equal(1, found.Count(stored));
    }

    [Fact]
    public void TheSwitchFileChangesOnlyInTheDeviceId()
    {
        var files = new MemoryFileSystem();
        var store = new SwitchStore("switches.json", files, NullLogger<SwitchStore>.Instance);

        store.Save(Stored());
        store.Poll();

        var before = store.Switches;
        var found = new FoundDevice("A", "B", Throttle, "gear", Fills: false);

        store.Save(found.Apply(before));
        store.Poll();

        Assert.Equal(before.Count, store.Switches.Count);

        foreach (var (was, now) in before.Zip(store.Switches))
        {
            var expected = was.DeviceId == "A" ? was with { DeviceId = "B" } : was;

            Assert.Equal(expected.Name, now.Name);
            Assert.Equal(expected.DeviceId, now.DeviceId);
            Assert.Equal(expected.Device, now.Device);
            Assert.Equal(expected.Positions, now.Positions);
        }
    }

    [Fact]
    public void TheReconcilerPressesNothingOnFirstSeeingARepointedSwitch()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings = [new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L")],
        };

        SwitchTick Tick(HotasReading reading) => new()
        {
            Now = Start,
            Readings = [reading],
            Status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = Start },
            Binds = binds,
            Enabled = true,
        };

        var lost = Switch("gear", "A", 8, 9);

        // Gear is up, and the switch sits at gear down on the device's new id.
        reconciler.Poll(Tick(On("B", 8)), [lost]);
        Assert.Equal(SwitchHealth.Reassign, reconciler.States[0].Health);

        var repointed = new FoundDevice("A", "B", Throttle, "gear", Fills: false).Apply([lost]);

        reconciler.Poll(Tick(On("B", 8)), repointed);
        Assert.Equal(SwitchHealth.Ready, reconciler.States[0].Health);
        Assert.Empty(reconciler.Drain());

        reconciler.Poll(Tick(On("B", 9)), repointed);
        reconciler.Poll(Tick(On("B", 8)), repointed);
        Assert.Single(reconciler.Drain());
    }
}
