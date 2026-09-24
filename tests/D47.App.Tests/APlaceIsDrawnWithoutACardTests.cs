using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// A place's groups are drawn straight onto the page (#435): no card to open or shut, no bulk controls,
/// and no HELP of its own — the top bar's HELP is the one route to its guide.
/// </summary>
public sealed class APlaceIsDrawnWithoutACardTests
{
    [AvaloniaTheory]
    [InlineData("voice-input")]
    [InlineData("voice")]
    [InlineData("sounds")]
    public void NoCardChromeIsDrawnOnThePage(string placeId)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, placeId);

        var buttons = host.View.GetVisualDescendants().OfType<Button>().ToList();

        Assert.DoesNotContain(buttons, button => button.Name is "ExpandAll" or "CollapseAll");
        Assert.DoesNotContain(buttons, button => button.Content as string == "HELP");

        // A row's own picker draws a ▾ inside its button; a collapse chevron stood on its own.
        Assert.DoesNotContain(
            Page(host.View).GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text is "▾" or "▸"
                    && !text.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.TemplatedControl>().Any());

        // The page's direct children are the head, the place's rows and the lines around them; none is a
        // bordered box.
        Assert.DoesNotContain(
            Page(host.View).Children.OfType<Border>(),
            border => border.IsVisible && border.BorderThickness != default);

        host.Close();
    }
}
