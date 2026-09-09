using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The <c>ⓘ</c> flyout shows every line of its reasoning whole, with no horizontal scrollbar.
/// </summary>
public sealed class TheInfoFlyoutWrapsRatherThanScrollsTests
{
    [AvaloniaFact]
    public void TheReasoningIsNoWiderThanTheFlyoutShows()
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            _ => "an excerpt",
            destination: "https://donations.example/store");
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var info = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "HelpImproveInfo");
        var flyout = Assert.IsType<Flyout>(info.Flyout);
        flyout.ShowAt(info);
        Dispatcher.UIThread.RunJobs();

        var content = Assert.IsType<StackPanel>(flyout.Content);
        var presenter = content.FindAncestorOfType<FlyoutPresenter>();
        Assert.NotNull(presenter);

        var viewer = presenter.GetVisualDescendants().OfType<ScrollViewer>().First();

        // Sideways, the content is exactly as wide as the hole it is shown through.
        Assert.True(
            viewer.Extent.Width <= viewer.Viewport.Width,
            $"the flyout's content is {viewer.Extent.Width} wide in a viewport {viewer.Viewport.Width} wide");
        Assert.Equal(ScrollBarVisibility.Disabled, viewer.HorizontalScrollBarVisibility);

        flyout.Hide();
    }
}
