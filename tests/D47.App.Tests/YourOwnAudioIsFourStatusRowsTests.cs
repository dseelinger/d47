using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class YourOwnAudioIsFourStatusRowsTests
{
    private static OwnAudioReport Report()
    {
        var folders = new List<KeyValuePair<string, int>>
        {
            new("music/main-menu", 24),
            new("music/docked", 23),
            new("music/supercruise", 23),
            new("music/normal-space", 21),
            new("music/on-foot", 21),
            new("music/docking-computer", 15),
        };

        for (var index = 0; index < 32; index++)
        {
            folders.Add(new($"music/extra-{index:00}", 1));
        }

        return OwnAudioReport.Of(folders, [], ["engine-room: Windows has no decoder for this file."]);
    }

    private static SettingsView Open(OwnAudioReport report, Window window)
    {
        var (settings, viewState, _) = TestSurface.Create();

        settings.Apply(D47.Core.Capabilities.Builtin.InterfaceCapability.ShowEverySettingKey, "true", D47.Core.Configuration.SettingsCaller.Panel);
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var view = new SettingsView();
        view.Attach(settings, viewState, ownAudio: () => report, openAudioFolder: () => { });

        window.Content = view;
        window.Show();
        view.ShowPlaceOf(settings, AudioCapability.OwnMusicKey);
        Dispatcher.UIThread.RunJobs();

        return view;
    }

    [AvaloniaFact]
    public void TheGroupIsFourRowsAndTheWarningIsUnderTheFolder()
    {
        var window = new Window { Width = 980, Height = 900 };
        var view = Open(Report(), window);

        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Concat(t.Inlines?.Select(i => (i as Avalonia.Controls.Documents.Run)?.Text) ?? [])).ToList();

        Assert.Contains("159 files for 38 situations: main menu 24, docked 23, supercruise 23 and 35 more", texts);
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), b => b.Name == "OpenOwnAudio");
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), b => b.Name == "OpenAudioFolder");
        Assert.Contains(view.GetVisualDescendants().OfType<Notice>(), n => n.IsVisible && n.Name == "OwnAudioWarning");

        view.GetVisualDescendants().OfType<Notice>().First(n => n.Name == "OwnAudioWarning").BringIntoView();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("your-own-audio-rows.png");
    }

    [AvaloniaFact]
    public void ThePageListsEveryFolderAndEveryFileThatWasNotUsed()
    {
        var page = new OwnAudioPage(Report, () => { }, null);
        var window = new Window { Content = page, Width = 980, Height = 1100 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = page.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Contains("extra-31", texts);
        Assert.Contains("Skipped: engine-room: Windows has no decoder for this file.", texts);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("your-own-audio-page.png");
    }
}
