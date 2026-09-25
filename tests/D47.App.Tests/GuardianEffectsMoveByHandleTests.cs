using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// A Guardian effect moves by dragging its handle, written once on drop, or by Up and Down on the focused
/// handle, which keeps focus (#480).
/// </summary>
public sealed class GuardianEffectsMoveByHandleTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (SettingsHost Host, SettingsService Settings) OpenVoice()
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(guardianTest: _ => Task.FromResult<string?>(null));
        var host = SettingsHost.Open(settings, viewState, paths, width: 1180, height: 880);
        Open(host.View, "voice");
        return (host, settings);
    }

    private static StackPanel Effects(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == SettingsView.GuardianEffectsName);

    private static Border Strip(SettingsView view, string id) =>
        Effects(view).GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(SettingsView.GuardianStripClass) && (string?)border.Tag == id);

    private static Border Handle(SettingsView view, string id) =>
        Strip(view, id).GetVisualDescendants().OfType<Border>().Single(border => border.Name == SettingsView.GuardianHandleName);

    private static string Number(SettingsView view, string id) =>
        Strip(view, id).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == SettingsView.GuardianNumberName).Text!;

    private static string[] Order(SettingsService settings) =>
        settings.Read(SpeechCapability.GuardianOrderKey)!.Split(',');

    private static string[] Defaults => [.. GuardianVoice.Table.Select(effect => effect.Id)];

    private static string PickerValue(SettingsView view) =>
        Words(Page(view).GetVisualDescendants().OfType<InlinePicker>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "InlinePickerValue"));

    private static Point Middle(SettingsHost host, Control control)
    {
        control.BringIntoView();
        Jobs();
        host.Window.UpdateLayout();

        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), host.Window)!.Value;
    }

    private static int Writes(SettingsService settings, Action act)
    {
        var writes = 0;
        void Count(SettingApplied applied)
        {
            if (applied.Key == SpeechCapability.GuardianOrderKey)
            {
                writes++;
            }
        }

        settings.Applied += Count;
        act();
        settings.Applied -= Count;

        return writes;
    }

    private static void PressOn(SettingsHost host, Border handle, Key key, PhysicalKey physical)
    {
        handle.Focus();
        Jobs();
        host.Window.KeyPress(key, RawInputModifiers.None, physical, null);
        Jobs();
    }

    [AvaloniaFact]
    public void DraggingTheThirdEffectToTheTopPutsItFirstAndShiftsTheRestDownWithOneWrite()
    {
        using var look = AppLook.Put();
        var (host, settings) = OpenVoice();
        var third = Defaults[2];
        var from = Middle(host, Handle(host.View, third));
        var first = Middle(host, Handle(host.View, Defaults[0]));
        var between = new Point(from.X, (from.Y + first.Y) / 2);

        var writes = Writes(settings, () =>
        {
            host.Window.MouseDown(from, MouseButton.Left);
            host.Window.MouseMove(between, RawInputModifiers.LeftMouseButton);
            Jobs();

            Assert.Equal("02", Number(host.View, third));
            Assert.Equal("03", Number(host.View, Defaults[1]));
            Assert.Equal(Defaults, Order(settings));

            host.Window.MouseMove(first, RawInputModifiers.LeftMouseButton);
            Jobs();

            Assert.Equal("01", Number(host.View, third));
            Assert.Equal("02", Number(host.View, Defaults[0]));
            Assert.Equal(Defaults, Order(settings));

            using (var frame = host.Window.CaptureRenderedFrame()!)
            {
                frame.Save(Path.Combine(TestSurface.CaptureDirectory, "guardian-voice-effects-dragging.png"), new PngBitmapEncoderOptions());
            }

            host.Window.MouseUp(first, MouseButton.Left);
            Jobs();
        });

        Assert.Equal(1, writes);
        Assert.Equal([third, Defaults[0], Defaults[1], .. Defaults[3..]], Order(settings));
        Assert.Equal("01", Number(host.View, third));
        Assert.Equal(third, (string?)Effects(host.View).GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains(SettingsView.GuardianStripClass)).Tag);
        Assert.All(Effects(host.View).GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains(SettingsView.GuardianStripClass)),
            strip => Assert.Null(strip.RenderTransform));

        host.Close();
    }

    [AvaloniaFact]
    public void ADropWhereItStartedWritesNothing()
    {
        var (host, settings) = OpenVoice();
        var from = Middle(host, Handle(host.View, Defaults[4]));

        var writes = Writes(settings, () =>
        {
            host.Window.MouseDown(from, MouseButton.Left);
            host.Window.MouseMove(new Point(from.X, from.Y + 5), RawInputModifiers.LeftMouseButton);
            host.Window.MouseUp(new Point(from.X, from.Y + 5), MouseButton.Left);
            Jobs();
        });

        Assert.Equal(0, writes);
        Assert.Equal(Defaults, Order(settings));

        host.Close();
    }

    [AvaloniaFact]
    public void UpAndDownMoveTheEffectOnePlaceAndKeepFocusOnItsHandle()
    {
        var (host, settings) = OpenVoice();
        var id = Defaults[3];

        PressOn(host, Handle(host.View, id), Key.Up, PhysicalKey.ArrowUp);

        Assert.Equal([Defaults[0], Defaults[1], id, Defaults[2], .. Defaults[4..]], Order(settings));
        Assert.Equal("03", Number(host.View, id));
        Assert.True(Handle(host.View, id).IsFocused);

        host.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Jobs();
        host.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Jobs();

        Assert.Equal([.. Defaults[..3], Defaults[4], id, .. Defaults[5..]], Order(settings));
        Assert.True(Handle(host.View, id).IsFocused);

        host.Close();
    }

    [AvaloniaFact]
    public void UpOnTheFirstAndDownOnTheLastDoNothing()
    {
        var (host, settings) = OpenVoice();

        var writes = Writes(settings, () =>
        {
            PressOn(host, Handle(host.View, Defaults[0]), Key.Up, PhysicalKey.ArrowUp);
            PressOn(host, Handle(host.View, Defaults[^1]), Key.Down, PhysicalKey.ArrowDown);
        });

        Assert.Equal(0, writes);
        Assert.Equal(Defaults, Order(settings));

        host.Close();
    }

    [AvaloniaFact]
    public void ReorderingABuiltInPresetMakesThePickerReadCustom()
    {
        var (host, settings) = OpenVoice();
        var picker = Page(host.View).GetVisualDescendants().OfType<InlinePicker>().Single();

        picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == InlinePicker.ButtonName)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        picker.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass) && AutomationProperties.GetName(button) == "Vocoder")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        Assert.Equal("Vocoder", PickerValue(host.View));

        PressOn(host, Handle(host.View, Defaults[1]), Key.Up, PhysicalKey.ArrowUp);

        Assert.Equal("Custom", PickerValue(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void TheHandleSaysHowToUseIt()
    {
        var (host, _) = OpenVoice();

        Assert.Equal(SettingsView.GuardianHandleTip, ToolTip.GetTip(Handle(host.View, Defaults[0])));
        Assert.Equal("Drag to reorder. Arrow keys also move it.", SettingsView.GuardianHandleTip);
        Assert.Contains(
            Effects(host.View).GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Applied top to bottom. Drag to reorder.");

        host.Close();
    }
}
