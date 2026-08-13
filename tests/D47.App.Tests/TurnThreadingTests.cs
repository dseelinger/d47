using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A turn's events do not arrive on the UI thread.
/// <para>
/// <c>VoicePipeline.RunAsync</c> consumes them with <c>ConfigureAwait(false)</c>, so once the
/// first network await has actually suspended, every event after it — every text delta, and the
/// completion — is delivered on a thread pool thread. Anything the panel does in response has to
/// survive that, and a view that touches a control directly does not.
/// </para>
/// <para>
/// This went unseen until a language model was configured for the first time: with no provider
/// key stored there are no streamed deltas, so nothing ever arrived from off the UI thread.
/// </para>
/// </summary>
public class TurnThreadingTests
{
    /// <summary>
    /// The crash a Commander saw as "[turn failed: The calling thread cannot access this object
    /// because a different thread owns it.]" — with the reply already on screen, because the
    /// transcript had been written before the scroll that threw.
    /// </summary>
    [AvaloniaFact]
    public async Task AppendingATurnsTextFromOffTheUiThreadDoesNotThrow()
    {
        var model = Shown();

        var thrown = await OffTheUiThread(() => model.Append("a delta arriving mid-turn"));

        Assert.Null(thrown);
        Assert.Contains("a delta arriving mid-turn", model.TranscriptText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The end of a turn arrives the same way, and used to be lost with it: the exception took
    /// the whole turn down, so the completion line never ran either.
    /// </summary>
    [AvaloniaFact]
    public async Task SettingTheTurnLineFromOffTheUiThreadDoesNotThrow()
    {
        var model = Shown();

        var thrown = await OffTheUiThread(() => model.TurnLine = "routed: keyword");

        Assert.Null(thrown);
    }

    /// <summary>
    /// Many deltas, because the scroll is raised once per delta and a fix that marshals has to
    /// hold up under a stream rather than a single call.
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
    /// The same hazard on the settings path, which Phase 10 made reachable: every setting is
    /// settable by voice, and a tool handler runs on whatever thread the turn is on. Repainting
    /// the application's brushes from there is the same crash wearing different clothes.
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
    /// A panel bound into a shown window, which is what gives the scroll viewer a thread to be
    /// affine to. An unbound view model cannot reproduce this.
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

        // Anything the fix marshals runs as a dispatcher job, so it has to be pumped before the
        // assertions — otherwise a marshalled call that would still throw looks like a pass.
        Dispatcher.UIThread.RunJobs();

        return thrown;
    }
}
