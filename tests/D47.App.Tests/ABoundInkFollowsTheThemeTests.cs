using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A property bound with <see cref="ThemeInk"/> takes the new brush when the theme changes.</summary>
public class ABoundInkFollowsTheThemeTests
{
    [AvaloniaFact]
    public void ATextBlockABorderAndARunFollowAThemeSwitch()
    {
        var themes = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);
        themes.Apply(ThemeCatalog.Dark);

        var text = new TextBlock { Text = "text" };
        var border = new Border();
        var run = new Run("run");
        text.Inlines = [run];
        var window = new Window { Content = new StackPanel { Children = { text, border } } };
        window.Show();

        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.AKey);
        ThemeInk.Bind(run, TextElement.ForegroundProperty, ThemeManager.GreyKey);

        var before = Colour(text.Foreground);
        themes.Apply(ThemeCatalog.Light);

        Assert.NotEqual(before, Colour(text.Foreground));
        Assert.Equal(Colour(Brush(ThemeManager.WhiteKey)), Colour(text.Foreground));
        Assert.Equal(Colour(Brush(ThemeManager.AKey)), Colour(border.Background));
        Assert.Equal(Colour(Brush(ThemeManager.GreyKey)), Colour(run.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void ColouringTwiceEndsOnTheSecondKeyAfterAThemeSwitch()
    {
        var themes = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);
        themes.Apply(ThemeCatalog.Dark);

        var text = new TextBlock { Text = "text" };
        var window = new Window { Content = text };
        window.Show();

        var first = ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        first.Dispose();
        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        themes.Apply(ThemeCatalog.Light);

        Assert.Equal(Colour(Brush(ThemeManager.WhiteKey)), Colour(text.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void AControlBoundBeforeItIsShownTakesItsInkWhenItIsAttached()
    {
        var themes = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);
        themes.Apply(ThemeCatalog.Dark);

        var text = new TextBlock { Text = "text" };
        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        var window = new Window { Content = text };
        window.Show();

        Assert.Equal(Colour(Brush(ThemeManager.CyanKey)), Colour(text.Foreground));

        themes.Apply(ThemeCatalog.Light);

        Assert.Equal(Colour(Brush(ThemeManager.CyanKey)), Colour(text.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void ADroppedControlIsNotKeptByItsInk()
    {
        var themes = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);
        themes.Apply(ThemeCatalog.Dark);

        var host = new StackPanel();
        var window = new Window { Content = host };
        window.Show();

        var shown = ShownThenDropped(host);
        var never = BoundNeverShown();

        for (var pass = 0; pass < 3 && (shown.IsAlive || never.IsAlive); pass++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(never.IsAlive);
        Assert.False(shown.IsAlive);

        window.Close();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ShownThenDropped(StackPanel host)
    {
        var text = new TextBlock { Text = "text", Inlines = [new Run("run")] };
        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(text.Inlines![0], TextElement.ForegroundProperty, ThemeManager.GreyKey);

        host.Children.Add(text);
        host.UpdateLayout();
        host.Children.Clear();
        host.UpdateLayout();

        return new WeakReference(text);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference BoundNeverShown()
    {
        var text = new TextBlock { Text = "text" };
        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return new WeakReference(text);
    }

    private static IBrush? Brush(string key) =>
        Application.Current!.Resources.TryGetResource(key, null, out var value) ? value as IBrush : null;

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;
}
