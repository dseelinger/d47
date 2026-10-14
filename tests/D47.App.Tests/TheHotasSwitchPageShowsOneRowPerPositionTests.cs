using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each switch position is one full-width row: the button, the action, ON or OFF, and a page (#1150).</summary>
[Trait("Category", "Integration")]
public class TheHotasSwitchPageShowsOneRowPerPositionTests
{
    private const string Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes";

    private static readonly string[] Sticks = ["stick-1", "stick-2", "stick-3", "stick-4"];

    private static readonly DateTimeOffset Instant = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static SwitchMapping Switch(string name, int stick, params SwitchPosition[] positions) => new()
    {
        Name = name,
        DeviceId = Sticks[stick],
        Device = Device,
        Positions = positions,
    };

    /// <summary>The four switches of the design board; WING FOLD's last position opens a page.</summary>
    private static List<SwitchMapping> Four() =>
    [
        Switch("LDG GEAR", 0, new SwitchPosition(9, "landing_gear", DesiredState.On), new SwitchPosition(8, "landing_gear", DesiredState.Off)),
        Switch("LAUNCH BAR", 1, new SwitchPosition(1, "analysis_mode", DesiredState.Off), new SwitchPosition(0, "analysis_mode", DesiredState.On)),
        Switch("HOOK", 2, new SwitchPosition(3), new SwitchPosition(2)),
        Switch(
            "WING FOLD",
            3,
            new SwitchPosition(4),
            new SwitchPosition(5),
            new SwitchPosition(6, null, DesiredState.On, "transcript.technical")),
    ];

    private static PanelDestination Technical() =>
        new(PanelTab.Transcript, new NavCrumb("transcript.technical", "Technical"));

    private static PanelDestination Conversation() =>
        new(PanelTab.Transcript, new NavCrumb("transcript.conversation", "Conversation"));

    private sealed record Shown(
        SwitchPage Page, Window Window, PanelView Panel, SwitchReconciler Reconciler, FakeHotasReader Reader, List<SwitchMapping> Mappings);

    private static Shown Open(int width = 820, int height = 640)
    {
        var root = TempFolders.Create("d47-switch-rows");
        var store = new SwitchStore(
            Path.Combine(root, "switches.json"), new D47.Core.Storage.DiskFileSystem(), NullLogger<SwitchStore>.Instance);

        var mappings = Four();
        store.Save(mappings);
        store.Poll();

        var reader = new FakeHotasReader();
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var page = new SwitchPage(
            store, reader, reconciler, () => Instant, Path.Combine(root, "capture.txt"), [Technical(), Conversation()]);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = width, Height = height };

        window.Show();
        _ = panel.Open(page);
        Jobs();

        return new Shown(page, window, panel, reconciler, reader, mappings);
    }

    /// <summary>One button held on each stick, in order.</summary>
    private static void PollWith(Shown shown, int ldg, int launch, int hook, int wing)
    {
        int[] held = [ldg, launch, hook, wing];

        shown.Reader.Set([.. held.Select((button, index) =>
        {
            var buttons = new bool[32];
            buttons[button] = true;

            return new HotasReading { Id = Sticks[index], VendorId = 0x4098, ProductId = 0xBD65, Buttons = buttons };
        })]);

        // Elite binds the launch bar's first position to two actions as well.
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings =
            [
                new EliteBinding("IncreaseWeaponsPower", "Primary", "4098BD65", "Joy_2"),
                new EliteBinding("IncreaseWeaponsPower_Buggy", "Primary", "4098BD65", "Joy_2"),
            ],
        };

        shown.Reconciler.Poll(
            new SwitchTick
            {
                Now = Instant,
                Readings = shown.Reader.Poll(),
                Status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = Instant },
                Binds = binds,
                Enabled = false,
            },
            shown.Mappings);

        shown.Page.Sample();
        Jobs();
    }

    private static List<bool> HeldBars(Visual root) =>
        [.. root.GetVisualDescendants().OfType<Border>().Where(border => border.Name == SwitchPage.HeldBarName).Select(bar => bar.IsVisible)];

    private static List<InlinePicker> Actions(Visual root) => [.. root.GetVisualDescendants().OfType<InlinePicker>()];

    private static List<Segment> States(Visual root) =>
        [.. root.GetVisualDescendants().OfType<Segment>().Where(segment => segment.Name == SwitchPage.StateName)];

    private static List<Button> PageButtons(Visual root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Name == SwitchPage.PageButtonName)];

    private static string Text(Control control) =>
        string.Concat(control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

    private static string ValueOf(InlinePicker picker) =>
        string.Concat(picker.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "InlinePickerValue")
            .Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    private static void PickAction(InlinePicker picker, string name)
    {
        Press(picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == InlinePicker.ButtonName));
        Assert.True(picker.IsOpen);

        Press(picker.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass) && Text(button) == name));
    }

    [AvaloniaFact]
    public void ThePositionTheSwitchIsInIsMarkedHeldAndFlippingMovesIt()
    {
        using var look = AppLook.Put();
        var shown = Open();

        PollWith(shown, ldg: 9, launch: 1, hook: 2, wing: 5);

        Assert.Equal([true, false, true, false, false, true, false, true, false], HeldBars(shown.Page));

        PollWith(shown, ldg: 8, launch: 1, hook: 2, wing: 5);

        Assert.Equal([false, true, true, false, false, true, false, true, false], HeldBars(shown.Page));

        var words = shown.Page.GetVisualDescendants().OfType<TextBlock>().Where(word => word.Name == SwitchPage.HeldWordName);
        Assert.Equal(4, words.Count(word => word.IsVisible));

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ACollisionShowsInFullInRedAndWrapped()
    {
        using var look = AppLook.Put();
        var shown = Open();

        PollWith(shown, ldg: 9, launch: 1, hook: 2, wing: 5);

        var said = shown.Page.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Text?.StartsWith("Elite binds this button", StringComparison.Ordinal) == true);

        Assert.Contains("Unbind it in Elite", said.Text, StringComparison.Ordinal);
        Assert.Equal(TextWrapping.Wrap, said.TextWrapping);
        Assert.True(said.IsVisible);

        Application.Current!.TryGetResource(ThemeManager.RedKey, null, out var red);
        Assert.Equal(((ISolidColorBrush)red!).Color, ((ISolidColorBrush)said.Foreground!).Color);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingAnActionClosesTheDropdownEnablesOnAndOffAndClearsThePage()
    {
        using var look = AppLook.Put();
        var shown = Open();

        // WING FOLD's third position: no action, so ON / OFF is greyed, and it opens a page.
        var picker = Actions(shown.Page)[8];
        var state = States(shown.Page)[8];
        var page = PageButtons(shown.Page)[8];

        Assert.False(state.IsEnabled);
        Assert.Contains("Technical", Text(page), StringComparison.Ordinal);

        PickAction(picker, "Landing gear");

        Assert.False(picker.IsOpen);
        Assert.Equal("Landing gear", ValueOf(picker));
        Assert.True(state.IsEnabled);
        Assert.Contains("Nothing", Text(page), StringComparison.Ordinal);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingNothingGreysOnAndOff()
    {
        using var look = AppLook.Put();
        var shown = Open();

        PickAction(Actions(shown.Page)[0], "Nothing");

        Assert.False(States(shown.Page)[0].IsEnabled);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingAPageClearsTheActionAndLeavesTheSwitchPageOnTheTrail()
    {
        using var look = AppLook.Put();
        var shown = Open();

        Press(PageButtons(shown.Page)[0]);

        var picker = shown.Panel.GetVisualDescendants().OfType<PickerPage>().Single();
        var choices = picker.GetControl<ListBox>("Choices");
        choices.SelectedItem = choices.Items.OfType<PickerChoice>().Single(choice => choice.Text.StartsWith("Technical", StringComparison.Ordinal));
        Jobs();
        Press(picker.GetControl<Button>("AcceptButton"));

        Assert.Empty(shown.Panel.GetVisualDescendants().OfType<PickerPage>());
        Assert.Equal("HOTAS switches", shown.Panel.Nav.Trail[^1].Word);
        Assert.Contains("Technical", Text(PageButtons(shown.Page)[0]), StringComparison.Ordinal);
        Assert.Equal("Nothing", ValueOf(Actions(shown.Page)[0]));
        Assert.False(States(shown.Page)[0].IsEnabled);

        shown.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(820, 640)]
    [InlineData(1024, 640)]
    public void EveryControlAPositionPressesIsFortyFourTallAndNoLabelWraps(int width, int height)
    {
        using var look = AppLook.Put();
        var shown = Open(width, height);

        var pressed = new List<Control>(
        [
            .. Actions(shown.Page).SelectMany(picker => picker.GetVisualDescendants().OfType<Button>().Where(button => button.Name == InlinePicker.ButtonName)),
            .. States(shown.Page),
            .. PageButtons(shown.Page),
        ]);

        Assert.Equal(27, pressed.Count);
        Assert.All(pressed, control => Assert.True(control.Bounds.Height >= TypeScale.MinimumTarget, $"{control.Name} is {control.Bounds.Height}px tall"));

        var labels = States(shown.Page).SelectMany(state => state.GetVisualDescendants().OfType<TextBlock>());
        Assert.All(labels, label => Assert.True(label.Bounds.Height < 24, $"'{label.Text}' wrapped"));

        shown.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(820, 640)]
    [InlineData(1024, 640)]
    public void TheRowsAreCapturedClosedAndWithADropdownOpen(int width, int height)
    {
        if (!TestSurface.CapturesWanted)
        {
            return;
        }

        using var look = AppLook.Put();
        var shown = Open(width, height);

        PollWith(shown, ldg: 9, launch: 1, hook: 2, wing: 5);

        using (var frame = shown.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture($"hotas-switch-rows-{width}.png");
        }

        shown.Window.Height = 1500;
        Jobs();

        using (var tall = shown.Window.CaptureRenderedFrame()!)
        {
            tall.SaveCapture($"hotas-switch-rows-whole-{width}.png");
        }

        shown.Window.Height = height;
        Jobs();

        Actions(shown.Page)[2].IsOpen = true;
        Jobs();

        using (var open = shown.Window.CaptureRenderedFrame()!)
        {
            open.SaveCapture($"hotas-switch-rows-open-{width}.png");
        }

        shown.Window.Close();
    }
}
