using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Callouts;
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
public sealed class TheInShipFiltersAreKeptAndDrawnTests
{
    [Fact]
    public void ViewStateKeepsBothBooleans()
    {
        var store = Store();

        store.Save(store.Load() with { HideComms = true, HideInventedChatter = true });

        var loaded = store.Load();

        Assert.True(loaded.HideComms);
        Assert.True(loaded.HideInventedChatter);
    }

    [Fact]
    public void TheModelStartsWithTheChoiceItWasLeftWith()
    {
        var store = Store();
        var first = new PanelViewModel();
        _ = new TranscriptFilterMemory(store, first);

        first.HideInventedChatter = true;

        var next = new PanelViewModel();
        _ = new TranscriptFilterMemory(store, next);

        Assert.False(next.HideComms);
        Assert.True(next.HideInventedChatter);
    }

    [AvaloniaFact]
    public void TheRowIsDrawnOnTheWindowAndTheBigPanelAtFortyFourPixels()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        foreach (var headset in new[] { false, true })
        {
            var model = Talked();
            var panel = new PanelView { DataContext = model };

            if (headset)
            {
                panel.Classes.Add("headset");
            }

            var window = new Window { Content = panel, Width = 1180, Height = 800, Background = (Avalonia.Media.IBrush?)Application.Current.Resources[ThemeManager.BgKey] };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            panel.Tab = PanelTab.Transcript;
            Dispatcher.UIThread.RunJobs();

            var strip = panel.GetControl<StackPanel>("ConversationFilterStrip");
            var comms = panel.GetControl<CheckBox>("HideCommsBox");

            Assert.True(strip.IsVisible);
            Assert.True(comms.Bounds.Height >= 44);

            comms.IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            Assert.True(model.HideComms);

            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture(headset ? "in-ship-filters-panel.png" : "in-ship-filters-window.png");

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

    private static ViewStateStore Store() =>
        new(
            new AppPaths(TempFolders.Create("d47-in-ship-filters-tests")),
            new DiskFileSystem(),
            NullLogger<ViewStateStore>.Instance);
}
