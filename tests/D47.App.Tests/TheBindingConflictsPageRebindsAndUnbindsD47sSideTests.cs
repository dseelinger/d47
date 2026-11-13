using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Settings › Screens › Window › Binding conflicts, its REBIND, UNBIND and OPEN THE SWITCH (#2383).</summary>
[Trait("Category", "Integration")]
public class TheBindingConflictsPageRebindsAndUnbindsD47sSideTests
{
    private const string Throttle = "{wgi/nrid/throttle}";

    private const string BindsPath = @"C:\d47-test\page.binds";

    /// <summary>Ctrl+Alt+O for the light, Ctrl+Alt+P for night vision, and the throttle's ninth button for the gear.</summary>
    private static EliteBinds Binds()
    {
        var files = new MemoryFileSystem();

        files.WriteText(BindsPath, """
            <Root PresetName="Custom">
              <ShipSpotLightToggle>
                <Primary Device="Keyboard" Key="Key_O">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </ShipSpotLightToggle>
              <NightVisionToggle>
                <Primary Device="Keyboard" Key="Key_P">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </NightVisionToggle>
              <LandingGearToggle>
                <Primary Device="Keyboard" Key="Key_L" />
                <Secondary Device="4098BD65" Key="Joy_9" />
              </LandingGearToggle>
            </Root>
            """);

        return EliteBinds.Parse(files, BindsPath, "Custom", NullLogger.Instance);
    }

    private static readonly IReadOnlyList<HotasReading> Readings =
    [
        new HotasReading { Id = Throttle, VendorId = 0x4098, ProductId = 0xBD65, Buttons = new bool[32] },
    ];

    private static readonly SwitchMapping Gear = new()
    {
        Name = "Gear",
        DeviceId = Throttle,
        Positions = [new SwitchPosition(8, Action: "LandingGear"), new SwitchPosition(null)],
    };

    private static SettingsHost Open(bool withSwitch = false, bool headset = false)
    {
        var binds = Binds();
        IReadOnlyList<SwitchMapping> switches = withSwitch ? [Gear] : [];

        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(listening: surface => surface with
        {
            Binds = () => binds,
            Controllers = () => Readings,
            Switches = () => switches,
        });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        viewState.Save(viewState.Load() with { SettingsSection = "window" });

        var editing = new SwitchEditing(
            new SwitchStore(Path.Combine(paths.Data, "switches.json"), new DiskFileSystem(), NullLogger<SwitchStore>.Instance),
            new FakeHotasReader(),
            new SwitchReconciler(NullLogger<SwitchReconciler>.Instance),
            () => DateTimeOffset.UnixEpoch,
            Path.Combine(paths.Data, "switch-capture.txt"),
            () => [],
            settings);

        return SettingsHost.Open(
            settings,
            viewState,
            paths,
            switches: editing,
            headset: headset,
            bindingConflicts: new BindingConflictEditing(() => binds, () => Readings, () => switches, _ => () => { }));
    }

    private static BindingConflictsPage? Page(SettingsHost host) =>
        host.Window.GetVisualDescendants().OfType<BindingConflictsPage>().FirstOrDefault();

    private static BindingConflictsPage OpenPage(SettingsHost host)
    {
        var open = host.View.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "OpenBindingConflicts");
        BindSlots.Click(host.Window, open);

        var page = Page(host);
        Assert.True(page is not null, "the BINDING CONFLICTS tile opened no page");

        return page!;
    }

    private static List<Border> Rows(BindingConflictsPage page) =>
        [.. page.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains(BindingConflictsPage.RowClass))];

    private static Button Tile(Visual row, string name) =>
        row.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static string? Says(Visual row) => BindSlots.Says(BindSlots.Of(row).Single());

    private static string? Message(Visual row) => row.GetVisualDescendants().OfType<StatusLine>().Single().Text;

    private static void Press(SettingsHost host, Key key, PhysicalKey physical)
    {
        host.Window.KeyPress(key, RawInputModifiers.Control | RawInputModifiers.Alt, physical, null);
        host.Window.KeyRelease(key, RawInputModifiers.Control | RawInputModifiers.Alt, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheWindowRowCountsTheConflictsAndItsTileOpensThePage()
    {
        var host = Open();

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<SelectableTextBlock>(),
            text => text.Text == "1 of D47's keys and buttons is bound in Elite too: Show or hide the overlay");

        using (var settingsFrame = host.Window.CaptureRenderedFrame()!)
        {
            settingsFrame.SaveCapture("binding-conflicts-window-row.png");
        }

        var row = Assert.Single(Rows(OpenPage(host)));

        Assert.Equal("CTRL+ALT+O", Says(row));
        Assert.Contains(row.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "ShipSpotLightToggle");
        Assert.Contains(row.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Settings › Screens › Overlay");

        host.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetBigPanelOpensTheSamePage()
    {
        var host = Open(headset: true);

        Assert.Single(Rows(OpenPage(host)));

        host.Close();
    }

    [AvaloniaFact]
    public void RebindRefusesAKeyEliteBindsAndKeepsListening()
    {
        var host = Open();
        var row = Assert.Single(Rows(OpenPage(host)));

        BindSlots.Click(host.Window, Tile(row, "Rebind"));
        Assert.Equal("PRESS A KEY…", Says(row));
        Assert.Contains("selected", Tile(row, "Rebind").Classes);

        Press(host, Key.P, PhysicalKey.P);

        Assert.Equal(
            "Elite binds Ctrl+Alt+P to NightVisionToggle. Press another key, or click elsewhere to keep Ctrl+Alt+O.",
            Message(row));
        Assert.Equal("PRESS A KEY…", Says(row));
        Assert.Equal("Ctrl+Alt+O", host.Settings.Current.Hotkeys.ShowOverlay);

        using var frame = host.Window.CaptureRenderedFrame()!;
        frame.SaveCapture("binding-conflicts-refused.png");

        host.Close();
    }

    [AvaloniaFact]
    public void RebindRefusesAKeyAnotherD47RowHoldsAndNamesIt()
    {
        var host = Open();
        var row = Assert.Single(Rows(OpenPage(host)));

        BindSlots.Click(host.Window, Tile(row, "Rebind"));
        Press(host, Key.M, PhysicalKey.M);

        Assert.EndsWith("is already bound to Move the overlay. Clear it there first, or press another key.", Message(row));
        Assert.Equal("Ctrl+Alt+O", host.Settings.Current.Hotkeys.ShowOverlay);
        Assert.Equal("Ctrl+Alt+M", host.Settings.Current.Hotkeys.MoveOverlay);

        host.Close();
    }

    [AvaloniaFact]
    public void RebindToAFreeKeySavesItAndTheRowLeaves()
    {
        var host = Open();
        var page = OpenPage(host);
        var row = Assert.Single(Rows(page));

        BindSlots.Click(host.Window, Tile(row, "Rebind"));
        Press(host, Key.K, PhysicalKey.K);

        Assert.Equal("Ctrl+Alt+K", host.Settings.Current.Hotkeys.ShowOverlay);
        Assert.Empty(Rows(page));
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text?.StartsWith("None of D47's keys or buttons", StringComparison.Ordinal) == true);

        host.Close();
    }

    [AvaloniaFact]
    public void AClickElsewhereKeepsTheOldBinding()
    {
        var host = Open();
        var page = OpenPage(host);
        var row = Assert.Single(Rows(page));

        BindSlots.Click(host.Window, Tile(row, "Rebind"));
        BindSlots.Click(host.Window, page);

        Assert.Equal("CTRL+ALT+O", Says(row));
        Assert.Equal("Ctrl+Alt+O", host.Settings.Current.Hotkeys.ShowOverlay);

        host.Close();
    }

    [AvaloniaFact]
    public void UnbindClearsTheSlotAndTheRowLeaves()
    {
        var host = Open();
        var page = OpenPage(host);

        BindSlots.Click(host.Window, Tile(Assert.Single(Rows(page)), "Unbind"));

        Assert.Null(host.Settings.Current.Hotkeys.ShowOverlay);
        Assert.Empty(Rows(page));

        host.Close();
    }

    [AvaloniaFact]
    public void ASwitchPositionRowOpensTheSwitchPage()
    {
        var host = Open(withSwitch: true);
        var page = OpenPage(host);

        var row = Rows(page).Single(border => border.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "OpenTheSwitch"));

        Assert.DoesNotContain(row.GetVisualDescendants().OfType<Button>(), button => button.Name is "Rebind" or "Unbind");
        Assert.Contains(row.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "LandingGearToggle");

        using (var frame = host.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("binding-conflicts-page.png");
        }

        BindSlots.Click(host.Window, Tile(row, "OpenTheSwitch"));

        Assert.NotEmpty(host.Window.GetVisualDescendants().OfType<SwitchPage>());

        host.Close();
    }
}
