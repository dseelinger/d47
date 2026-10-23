using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>The COVAS reverb tile, the Test row's text and the TEST tile are one line on Its voice.</summary>
[Trait("Category", "Integration")]
public sealed class TheCovasReverbAndItsTestShareOneLineTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (SettingsHost Host, SettingsService Settings) OpenVoice(Func<CancellationToken, Task<string?>>? covasTest = null)
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(
            speech: surface => surface with { CovasTest = covasTest });
        var host = SettingsHost.Open(settings, viewState, paths, width: 1180, height: 880);
        Open(host.View, "voice");
        return (host, settings);
    }

    private static Grid Line(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<StackPanel>()
            .Single(panel => panel.Name == SettingsView.ToggleLineName)
            .GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 5);

    [AvaloniaFact]
    public void TheTileTheHintAndTheTestButtonShareOneRow()
    {
        var (host, _) = OpenVoice(_ => Task.FromResult<string?>(null));
        var test = (Button)host.View.ControlFor(SpeechCapability.CovasTestKey)!;
        var line = Line(host.View);

        Assert.Same(line, test.GetVisualParent());
        Assert.Equal("Test", test.Content);

        var hint = line.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Text == "Hear the COVAS reverb without paying for it.");
        var tile = host.View.ControlFor(SpeechCapability.CovasReverbKey)!;

        Assert.Contains(tile, line.GetVisualDescendants());
        Assert.Contains(hint, line.GetVisualDescendants());

        var top = tile.TranslatePoint(default, line)!.Value.Y;
        Assert.Equal(top, test.TranslatePoint(default, line)!.Value.Y, 1);

        var slab = hint.GetVisualAncestors().OfType<Border>().First(border => border.Parent == line);
        Assert.Equal(top, slab.TranslatePoint(default, line)!.Value.Y, 1);
        Assert.Equal(slab.Bounds.Height, test.Bounds.Height, 0.5);
        Assert.Equal(44, test.Bounds.Height, 0.5);

        host.Close();
    }

    [AvaloniaFact]
    public void NoRowLabelledTestIsDrawnInTheCovasVoiceGroup()
    {
        var (host, _) = OpenVoice(_ => Task.FromResult<string?>(null));

        Assert.Null(host.View.LabelFor(SpeechCapability.CovasTestKey));

        var loose = Page(host.View).GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Text == "Test" && !text.GetVisualAncestors().OfType<Button>().Any());
        Assert.DoesNotContain(loose, text => text.GetVisualAncestors().OfType<CheckBox>().Any() is false);

        host.Close();
    }

    [AvaloniaFact]
    public void ClickingTheTileStillWritesTheReverbSetting()
    {
        var (host, settings) = OpenVoice();
        var before = settings.Current.Speech.CovasReverb;
        var box = (CheckBox)host.View.ControlFor(SpeechCapability.CovasReverbKey)!;

        box.IsChecked = !before;
        Jobs();

        Assert.Equal(!before, settings.Current.Speech.CovasReverb);

        host.Close();
    }

    [AvaloniaFact]
    public void TestReadsPlayingWhileItRuns()
    {
        var playing = new TaskCompletionSource<string?>();
        var (host, _) = OpenVoice(_ => playing.Task);
        var test = (Button)host.View.ControlFor(SpeechCapability.CovasTestKey)!;

        test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        Assert.Equal("Playing", test.Content);

        playing.SetResult(null);
        Jobs();
        Assert.Equal("Test", test.Content);

        host.Close();
    }
}
