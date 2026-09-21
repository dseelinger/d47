using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The segmented choice's own frame, ground, ink and wrapping (#348).</summary>
public class SegmentedChoiceGetsAFrameAndWrapsTests
{
    private static (Window Window, Segment Segment) Open(double width = 900)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        // Not in HeadlessApp — it stands in for App.axaml but leaves the control kit out — so the
        // D47.Segment theme this test exercises has to come from the real file.
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var segment = new Segment
        {
            ItemsSource = ["Reach: near here", "Reach: a session's flying", "Reach: anywhere", "Reach: the edge"],
            SelectedIndex = 0,
        };
        var window = new Window { Content = segment, Width = width, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (window, segment);
    }

    [AvaloniaFact]
    public void TheGroupIsOneFramedBlockOnItsOwnGroundAndItsSegmentsCarryNoBorder()
    {
        var (_, segment) = Open();

        var frame = Assert.IsType<Border>(segment.Content);

        Assert.Equal(new Thickness(1), frame.BorderThickness);
        Assert.Equal(new Thickness(3), frame.Padding);

        var resources = Application.Current!.Resources;
        Assert.Equal(resources[ThemeManager.RuleKey], frame.BorderBrush);
        Assert.Equal(resources[ThemeManager.FillLowKey], frame.Background);

        foreach (var button in frame.GetVisualDescendants().OfType<RadioButton>())
        {
            Assert.Equal(new Thickness(0), button.BorderThickness);
        }
    }

    [AvaloniaFact]
    public void SelectedIsAccentFillWithKnockInkAndUnselectedIsTextFaint()
    {
        var (_, segment) = Open();

        var buttons = segment.GetVisualDescendants().OfType<RadioButton>().ToList();
        var resources = Application.Current!.Resources;

        var selected = buttons.Single(button => button.IsChecked == true);
        var unselected = buttons.Where(button => button.IsChecked != true);

        Assert.Equal(resources[ThemeManager.KnockKey], selected.Foreground);

        foreach (var button in unselected)
        {
            Assert.Equal(resources[ThemeManager.TextFaintKey], button.Foreground);
        }
    }

    [AvaloniaFact]
    public void AFourOptionGroupWrapsInsteadOfClippingAtFiveTwelvePixels()
    {
        var (_, segment) = Open(width: 512);

        var rows = segment.GetVisualDescendants().OfType<RadioButton>()
            .Select(button => button.Bounds.Y)
            .Distinct()
            .Count();

        Assert.True(rows > 1, "a four-option group at 512px should wrap onto more than one row");
    }

    [AvaloniaFact]
    public void ArrowKeysStillMoveTheSelection()
    {
        var (window, segment) = Open();

        segment.GetVisualDescendants().OfType<RadioButton>().First().Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, segment.SelectedIndex);
    }
}
