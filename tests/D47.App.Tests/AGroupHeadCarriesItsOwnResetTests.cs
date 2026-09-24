using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// Every group is drawn under a head carrying its title, its description and its own reset, and a group
/// with no row showing is not drawn at all (#436).
/// </summary>
public sealed class AGroupHeadCarriesItsOwnResetTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static List<StackPanel> Heads(SettingsView view) =>
        [.. Page(view).GetVisualDescendants().OfType<StackPanel>().Where(panel => panel.Name == SettingsView.GroupHeadName)];

    /// <summary>The head whose reset is named for a group title, or null where no such head is built.</summary>
    private static StackPanel? Head(SettingsView view, string title) =>
        Heads(view).SingleOrDefault(head => head.GetVisualDescendants().OfType<Button>()
            .Any(button => AutomationProperties.GetName(button) == $"Reset {title}"));

    private static Button Reset(StackPanel head) =>
        head.GetVisualDescendants().OfType<Button>().Single(button => button.Name == SettingsView.GroupResetName);

    [AvaloniaTheory]
    [InlineData("voice-input")]
    [InlineData("voice")]
    [InlineData("sounds")]
    public void EachHeadHasATitleADescriptionAndOneResetOnAnAccentRule(string placeId)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, placeId);

        var place = SettingsLayout.Areas.SelectMany(a => a.Places).Single(p => p.Id == placeId);
        var drawn = Heads(host.View).Where(head => head.IsEffectivelyVisible).ToList();

        Assert.NotEmpty(drawn);

        foreach (var head in drawn)
        {
            var reset = Reset(head);
            var title = AutomationProperties.GetName(reset)!["Reset ".Length..];
            var group = place.Groups.Single(g => g.Title == title);
            var texts = head.GetVisualDescendants().OfType<TextBlock>().ToList();

            Assert.Contains(texts, text => text.FontSize == TypeScale.Secondary
                && Words(text) == group.Title.ToUpperInvariant());
            Assert.Contains(texts, text => Words(text) == group.Help);
            Assert.Equal(TypeScale.MinimumTarget, reset.Width);

            var rule = head.Children.OfType<Border>().Single();
            Assert.Equal(1, rule.Height);
            Assert.Equal(head.Bounds.Width, rule.Bounds.Width);
        }

        host.Close();
    }

    [AvaloniaFact]
    public void ChangingPushToTalkEnablesTheMicrophoneResetAndPressingItPutsItBack()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "voice-input");

        var reset = Reset(Head(host.View, "Microphone")!);
        Assert.False(reset.IsEnabled);

        settings.Apply(ListeningCapability.PushToTalkKeyKey, "F9", SettingsCaller.Panel);
        Jobs();
        Assert.True(reset.IsEnabled);

        reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.False(settings.IsChanged(ListeningCapability.PushToTalkKeyKey));
        Assert.False(reset.IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void TheWakeWordHeadIsDrawnOnlyWhileItsRowsAre()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply(ListeningCapability.ModeKey, ListeningCapability.HoldMode, SettingsCaller.Panel);
        Open(host.View, "voice-input");

        Assert.False(OnPage(host.View, Head(host.View, "Wake word")));

        settings.Apply(ListeningCapability.ModeKey, ListeningCapability.WakeMode, SettingsCaller.Panel);
        Jobs();

        Assert.True(OnPage(host.View, Head(host.View, "Wake word")));
        Assert.True(OnPage(host.View, host.View.ControlFor(ListeningCapability.WakeWordsKey)));
        Assert.True(OnPage(host.View, host.View.ControlFor(ListeningCapability.WakeWindowKey)));

        host.Close();
    }

    [AvaloniaFact]
    public void TurningTheThinkingBedOffLeavesTheCuesGroupDrawn()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply(SpeechCapability.BedEnabledKey, "false", SettingsCaller.Panel);
        Open(host.View, "sounds");

        Assert.True(OnPage(host.View, Head(host.View, "Cues")));
        Assert.True(OnPage(host.View, host.View.ControlFor(SpeechCapability.CuesKey)));

        host.Close();
    }

    [AvaloniaFact]
    public void AFilterKeepsAGroupItsTitleMatchesAndHidesTheRest()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "voice");
        host.View.Filter("guardian voice");
        Jobs();

        Assert.True(OnPage(host.View, Head(host.View, "Guardian voice")));
        Assert.False(OnPage(host.View, Head(host.View, "What it costs")));

        host.Close();
    }

    [AvaloniaFact]
    public void EveryHeadsetGroupHasOneReset()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        settings.Apply(VrCapability.EnabledKey, "true", SettingsCaller.Panel);
        Open(host.View, "headset");

        foreach (var head in Heads(host.View))
        {
            Assert.Single(head.GetVisualDescendants().OfType<Button>());
        }

        Assert.NotNull(Head(host.View, "Panel placement"));

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1280, 860)]
    [InlineData(924, 640)]
    public void TheVoiceAndHearingPagesAreCaptured(double width, double height)
    {
        using var look = AppLook.Put();

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: height);

        foreach (var placeId in new[] { "voice-input", "voice", "sounds" })
        {
            Open(host.View, placeId);

            var path = Path.Combine(TestSurface.CaptureDirectory, $"group-heads-{placeId}-{width}x{height}.png");

            using (var frame = host.Window.CaptureRenderedFrame()!)
            {
                frame.Save(path, new PngBitmapEncoderOptions());
            }

            Assert.True(File.Exists(path));
        }

        host.Close();
    }
}
