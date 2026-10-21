using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>A scale row names its ends, lights the word the value is on and disables the arrow past it (#1466).</summary>
public sealed class ASerialToComedianScaleMarksItsEndsTests
{
    [AvaloniaFact]
    public void TheLowWordIsWhiteAtTheMinimumAndTheHighWordAtTheMaximum()
    {
        using var look = AppLook.Put();
        var track = new ScaleTrack("Serious", "Comedian", 0, 10);
        var window = new Window { Content = track, Width = 480, Height = 100 };
        window.Show();

        track.Show(0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Colour(ThemeManager.WhiteKey), InkOf(track, "SERIOUS"));
        Assert.Equal(Colour(ThemeManager.GreyKey), InkOf(track, "COMEDIAN"));

        track.Show(10);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Colour(ThemeManager.GreyKey), InkOf(track, "SERIOUS"));
        Assert.Equal(Colour(ThemeManager.WhiteKey), InkOf(track, "COMEDIAN"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheArrowPastAnEndIsDisabledOnlyWhereTheRowAsksForIt()
    {
        using var look = AppLook.Put();
        var scale = new Amount { Minimum = 0, Maximum = 10, DisablesAtEnds = true, Value = 0 };
        var plain = new Amount { Minimum = 0, Maximum = 10, Value = 0 };
        var window = new Window { Content = new StackPanel { Children = { scale, plain } }, Width = 480, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(Arrow(scale, "Decrease").IsEnabled);
        Assert.True(Arrow(scale, "Increase").IsEnabled);
        Assert.True(Arrow(plain, "Decrease").IsEnabled);

        scale.Value = 10;
        Assert.True(Arrow(scale, "Decrease").IsEnabled);
        Assert.False(Arrow(scale, "Increase").IsEnabled);

        window.Close();
    }

    private static Button Arrow(Amount amount, string name) =>
        amount.GetVisualDescendants().OfType<Button>()
            .First(button => Avalonia.Automation.AutomationProperties.GetName(button) == name);

    private static Color InkOf(ScaleTrack track, string word) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(
            track.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == word).Foreground).Color;

    private static Color Colour(string key) =>
        Assert.IsAssignableFrom<ISolidColorBrush>((IBrush?)Application.Current!.FindResource(key)).Color;
}
