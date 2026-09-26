using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>The stepper, the amount and the level draw the design's type and sizes, at 44 tall (#515).</summary>
public sealed class StepperAmountAndLevelTakeTheDesignsSizesTests
{
    [AvaloniaFact]
    public void AStepperValueIsSairaFourteenMediumInWhiteOnTheLeft()
    {
        using var look = AppLook.Put();
        var stepper = Shown(new Stepper { ItemsSource = ["Kokoro", "Piper"], SelectedIndex = 0 }, out var window);
        var value = Named(stepper, "StepperValue");

        Assert.Equal(new FontFamily(Fonts.ChromeFamily).Name, value.FontFamily.Name);
        Assert.Equal(TypeScale.ControlLarge, value.FontSize);
        Assert.Equal(FontWeight.Medium, value.FontWeight);
        Assert.Equal(TextAlignment.Left, value.TextAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, value.HorizontalAlignment);
        Assert.Equal(Colour(ThemeManager.WhiteKey), Solid(value.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void AStepperValueSitsOnSlabWithTwelveEitherSide()
    {
        using var look = AppLook.Put();
        var stepper = Shown(new Stepper { ItemsSource = ["Kokoro", "Piper"], SelectedIndex = 0 }, out var window);
        var cell = Named(stepper, "StepperValue").GetVisualAncestors().OfType<Border>().First();

        Assert.Equal(new Thickness(12, 0), cell.Padding);
        Assert.Equal(Colour(ThemeManager.SlabKey), Solid(cell.Background));
        Assert.Equal(TypeScale.MinimumTarget, cell.Bounds.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void AStepperCountIsMonoElevenInGrey2()
    {
        using var look = AppLook.Put();
        var stepper = Shown(new Stepper { ItemsSource = ["Kokoro", "Piper"], SelectedIndex = 0 }, out var window);
        var count = Named(stepper, "StepperPosition");

        Assert.Equal(new FontFamily(Fonts.MonoFamily).Name, count.FontFamily.Name);
        Assert.Equal(TypeScale.MetaSmall, count.FontSize);
        Assert.Equal(Colour(ThemeManager.Grey2Key), Solid(count.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void AnAmountIsMonoFourteenAtFortyFourTall()
    {
        using var look = AppLook.Put();
        var amount = Shown(new Amount { Value = 500, Unit = "ms" }, out var window);

        Assert.Equal(TypeScale.MinimumTarget, amount.Bounds.Height);

        var shown = Named(amount, "AmountValue");
        var editor = amount.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == "AmountEditor");

        Assert.Equal(new FontFamily(Fonts.MonoFamily).Name, shown.FontFamily.Name);
        Assert.Equal(TypeScale.ControlLarge, shown.FontSize);
        Assert.Equal(new FontFamily(Fonts.MonoFamily).Name, editor.FontFamily.Name);
        Assert.Equal(TypeScale.ControlLarge, editor.FontSize);

        window.Close();
    }

    [AvaloniaFact]
    public void ALevelsSegmentsAreTwentyTwoTallAndItsReadoutIsMonoThirteen()
    {
        using var look = AppLook.Put();
        var level = Shown(new Level { Value = 0.5 }, out var window);
        var readout = Named(level, "LevelReadout");

        Assert.All(Segments(level), segment => Assert.Equal(Level.SegmentHeight, segment.Bounds.Height));
        Assert.Equal(22, Level.SegmentHeight);
        Assert.Equal(new FontFamily(Fonts.MonoFamily).Name, readout.FontFamily.Name);
        Assert.Equal(TypeScale.Small, readout.FontSize);
        Assert.Equal(Colour(ThemeManager.WhiteKey), Solid(readout.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void AMutedLevelsReadoutTurnsGrey2AndBackToWhite()
    {
        using var look = AppLook.Put();
        var level = Shown(new Level { Value = 0.5, Muted = true }, out var window);
        var readout = Named(level, "LevelReadout");

        Assert.Equal(Colour(ThemeManager.Grey2Key), Solid(readout.Foreground));

        level.Muted = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Colour(ThemeManager.WhiteKey), Solid(readout.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void TheStepperAmountAndLevelAreCaptured()
    {
        using var look = AppLook.Put();

        var content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                new Stepper
                {
                    ItemsSource = ["Kokoro (local)", "Piper (local)", "Edge Neural"],
                    SelectedIndex = 0,
                    Consequences = ["~330 MB download", null, null],
                },
                new Amount { Value = 500, Unit = "ms" },
                new Amount { Format = "0.00", Unit = "$", Placeholder = "0.05" },
                new Level { Value = 0.85, Width = Level.CompactWidth },
                new Level { Value = 0.4, Muted = true, Width = Level.CompactWidth },
            },
        };

        AppLook.Capture(content, "stepper-amount-level.png", width: 480, height: 320);
    }

    private static T Shown<T>(T control, out Window window)
        where T : Control
    {
        control.VerticalAlignment = VerticalAlignment.Top;
        window = new Window { Content = control, Width = 480, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return control;
    }

    private static TextBlock Named(Control control, string name) =>
        control.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == name);

    private static IEnumerable<Border> Segments(Level level) =>
        level.GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 20)
            .Children.OfType<Border>();

    private static Color Colour(string key) =>
        Solid((IBrush?)Application.Current!.FindResource(key));

    private static Color Solid(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
