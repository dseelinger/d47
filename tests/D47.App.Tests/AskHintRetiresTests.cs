using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The ask box's worked example is onboarding, and it retires once the Commander has asked
/// anything at all (docs/plans/change-requests.md item 5).
/// <para>
/// What it must not do is come back. That is why the fact is written down rather than worked
/// out: the live signal that would imply it — a conversation with something in it — does not
/// survive a restart, so every launch would look like a first one.
/// </para>
/// </summary>
public class AskHintRetiresTests
{
    private const string Example = "where am I";

    private static (Window Window, PanelView View) Open(PanelViewModel model)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static string Placeholder(PanelView view) =>
        ((TextBox)view.FindControl<Control>("AskBox")!).PlaceholderText ?? string.Empty;

    [AvaloniaFact]
    public void TheExampleGoesOnceTheCommanderHasAsked()
    {
        var model = new PanelViewModel();
        var (window, view) = Open(model);

        Assert.Contains(Example, Placeholder(view), StringComparison.OrdinalIgnoreCase);

        model.HasAsked = true;
        Dispatcher.UIThread.RunJobs();

        // The example goes; the label does not. A text box with no placeholder at all is a box.
        Assert.DoesNotContain(Example, Placeholder(view), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Ask D47 something", Placeholder(view));

        window.Close();
    }

    /// <summary>
    /// A Commander who has asked before never sees the example again, and never sees it appear
    /// and then vanish either — it is read before the first paint.
    /// </summary>
    [AvaloniaFact]
    public void ACommanderWhoHasAlreadyAskedNeverSeesIt()
    {
        var model = new PanelViewModel { HasAsked = true };
        var (window, view) = Open(model);

        Assert.Equal("Ask D47 something", Placeholder(view));

        window.Close();
    }

    /// <summary>
    /// Both surfaces show the ask box and there is one Commander behind them, so the fact lives
    /// on the model. Held per view, the desktop window would retire the hint while the headset
    /// went on offering it.
    /// </summary>
    [AvaloniaFact]
    public void BothSurfacesAgreeBecauseTheFactIsOnTheModel()
    {
        var model = new PanelViewModel();
        var (window, desktop) = Open(model);

        var headset = new PanelView { DataContext = model };
        var second = new Window { Content = headset, Width = 640, Height = 480 };
        second.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Example, Placeholder(headset), StringComparison.OrdinalIgnoreCase);

        model.HasAsked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ask D47 something", Placeholder(desktop));
        Assert.Equal("Ask D47 something", Placeholder(headset));

        second.Close();
        window.Close();
    }

    /// <summary>
    /// Set from a background thread without throwing, like every other model property a turn can
    /// raise. The placeholder is applied in code rather than bound precisely so this route
    /// exists: a binding subscribes to every property change and would be woken on the turn's
    /// own thread by each streamed delta.
    /// </summary>
    [AvaloniaFact]
    public async Task ItCanBeSetFromAnotherThread()
    {
        var model = new PanelViewModel();
        var (window, view) = Open(model);

        await Task.Run(() => model.HasAsked = true);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ask D47 something", Placeholder(view));

        window.Close();
    }

    /// <summary>
    /// The point of writing it down: it survives the process. A launch by someone who has asked
    /// before and a launch by someone who never has are indistinguishable from live state, so
    /// without this the hint would return every time and go on teaching a Commander a month in.
    /// </summary>
    [Fact]
    public void TheFactSurvivesARestart()
    {
        var root = TempFolders.Create("d47-ask-hint-tests");
        var paths = new AppPaths(root);

        var store = new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance);
        Assert.False(store.Load().HasAsked);

        store.Save(store.Load() with { HasAsked = true });

        // A second store over the same folder, which is what the next launch has.
        Assert.True(new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance).Load().HasAsked);
    }

    /// <summary>
    /// Both states, for a human to look at. Whether the shorter one still reads as an invitation
    /// rather than as a box that lost its label is a question only eyes settle.
    /// </summary>
    [AvaloniaFact]
    public void BothHintsRenderToACapture()
    {
        var model = new PanelViewModel();
        var (window, _) = Open(model);

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "ask-hint-first-run.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        model.HasAsked = true;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "ask-hint-retired.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}
