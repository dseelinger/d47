using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
/// The Guardian Voice Effects group is one control: an inline preset picker and TEST on the preset row,
/// and every effect as a strip under it, each writing through its own row (#479).
/// </summary>
public sealed class GuardianVoiceEffectsAreOneGroupTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (SettingsHost Host, SettingsService Settings) OpenVoice(
        Func<CancellationToken, Task<string?>>? guardianTest = null,
        Action<SettingsService>? arrange = null,
        double width = 1180,
        double height = 880)
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(guardianTest: guardianTest);
        arrange?.Invoke(settings);
        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: height);
        Open(host.View, "voice");
        return (host, settings);
    }

    private static InlinePicker Picker(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<InlinePicker>().Single();

    private static Button PickerButton(SettingsView view) =>
        Picker(view).GetVisualDescendants().OfType<Button>().Single(button => button.Name == InlinePicker.ButtonName);

    private static string PickerValue(SettingsView view) =>
        Words(Picker(view).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "InlinePickerValue"));

    private static StackPanel Effects(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == SettingsView.GuardianEffectsName);

    private static Border Strip(SettingsView view, string id) =>
        Effects(view).GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(SettingsView.GuardianStripClass) && (string?)border.Tag == id);

    private static Button Named(Control within, string name) =>
        within.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == name);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static void Pick(SettingsView view, string label)
    {
        Press(PickerButton(view));
        Press(Picker(view).GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass) && AutomationProperties.GetName(button) == label));
    }

    private static CheckBox Box(SettingsView view, string id) =>
        (CheckBox)view.ControlFor(SpeechCapability.GuardianEffectKey(id))!;

    private static Level LevelOf(SettingsView view, string id) =>
        (Level)view.ControlFor(SpeechCapability.GuardianLevelKey(id))!;

    private static void WithSaved(SettingsService settings, string name, string tickedId, int level)
    {
        var effects = GuardianVoice.Table
            .Select(effect => new GuardianVoiceEffect
            {
                Id = effect.Id,
                Ticked = effect.Id == tickedId,
                Level = effect.Id == tickedId ? level : effect.DefaultLevel,
            })
            .ToList();

        settings.Replace("test preset", s => s with
        {
            Speech = s.Speech with
            {
                GuardianVoice = s.Speech.GuardianVoice with
                {
                    SavedPresets = [new GuardianVoicePreset { Name = name, Effects = effects }],
                },
            },
        });
    }

    /// <summary>A point along a level's track, as a share of its width, in window coordinates.</summary>
    private static Point Along(SettingsHost host, Level level, double share)
    {
        level.BringIntoView();
        Jobs();
        host.Window.UpdateLayout();

        return level.TranslatePoint(new Point(level.Bounds.Width * share, level.Bounds.Height / 2), host.Window)!.Value;
    }

    [AvaloniaFact]
    public void OpeningThePickerPushesTheEffectsDownWithinThePage()
    {
        var (host, settings) = OpenVoice();
        var effects = Effects(host.View);
        host.Window.UpdateLayout();

        var before = effects.TranslatePoint(default, Page(host.View))!.Value.Y;

        Press(PickerButton(host.View));
        host.Window.UpdateLayout();

        var list = Picker(host.View).GetVisualDescendants().OfType<Border>().Single(border => border.Name == InlinePicker.ListName);

        Assert.True(list.IsEffectivelyVisible);
        Assert.True(effects.TranslatePoint(default, Page(host.View))!.Value.Y > before);
        Assert.Contains(Page(host.View), list.GetVisualAncestors());

        var group = (Control)Picker(host.View).GetVisualAncestors().OfType<StackPanel>()
            .First(panel => panel.Children.Contains(effects));
        Assert.Empty(group.GetVisualDescendants().OfType<Popup>());
        Assert.All(group.GetVisualDescendants().OfType<Button>(), button => Assert.Null(button.Flyout));
        Assert.All(group.GetVisualDescendants().OfType<Control>(), control => Assert.Null(FlyoutBase.GetAttachedFlyout(control)));

        host.Close();
    }

    [AvaloniaFact]
    public void TheListIsTheBuiltInsThenYourPresetsThenCustom()
    {
        var (host, settings) = OpenVoice();

        Press(PickerButton(host.View));

        var words = Picker(host.View).GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Name != "InlinePickerValue" && text.Text is { Length: > 1 })
            .Select(text => text.Text!)
            .ToList();

        Assert.Equal(
            [.. GuardianPresets.Builtins.Select(builtin => builtin.Label), "YOUR PRESETS",
                "None yet. Set up the effects, then press SAVE AS.", "Custom"],
            words);

        host.Close();
    }

    [AvaloniaFact]
    public void PickingABuiltInTicksExactlyItsEffects()
    {
        var (host, settings) = OpenVoice();

        Pick(host.View, "Vocoder");

        Assert.Equal("vocoder", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Equal("Vocoder", PickerValue(host.View));
        Assert.True(Box(host.View, "cylon").IsChecked);
        Assert.False(Box(host.View, "glitch").IsChecked);
        Assert.False(Picker(host.View).IsOpen);

        host.Close();
    }

    [AvaloniaFact]
    public void PickingASavedPresetLoadsItAndChangingItReadsChangedFromIt()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        Pick(host.View, "Hull breach");

        Assert.Equal("Hull breach", PickerValue(host.View));
        Assert.Equal("Hull breach", settings.Current.Speech.GuardianVoice.Basis);
        Assert.True(Box(host.View, "reverb").IsChecked);
        Assert.Equal("3", settings.Read(SpeechCapability.GuardianLevelKey("reverb")));

        Box(host.View, "cylon").IsChecked = true;
        Jobs();

        Assert.Equal("Custom  · changed from Hull breach", PickerValue(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void PickingCustomChangesNothing()
    {
        var (host, settings) = OpenVoice();

        Box(host.View, "reverb").IsChecked = true;
        Jobs();
        var before = settings.Current;

        Pick(host.View, "Custom");

        Assert.Equal(before.Speech.GuardianVoice, settings.Current.Speech.GuardianVoice);
        Assert.Equal("Custom", PickerValue(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void TickingTheVocodersEffectsMakesThePickerReadVocoder()
    {
        var (host, settings) = OpenVoice();

        foreach (var id in new[] { "cylon", "chorus", "reverb" })
        {
            Box(host.View, id).IsChecked = true;
            Jobs();
            Assert.Equal("true", settings.Read(SpeechCapability.GuardianEffectKey(id)));
        }

        Assert.Equal("Vocoder", PickerValue(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void ClickingSegmentNSetsLevelN()
    {
        var (host, settings) = OpenVoice();
        var level = LevelOf(host.View, "glitch");

        var point = Along(host, level, 4.5 / 20);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        Jobs();

        Assert.Equal("5", settings.Read(SpeechCapability.GuardianLevelKey("glitch")));
        Assert.Equal("Custom", PickerValue(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void DraggingAcrossTheTrackSetsTheLevelWhereItStops()
    {
        var (host, settings) = OpenVoice();
        var level = LevelOf(host.View, "chorus");

        var from = Along(host, level, 1.5 / 20);
        var to = Along(host, level, 11.5 / 20);
        host.Window.MouseDown(from, MouseButton.Left);
        host.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        host.Window.MouseUp(to, MouseButton.Left);
        Jobs();

        Assert.Equal("12", settings.Read(SpeechCapability.GuardianLevelKey("chorus")));

        host.Close();
    }

    [AvaloniaFact]
    public void TheStepperMovesOneLevelAndShowsRealUnits()
    {
        var (host, settings) = OpenVoice();
        var strip = Strip(host.View, "pitchDown");
        var start = GuardianVoice.Table.Single(effect => effect.Id == "pitchDown").DefaultLevel;

        Press(Named(strip, "Increase Pitch down"));
        Assert.Equal($"{start + 1}", settings.Read(SpeechCapability.GuardianLevelKey("pitchDown")));

        Press(Named(strip, "Decrease Pitch down"));
        Press(Named(strip, "Decrease Pitch down"));
        Assert.Equal($"{start - 1}", settings.Read(SpeechCapability.GuardianLevelKey("pitchDown")));

        var shown = strip.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == SettingsView.GuardianValueName);
        Assert.Equal($"−{(start - 1) / 2.0:0.0} st", shown.Text);

        host.Close();
    }

    [AvaloniaFact]
    public void ALevelStaysBetweenOneAndTwenty()
    {
        var (host, settings) = OpenVoice();
        var strip = Strip(host.View, "reverb");
        var key = SpeechCapability.GuardianLevelKey("reverb");

        for (var i = 0; i < 25; i++)
        {
            Press(Named(strip, "Increase Reverb"));
        }

        Assert.Equal("20", settings.Read(key));

        for (var i = 0; i < 25; i++)
        {
            Press(Named(strip, "Decrease Reverb"));
        }

        Assert.Equal("1", settings.Read(key));

        var point = Along(host, LevelOf(host.View, "reverb"), 0.001);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        Jobs();

        Assert.Equal("1", settings.Read(key));

        host.Close();
    }

    [AvaloniaFact]
    public void TheGroupResetMakesThePickerReadOffAndClearsTheBasis()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        Pick(host.View, "Hull breach");
        Press(Named(Strip(host.View, "cylon"), "Decrease Cylon"));
        Assert.StartsWith("Custom", PickerValue(host.View), StringComparison.Ordinal);

        var reset = Page(host.View).GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == SettingsView.GroupResetName
                              && AutomationProperties.GetName(button) == "Reset Guardian Voice Effects");
        Assert.True(reset.IsEnabled);

        Press(reset);

        Assert.Equal("Off", PickerValue(host.View));
        Assert.Null(settings.Current.Speech.GuardianVoice.Basis);
        Assert.Single(settings.Current.Speech.GuardianVoice.SavedPresets!);
        Assert.Equal(
            $"{GuardianVoice.Table.Single(effect => effect.Id == "cylon").DefaultLevel}",
            settings.Read(SpeechCapability.GuardianLevelKey("cylon")));
        Assert.False(reset.IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void TestReadsPlayingWhileItRuns()
    {
        var playing = new TaskCompletionSource<string?>();
        var (host, _) = OpenVoice(guardianTest: _ => playing.Task);
        var test = (Button)host.View.ControlFor(SpeechCapability.GuardianTestKey)!;

        Assert.True(test.IsEnabled);
        Press(test);
        Assert.Equal("Playing", test.Content);

        playing.SetResult(null);
        Jobs();
        Assert.Equal("Test", test.Content);

        host.Close();
    }

    [AvaloniaFact]
    public void NoEffectIsATileInAGrid()
    {
        var (host, settings) = OpenVoice();

        Assert.DoesNotContain(
            Page(host.View).GetVisualDescendants().OfType<TileGrid>().SelectMany(grid => grid.GetVisualDescendants().OfType<CheckBox>()),
            box => GuardianVoice.Table.Any(effect => AutomationProperties.GetName(box) == effect.Label));
        Assert.Equal(GuardianVoice.Table.Count, Effects(host.View).GetVisualDescendants().OfType<Border>()
            .Count(border => border.Classes.Contains(SettingsView.GuardianStripClass)));

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1280, 860)]
    [InlineData(924, 640)]
    public void TheGroupIsCapturedWithTheTrackAtLeast180Wide(double width, double height)
    {
        using var look = AppLook.Put();

        var (host, _) = OpenVoice(guardianTest: _ => Task.FromResult<string?>(null), width: width, height: height);
        var effects = Effects(host.View);

        effects.BringIntoView();
        Jobs();
        host.Window.UpdateLayout();

        foreach (var effect in GuardianVoice.Table)
        {
            var strip = Strip(host.View, effect.Id);
            var level = LevelOf(host.View, effect.Id);
            var right = strip.TranslatePoint(new Point(strip.Bounds.Width, 0), effects)!.Value.X;

            Assert.True(level.Bounds.Width >= SettingsView.GuardianTrackMinWidth, $"{effect.Label}'s track is {level.Bounds.Width} wide");
            Assert.True(right <= effects.Bounds.Width + 0.5, $"{effect.Label}'s strip runs to {right} in {effects.Bounds.Width}");
        }

        Capture(host, $"guardian-voice-effects-{width}x{height}.png");

        Press(PickerButton(host.View));
        Picker(host.View).BringIntoView();
        Jobs();
        host.Window.UpdateLayout();

        Capture(host, $"guardian-voice-effects-picker-{width}x{height}.png");

        host.Close();
    }

    private static void Capture(SettingsHost host, string name)
    {
        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = host.Window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        Assert.True(File.Exists(path));
    }
}
