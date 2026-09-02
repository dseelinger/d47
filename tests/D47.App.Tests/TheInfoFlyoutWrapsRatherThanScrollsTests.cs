using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The <c>ⓘ</c> flyout shows every line of its reasoning whole, with no horizontal scrollbar
/// (<a href="https://github.com/dseelinger/d47/issues/271">#271</a>).
/// <para>
/// The theme's presenter caps its own width and then scrolls sideways when the content is wider,
/// and the content was capped at a number a few dozen pixels past what the presenter shows — so
/// every paragraph was clipped at the right edge with a scrollbar under it. What is asserted is
/// the measure rather than the numbers: the presenter's viewer must have nothing to scroll to
/// sideways, whatever the theme's cap and padding happen to be.
/// </para>
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

        // Sideways, the content is exactly as wide as the hole it is shown through. Downwards the
        // presenter may scroll, so only the width is the claim.
        Assert.True(
            viewer.Extent.Width <= viewer.Viewport.Width,
            $"the flyout's content is {viewer.Extent.Width} wide in a viewport {viewer.Viewport.Width} wide");
        Assert.Equal(ScrollBarVisibility.Disabled, viewer.HorizontalScrollBarVisibility);

        flyout.Hide();
    }
}
