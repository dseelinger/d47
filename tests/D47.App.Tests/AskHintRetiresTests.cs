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
/// The ask box's worked example is onboarding, and it retires once the Commander has asked anything at
/// all.
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

        // The example goes; the label does not.
        Assert.DoesNotContain(Example, Placeholder(view), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Ask D47 something", Placeholder(view));

        window.Close();
    }

    /// <summary>
    /// A Commander who has asked before never sees the example again, and never sees it appear and then
    /// vanish either — it is read before the first paint.
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
    /// Both surfaces show the ask box and there is one Commander behind them, so the fact lives on the
    /// model.
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
    /// Set from a background thread without throwing, like every other model property a turn can raise.
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

    /// <summary>The point of writing it down: it survives the process.</summary>
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

    /// <summary>Both states, for a human to look at.</summary>
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
