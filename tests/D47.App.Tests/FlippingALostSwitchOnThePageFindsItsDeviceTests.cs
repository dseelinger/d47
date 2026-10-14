using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A switch flipped on the HOTAS switches page finds its device's new id and writes it at once, leaving the page's
/// unsaved edits unsaved (#1157).
/// </summary>
[Trait("Category", "Integration")]
public class FlippingALostSwitchOnThePageFindsItsDeviceTests
{
    private const string Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes";

    private static readonly DateTimeOffset Instant = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static SwitchMapping Switch(string name, int first, int second) => new()
    {
        Name = name,
        DeviceId = "old-block",
        Device = Device,
        Positions =
        [
            new SwitchPosition(first, "landing_gear", DesiredState.On),
            new SwitchPosition(second, "landing_gear", DesiredState.Off),
        ],
    };

    private static HotasReading Holding(params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = "new-block", VendorId = 0x4098, ProductId = 0xBD65, Buttons = buttons };
    }

    private static void Sample(SwitchPage page, FakeHotasReader reader, SwitchReconciler reconciler, SwitchStore store, HotasReading reading)
    {
        reader.Set(reading);

        reconciler.Poll(
            new SwitchTick
            {
                Now = Instant,
                Readings = reader.Poll(),
                Status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = Instant },
                Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
                Enabled = false,
            },
            store.Switches);

        page.Sample();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void OneFlipRepointsBothSwitchesAndTheRenameStaysUnsaved()
    {
        using var look = AppLook.Put();

        var store = new SwitchStore("switches.json", new MemoryFileSystem(), NullLogger<SwitchStore>.Instance);
        store.Save([Switch("LDG GEAR", 8, 9), Switch("LIGHTS", 10, 11)]);
        store.Poll();

        var reader = new FakeHotasReader();
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var page = new SwitchPage(store, reader, reconciler, () => Instant, "capture.txt", []);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 820, Height = 640 };

        window.Show();
        _ = panel.Open(page);
        Dispatcher.UIThread.RunJobs();

        Sample(page, reader, reconciler, store, Holding(8, 10));
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsVisible && block.Text?.Contains("Flip it on the HOTAS switches page", StringComparison.Ordinal) == true);

        // An edit the Commander has not saved.
        page.GetVisualDescendants().OfType<TextBox>().First(box => box.Text == "LIGHTS").Text = "CABIN LIGHTS";

        Sample(page, reader, reconciler, store, Holding(9, 10));

        Assert.Equal(["new-block", "new-block"], store.Switches.Select(mapping => mapping.DeviceId));
        Assert.Equal(["LDG GEAR", "LIGHTS"], store.Switches.Select(mapping => mapping.Name));

        var found = page.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsVisible && block.Text == "Found the device — 2 switches re-pointed.")
            .ToList();

        Assert.Equal(2, found.Count);
        Assert.DoesNotContain(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsVisible && block.Text?.Contains("VID 0x", StringComparison.Ordinal) == true);

        // The working copy took the new id, so saving the rename does not write the old one back.
        page.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Save")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["new-block", "new-block"], store.Switches.Select(mapping => mapping.DeviceId));
        Assert.Equal(["LDG GEAR", "CABIN LIGHTS"], store.Switches.Select(mapping => mapping.Name));

        window.Close();
    }

    [AvaloniaFact]
    public void PushToTalkAndCancelOnTheOldIdMoveWithTheSwitches()
    {
        using var look = AppLook.Put();

        var (settings, _, _) = TestSurface.Create();
        settings.Replace("test", current => current with
        {
            Listening = current.Listening with { PushToTalkButton = "old-block#4" },
            Speech = current.Speech with { CancelButton = "old-block#5" },
        });

        var changed = new List<string>();
        settings.Changed += change => changed.Add(change.Key);

        var store = new SwitchStore("switches.json", new MemoryFileSystem(), NullLogger<SwitchStore>.Instance);
        store.Save([Switch("LDG GEAR", 8, 9), Switch("LIGHTS", 10, 11)]);
        store.Poll();

        var reader = new FakeHotasReader();
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var page = new SwitchPage(store, reader, reconciler, () => Instant, "capture.txt", [], settings);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 820, Height = 640 };

        window.Show();
        _ = panel.Open(page);
        Dispatcher.UIThread.RunJobs();

        Sample(page, reader, reconciler, store, Holding(8, 10));
        Sample(page, reader, reconciler, store, Holding(9, 10));

        Assert.Equal("new-block#4", settings.Current.Listening.PushToTalkButton);
        Assert.Equal("new-block#5", settings.Current.Speech.CancelButton);

        // The change reaches the listener's rebind through the listening fan-out.
        Assert.Contains(changed, key => Core.Configuration.SettingsFanout.For(key).Subsystem == Core.Configuration.SettingsSubsystem.Listening);

        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsVisible && block.Text == "Found the device — 2 switches, push-to-talk and cancel re-pointed.");

        window.Close();
    }
}
