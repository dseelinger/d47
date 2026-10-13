using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Stories tab's settings strip has a Cast reactions checkbox, and clearing it turns reactions off for every story.</summary>
public sealed class TheStoriesStripOffersCastReactionsTests
{
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ClearingCastReactionsOnTheStoriesTabTurnsThemOff()
    {
        using var look = AppLook.Put();

        var (settings, viewState, _) = TestSurface.Create();
        var view = new SettingsView();

        viewState.Save(viewState.Load().With("adventures", expanded: true));

        view.Attach(settings, viewState, tabPlaceId: "adventures");

        var window = new Window { Content = view, Width = 640, Height = 400 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("stories-strip-cast-reactions.png");
        }

        var box = Assert.IsType<CheckBox>(view.ControlFor(AdventureCapability.CastReactionsKey));

        Assert.True(box.IsEffectivelyVisible);
        Assert.True(box.IsChecked);

        box.IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(settings.Current.Callouts.CastReactions);
        Assert.Equal("false", settings.Read(AdventureCapability.CastReactionsKey));

        window.Close();
    }
}
