using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Hotas;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The switch page composes a position's key chord from two closed lists (#1429).</summary>
[Trait("Category", "Integration")]
public class ASwitchPositionCanPressAKeyChordTests
{
    private const string Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes";

    private static readonly DateTimeOffset Instant = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private sealed record Shown(SwitchStore Store, SwitchPage Page, Window Window, PanelView Panel, string Root);

    private static Shown Open(string? root = null, int width = 1024, int height = 640)
    {
        root ??= TempFolders.Create("d47-switch-chord");
        var store = new SwitchStore(
            Path.Combine(root, "switches.json"), new D47.Core.Storage.DiskFileSystem(), NullLogger<SwitchStore>.Instance);

        if (!File.Exists(Path.Combine(root, "switches.json")))
        {
            store.Save(
            [
                new SwitchMapping
                {
                    Name = "HOOK",
                    DeviceId = "stick-1",
                    Device = Device,
                    Positions = [new SwitchPosition(3), new SwitchPosition(2, "landing_gear", DesiredState.On)],
                },
            ]);
        }

        store.Poll();

        var page = new SwitchPage(
            store, new FakeHotasReader(), new SwitchReconciler(NullLogger<SwitchReconciler>.Instance), () => Instant,
            Path.Combine(root, "capture.txt"), []);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = width, Height = height };

        window.Show();
        _ = panel.Open(page);
        Jobs();

        return new Shown(store, page, window, panel, root);
    }

    private static List<InlinePicker> Pickers(Visual root) => [.. root.GetVisualDescendants().OfType<InlinePicker>()];

    private static string Text(Control control) =>
        string.Concat(control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

    private static string ValueOf(InlinePicker picker) =>
        string.Concat(picker.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "InlinePickerValue")
            .Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    private static void Pick(InlinePicker picker, string name)
    {
        Press(picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == InlinePicker.ButtonName));
        Press(picker.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass) && Text(button) == name));
    }

    private static void Save(Shown shown) =>
        Press(shown.Panel.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "Save"));

    private static KeyChord? Stored(Shown shown) =>
        shown.Store.Switches.Single().Positions[0].Chord;

    [AvaloniaFact]
    public void ChoosingKeyChordStartsAtCtrlVAndSavesIt()
    {
        using var look = AppLook.Put();
        var shown = Open();

        Pick(Pickers(shown.Page)[0], "Key chord");

        var chord = shown.Page.GetVisualDescendants().OfType<ChordPicker>().Single();
        Assert.Equal(new KeyChord("Key_V", ChordModifiers.Ctrl), chord.Chord);
        Assert.False(shown.Page.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == SwitchPage.StateName).IsVisible);

        Save(shown);

        Assert.Equal(new KeyChord("Key_V", ChordModifiers.Ctrl), Stored(shown));

        shown.Window.Close();

        var reopened = Open(shown.Root);
        var shownChord = Pickers(reopened.Page.GetVisualDescendants().OfType<ChordPicker>().Single());
        Assert.Equal(["Ctrl", "V"], shownChord.Select(ValueOf));

        reopened.Window.Close();
    }

    [AvaloniaFact]
    public void TheModifierListHasSixteenEntriesAndTheKeyListHasNoModifier()
    {
        using var look = AppLook.Put();
        var shown = Open();

        Pick(Pickers(shown.Page)[0], "Key chord");

        var chord = shown.Page.GetVisualDescendants().OfType<ChordPicker>().Single();
        foreach (var picker in Pickers(chord))
        {
            picker.IsOpen = true;
        }

        Jobs();

        var modifiers = Pickers(chord)[0].GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains(InlinePicker.OptionClass)).Select(Text).ToList();
        var keys = Pickers(chord)[1].GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains(InlinePicker.OptionClass)).Select(Text).ToList();
        var headings = Pickers(chord)[1].GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => t is "LETTERS" or "DIGITS" or "FUNCTION KEYS" or "NUMPAD" or "NAVIGATION" or "OTHER").ToList();

        Assert.Equal(16, modifiers.Count);
        Assert.Equal("None", modifiers[0]);
        Assert.Equal("Ctrl+Shift+Alt+Win", modifiers[15]);
        Assert.Equal(["LETTERS", "DIGITS", "FUNCTION KEYS", "NUMPAD", "NAVIGATION", "OTHER"], headings);
        Assert.DoesNotContain(keys, key => key.Contains("Shift", StringComparison.Ordinal) || key.Contains("Control", StringComparison.Ordinal)
            || key.Contains("Alt", StringComparison.Ordinal) || key.Contains("Win", StringComparison.Ordinal));

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ChoosingAnActionAfterwardsSavesNoChord()
    {
        using var look = AppLook.Put();
        var shown = Open();

        Pick(Pickers(shown.Page)[0], "Key chord");
        Save(shown);
        Assert.NotNull(Stored(shown));

        Pick(Pickers(shown.Page)[0], "Landing gear");
        Assert.Empty(shown.Page.GetVisualDescendants().OfType<ChordPicker>());

        Save(shown);

        Assert.Null(Stored(shown));
        Assert.Equal("landing_gear", shown.Store.Switches.Single().Positions[0].Action);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void AChordTheGameWouldCloseOnIsRefusedAndTheStoredSwitchIsUnchanged()
    {
        using var look = AppLook.Put();
        var shown = Open();
        var before = File.ReadAllText(Path.Combine(shown.Root, "switches.json"));

        Pick(Pickers(shown.Page)[0], "Key chord");

        var chord = shown.Page.GetVisualDescendants().OfType<ChordPicker>().Single();
        Pick(Pickers(chord)[0], "Alt");
        Pick(Pickers(chord)[1], "F4");

        var notice = shown.Page.GetVisualDescendants().OfType<Notice>().Single();
        Assert.True(notice.IsVisible);
        Assert.Contains("Alt+F4 is refused", notice.Text, StringComparison.Ordinal);

        Save(shown);

        Assert.Equal(before, File.ReadAllText(Path.Combine(shown.Root, "switches.json")));
        Assert.Null(Stored(shown));

        shown.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(820, 640)]
    [InlineData(1280, 800)]
    public void TheChordRowIsFortyFourTallAndCaptured(int width, int height)
    {
        using var look = AppLook.Put();
        var shown = Open(null, width, height);

        Pick(Pickers(shown.Page)[0], "Key chord");

        var buttons = Pickers(shown.Page.GetVisualDescendants().OfType<ChordPicker>().Single())
            .SelectMany(picker => picker.GetVisualDescendants().OfType<Button>().Where(button => button.Name == InlinePicker.ButtonName));
        Assert.All(buttons, button => Assert.True(button.Bounds.Height >= D47.App.Theming.TypeScale.MinimumTarget));

        if (TestSurface.CapturesWanted)
        {
            using var frame = shown.Window.CaptureRenderedFrame()!;
            frame.SaveCapture($"switch-chord-row-{width}.png");
        }

        shown.Window.Close();
    }
}
