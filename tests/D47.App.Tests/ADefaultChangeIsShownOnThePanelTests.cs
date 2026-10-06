using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The panel shows a model default change with one action and a dismiss, and hides it when cleared.</summary>
public class ADefaultChangeIsShownOnThePanelTests
{
    [AvaloniaFact]
    public void TheNoticeShowsItsTextAndRoutesBothButtons()
    {
        var model = new PanelViewModel();
        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 900, Height = 600 };
        window.Show();

        var notice = panel.FindControl<Notice>("DefaultChangeBanner")!;

        Assert.False(notice.IsVisible);

        model.DefaultChangeText = "D47 now answers with Claude Sonnet 5.5.";
        model.DefaultChangeAction = "Keep Claude Sonnet 5";
        Dispatcher.UIThread.RunJobs();

        Assert.True(notice.IsVisible);
        Assert.Equal("Keep Claude Sonnet 5", panel.FindControl<Button>("DefaultChangeActionButton")!.Content);

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, "default-change-notice.png"), new PngBitmapEncoderOptions());

        var accepted = 0;
        var dismissed = 0;
        model.DefaultChangeAccepted += () => accepted++;
        model.DefaultChangeDismissed += () => dismissed++;

        model.AcceptDefaultChange();
        model.DismissDefaultChange();

        Assert.Equal(1, accepted);
        Assert.Equal(1, dismissed);

        model.DefaultChangeText = null;
        Dispatcher.UIThread.RunJobs();

        Assert.False(notice.IsVisible);

        window.Close();
    }
}
