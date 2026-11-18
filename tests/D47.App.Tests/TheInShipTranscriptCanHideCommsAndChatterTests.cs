using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public sealed class TheInShipTranscriptCanHideCommsAndChatterTests
{
    private static PanelViewModel Talked()
    {
        var model = new PanelViewModel();

        model.Append("\nShip line.\n");
        model.Append("\nTower line.\n", speaker: "Tower", sourceKey: "k", origin: AnnouncementOrigin.Comms);
        model.Append("\nChatter line.\n", speaker: "Crew", sourceKey: "k", origin: AnnouncementOrigin.InventedChatter);
        model.Append("what is that", voice: TranscriptVoice.Commander);
        model.Mark("core changed");

        return model;
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, false)]
    public void TheConversationPageDropsRunsOfTheCheckedOrigins(bool hideComms, bool hideChatter, bool comms, bool chatter)
    {
        var model = Talked();
        model.HideComms = hideComms;
        model.HideInventedChatter = hideChatter;

        var origins = model.Segments(TranscriptPage.Conversation).Select(segment => segment.Origin).ToList();

        Assert.Equal(comms, origins.Contains(AnnouncementOrigin.Comms));
        Assert.Equal(chatter, origins.Contains(AnnouncementOrigin.InventedChatter));

        var text = string.Concat(model.Segments(TranscriptPage.Conversation).Select(segment => segment.Text));

        Assert.Contains("Ship line.", text);
        Assert.Contains("what is that", text);
        Assert.Contains("core changed", text);
    }

    [Fact]
    public void UncheckingShowsTheHiddenRunsAgain()
    {
        var model = Talked();
        var all = model.Segments(TranscriptPage.Conversation).Count;

        model.HideComms = true;
        model.HideInventedChatter = true;
        model.HideComms = false;
        model.HideInventedChatter = false;

        Assert.Equal(all, model.Segments(TranscriptPage.Conversation).Count);
    }

    [Fact]
    public void OtherPagesAreNotFiltered()
    {
        var model = Talked();
        model.HideComms = true;

        Assert.Contains(AnnouncementOrigin.Comms, model.Segments(TranscriptPage.Journal).Select(segment => segment.Origin));
    }

    [Fact]
    public void TheFiltersTakeNoRunOutOfTheTranscript()
    {
        var model = Talked();
        var before = model.TranscriptText;

        model.HideComms = true;
        model.HideInventedChatter = true;

        Assert.Equal(before, model.TranscriptText);
    }
}

[Trait("Category", "Integration")]
public sealed class TheInShipFiltersAreSettingsAndDrawnTests
{
    [Fact]
    public void BothRowsAreTogglesThatStartOff()
    {
        var settings = TestSurface.Settings();

        foreach (var key in new[] { InterfaceCapability.HideCommsKey, InterfaceCapability.HideInventedChatterKey })
        {
            var row = settings.Find(key)!;

            Assert.Equal(SettingKind.Toggle, row.Kind);
            Assert.False(string.IsNullOrWhiteSpace(row.Exercise));
            Assert.Equal("false", settings.Read(key));
        }

        Assert.Equal("Hide comms traffic", settings.Find(InterfaceCapability.HideCommsKey)!.Label);
        Assert.Equal("Hide invented chatter", settings.Find(InterfaceCapability.HideInventedChatterKey)!.Label);
    }

    [Fact]
    public void ChangingARowByVoiceMovesTheModelWithoutARestart()
    {
        var (settings, viewState, _) = TestSurface.Create();
        var model = Talked();

        TranscriptFilterSettings.Bind(viewState, settings, model, action => action());

        Assert.True(settings.Apply(InterfaceCapability.HideCommsKey, "true", SettingsCaller.Model).Ok);

        Assert.True(model.HideComms);
        Assert.False(model.HideInventedChatter);

        Assert.True(settings.Apply(InterfaceCapability.HideCommsKey, "false", SettingsCaller.Model).Ok);
        Assert.True(settings.Apply(InterfaceCapability.HideInventedChatterKey, "true", SettingsCaller.Model).Ok);

        Assert.False(model.HideComms);
        Assert.True(model.HideInventedChatter);
    }

    [Fact]
    public void ABoxCheckedInTheViewStateBeforeTheUpdateIsOnAfterIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var files = TestSurface.FilesFor(paths);
        var model = Talked();

        files.WriteText(paths.ViewStateFile, "{\"hideComms\": true, \"hideInventedChatter\": false, \"lastTab\": \"Transcript\"}");

        TranscriptFilterSettings.Bind(viewState, settings, model, action => action());

        Assert.True(settings.Current.Ui.HideComms);
        Assert.False(settings.Current.Ui.HideInventedChatter);
        Assert.True(model.HideComms);
        Assert.False(model.HideInventedChatter);

        var kept = files.ReadText(paths.ViewStateFile)!;

        Assert.DoesNotContain("hideComms", kept, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hideInventedChatter", kept, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Transcript", viewState.Load().LastTab);
    }

    [AvaloniaFact]
    public void TheTileIsOnTheTitleLineOfTheWindowAndTheBigPanelAndOpensBothRows()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        foreach (var headset in new[] { false, true })
        {
            var (settings, viewState, _) = TestSurface.Create();
            var model = Talked();
            TranscriptFilterSettings.Bind(viewState, settings, model, action => action());

            var strip = new SettingsView();
            strip.Attach(settings, viewState, tabPlaceId: "conversation-filters");

            var panel = new PanelView { DataContext = model };
            panel.EnableConversationSettings(() => strip);

            if (headset)
            {
                panel.Classes.Add("headset");
            }

            var window = new Window { Content = panel, Width = 1180, Height = 800, Background = (Avalonia.Media.IBrush?)Application.Current.Resources[ThemeManager.BgKey] };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            panel.Tab = PanelTab.Transcript;
            Dispatcher.UIThread.RunJobs();

            var tool = panel.GetControl<ContentControl>("PageTool");

            Assert.Null(panel.FindControl<Control>("ConversationFilterStrip"));
            Assert.Same(strip.BarTool, tool.Content);
            Assert.True(tool.IsVisible);
            Assert.Contains("SETTINGS 2", ((Button)strip.BarTool!).Content as string, StringComparison.Ordinal);

            ((Button)strip.BarTool!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var texts = strip.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

            Assert.Contains("Hide comms traffic", texts);
            Assert.Contains("Hide invented chatter", texts);

            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture(headset ? "in-ship-settings-panel.png" : "in-ship-settings-window.png");

            window.Close();
        }
    }

    private static PanelViewModel Talked()
    {
        var model = new PanelViewModel();

        model.Append("\nShip line.\n");
        model.Append("\nTower line.\n", speaker: "Tower", sourceKey: "k", origin: AnnouncementOrigin.Comms);
        model.Append("\nChatter line.\n", speaker: "Crew", sourceKey: "k", origin: AnnouncementOrigin.InventedChatter);

        return model;
    }
}
