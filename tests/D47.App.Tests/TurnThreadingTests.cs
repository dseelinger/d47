using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>A turn's events do not arrive on the UI thread.</summary>
public class TurnThreadingTests
{
    /// <summary>
    /// The crash a Commander saw as "[turn failed: The calling thread cannot access this object because
    /// a different thread owns it.]" — with the reply already on screen, because the transcript had
    /// been written before the scroll that threw.
    /// </summary>
    [AvaloniaFact]
    public async Task AppendingATurnsTextFromOffTheUiThreadDoesNotThrow()
    {
        var model = Shown();

        var thrown = await OffTheUiThread(() => model.Append("a delta arriving mid-turn"));

        Assert.Null(thrown);
        Assert.Contains("a delta arriving mid-turn", model.TranscriptText, StringComparison.Ordinal);
    }

    /// <summary>The end of a turn arrives off the UI thread too: an exception there takes the whole turn down, and the completion line with it.</summary>
    [AvaloniaFact]
    public async Task SettingTheTurnLineFromOffTheUiThreadDoesNotThrow()
    {
        var model = Shown();

        var thrown = await OffTheUiThread(() => model.TurnLine = "routed: keyword");

        Assert.Null(thrown);
    }

    /// <summary>
    /// Many deltas, because the scroll is raised once per delta and a fix that marshals has to hold up
    /// under a stream rather than a single call.
    /// </summary>
    [AvaloniaFact]
    public async Task AWholeStreamedReplyArrivesWithoutThrowing()
    {
        var model = Shown();

        var thrown = await OffTheUiThread(() =>
        {
            foreach (var word in "Welcome back Commander the expedition is holding steady".Split(' '))
            {
                model.Append(word + " ");
            }
        });

        Assert.Null(thrown);
        Assert.Contains("holding steady", model.TranscriptText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same hazard on the settings path, which Phase 10 made reachable: every setting is settable
    /// by voice, and a tool handler runs on whatever thread the turn is on.
    /// </summary>
    [AvaloniaFact]
    public async Task ChangingTheThemeFromOffTheUiThreadDoesNotThrow()
    {
        var (settings, _, _) = TestSurface.Create();

        new D47.App.Theming.ThemeManager(
            Avalonia.Application.Current!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .FollowSettings(settings);

        var thrown = await OffTheUiThread(() => settings.Apply(
            "ui.theme",
            D47.Core.Interface.ThemeCatalog.Guardian,
            D47.Core.Configuration.SettingsCaller.Model));

        Assert.Null(thrown);
    }

    /// <summary>
    /// A panel bound into a shown window, which is what gives the scroll viewer a thread to be affine
    /// to.
    /// </summary>
    private static PanelViewModel Shown()
    {
        var model = new PanelViewModel();

        new D47.App.Theming.ThemeManager(
            Avalonia.Application.Current!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(D47.Core.Interface.ThemeCatalog.Elite);

        var window = new Window { Width = 820, Height = 640, Content = new PanelView { DataContext = model } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return model;
    }

    private static async Task<Exception?> OffTheUiThread(Action work)
    {
        var thrown = await Task.Run(() =>
        {
            try
            {
                work();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        // Anything the fix marshals runs as a dispatcher job, so it has to be pumped before the assertions —
        // otherwise a marshalled call that would still throw looks like a pass.
        Dispatcher.UIThread.RunJobs();

        return thrown;
    }

    /// <summary>Two threads appending to one transcript, which is the shape the lock exists for.</summary>
    [Fact]
    public async Task TheTranscriptSurvivesTwoThreadsWritingToIt()
    {
        var panel = new PanelViewModel();

        var writers = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 250; i++)
            {
                panel.Append(
                    $"{worker}",
                    voice: worker % 2 == 0 ? TranscriptVoice.Ship : TranscriptVoice.Commander);
            }
        }));

        await Task.WhenAll(writers);

        // Every append is one character, and the Commander's runs are drawn with the marks a flat page puts
        // round them — so the count is asked of the buffer rather than of the drawing.
        Assert.Equal(1000, panel.Segments(TranscriptPage.Conversation, framed: false)
            .Sum(segment => segment.Text.Length));
    }
}
