using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The caption strip (#286): drawn once by <see cref="CaptionStrip"/>, applied to every window either
/// directly (<see cref="MainWindow"/>) or through <see cref="Dialogs.Over"/> — which
/// <see cref="DialogsMatchTheWindowsZoomTests.NothingCallsShowDialogDirectly"/> already proves is the
/// only way any other window in the list is shown.
/// </summary>
public class EveryWindowDrawsItsOwnCaptionStripTests
{
    private static Control Strip(Window window)
    {
        window.Show();
        return (Control)window.GetVisualDescendants().Single(c => c.Name == "CaptionStrip");
    }

    private static IReadOnlyList<Button> Buttons(Window window)
    {
        window.Show();
        return [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("caption-button"))];
    }

    [AvaloniaFact]
    public void TheStripCarriesTheWindowsTitleAndFollowsIt()
    {
        var window = new Window { Content = new TextBlock(), Title = "Before" };
        CaptionStrip.Apply(window);
        window.Show();

        var title = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "BEFORE");

        window.Title = "After";

        Assert.Equal("AFTER", title.Text);
    }

    [AvaloniaFact]
    public void TheVersionAfterTheDashIsSetApartFromTheName()
    {
        var window = new Window { Content = new TextBlock(), Title = "Directive 47 — 0.1.0" };
        CaptionStrip.Apply(window);
        window.Show();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Contains("DIRECTIVE 47", texts);
        Assert.Contains("0.1.0", texts);
    }

    /// <summary>Avalonia 12 draws its own title and caption buttons into an extended client area on
    /// Windows; left in, the title shows twice, one over the other.</summary>
    [AvaloniaFact]
    public void AvaloniaDrawsNoTitlebarOfItsOwnUnderTheStrip()
    {
        var window = new Window { Content = new TextBlock() };
        CaptionStrip.Apply(window);

        var template = Assert.Single(window.WindowDecorationsTheme!.Setters.OfType<Avalonia.Styling.Setter>(),
            s => s.Property == Avalonia.Controls.Chrome.WindowDrawnDecorations.TemplateProperty);
        var content = Assert.IsAssignableFrom<Avalonia.Controls.Chrome.IWindowDrawnDecorationsTemplate>(template.Value).Build().Result;
        Assert.Null(content.Overlay);
        Assert.Null(content.Underlay);
    }

    [AvaloniaFact]
    public void AResizableWindowShowsMinimiseMaximiseAndClose()
    {
        var window = new Window { Content = new TextBlock(), CanResize = true };
        CaptionStrip.Apply(window);

        Assert.Equal(3, Buttons(window).Count);
    }

    [AvaloniaFact]
    public void ANonResizableWindowShowsNoMaximiseButton()
    {
        var window = new Window { Content = new TextBlock(), CanResize = false };
        CaptionStrip.Apply(window);

        Assert.Equal(2, Buttons(window).Count);
    }

    [AvaloniaFact]
    public void ShowMinimiseFalseLeavesOnlyClose()
    {
        var window = new Window { Content = new TextBlock(), CanResize = false };
        CaptionStrip.Apply(window, showMinimize: false);

        Assert.Single(Buttons(window));
    }

    [AvaloniaFact]
    public void ThePresentButtonsAreMinimiseMaximiseAndCloseInWindowsOrder()
    {
        var window = new Window { Content = new TextBlock(), CanResize = true };
        CaptionStrip.Apply(window);

        var names = Buttons(window).Select(b => Avalonia.Automation.AutomationProperties.GetName(b) ?? "").ToArray();

        Assert.Equal(["Minimize", "Maximize", "Close"], names);
    }

    [AvaloniaFact]
    public void CloseClosesTheWindow()
    {
        var window = new Window { Content = new TextBlock() };
        CaptionStrip.Apply(window);
        window.Show();

        var close = Buttons(window).Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Close");
        close.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void MaximiseTogglesWindowStateAndTheGlyphFollows()
    {
        var window = new Window { Content = new TextBlock(), CanResize = true };
        CaptionStrip.Apply(window);

        var maximize = Buttons(window).Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Maximize");

        maximize.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal("Restore", Avalonia.Automation.AutomationProperties.GetName(maximize));

        maximize.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal("Maximize", Avalonia.Automation.AutomationProperties.GetName(maximize));
    }

    /// <summary>The strip sets the hints that extend the client area into the space the native
    /// titlebar used to paint, and reserves exactly its own height there.</summary>
    [AvaloniaFact]
    public void TheWindowExtendsIntoItsOwnDecorations()
    {
        var window = new Window { Content = new TextBlock() };
        CaptionStrip.Apply(window);

        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(CaptionStrip.StripHeight, window.ExtendClientAreaTitleBarHeightHint);
    }

    /// <summary>Windows keeps deciding resizing, snapping and the system menu — nothing here turns
    /// decorations off, only draws over where they used to paint (#286).</summary>
    [AvaloniaFact]
    public void WindowDecorationsAreLeftAlone()
    {
        var window = new Window { Content = new TextBlock(), WindowDecorations = WindowDecorations.Full };
        CaptionStrip.Apply(window);

        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
    }

    [AvaloniaFact]
    public void TheOriginalContentIsStillInTheTree()
    {
        var original = new TextBlock { Text = "the page" };
        var window = new Window { Content = original };
        CaptionStrip.Apply(window);
        window.Show();

        Assert.Contains(original, window.GetVisualDescendants());
    }

    /// <summary>Every caption button carries a name a screen reader and the focus-visible outline both
    /// read off — the default Avalonia focus adorner keys on the same automation identity.</summary>
    [AvaloniaFact]
    public void EveryCaptionButtonIsFocusable()
    {
        var window = new Window { Content = new TextBlock(), CanResize = true };
        CaptionStrip.Apply(window);

        Assert.All(Buttons(window), button => Assert.True(button.Focusable));
    }

    [AvaloniaFact]
    public void CloseHoversToDangerAndMinimiseHoversToTheFaintFill()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var window = new Window { Content = new TextBlock(), CanResize = true, Width = 400, Height = 300 };
        CaptionStrip.Apply(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var close = Buttons(window).Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Close");
        var minimize = Buttons(window).Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Minimize");

        var closeAt = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value;
        window.MouseMove(closeAt);

        var dangerBrush = (IBrush)Application.Current!.Resources[D47.App.Theming.ThemeManager.DangerKey]!;
        Assert.Equal(((SolidColorBrush)dangerBrush).Color, ((SolidColorBrush)close.Background!).Color);

        var minimizeAt = minimize.TranslatePoint(new Point(minimize.Bounds.Width / 2, minimize.Bounds.Height / 2), window)!.Value;
        window.MouseMove(minimizeAt);

        var fillHighBrush = (IBrush)Application.Current!.Resources[D47.App.Theming.ThemeManager.FillHighKey]!;
        Assert.Equal(((SolidColorBrush)fillHighBrush).Color, ((SolidColorBrush)minimize.Background!).Color);

        // And the pointer moving on drops the one it left back to nothing.
        Assert.Equal(Brushes.Transparent, close.Background);
    }

    // -- The two ways a window actually gets the strip in the running app --

    /// <summary>Every window but MainWindow is shown with <c>.Over(owner)</c>, and that call wraps it
    /// in the same strip (#286).</summary>
    [AvaloniaFact]
    public void OverAddsTheCaptionStrip()
    {
        var owner = new Window { Content = new TextBlock() };
        owner.Show();

        var dialog = new Window { Content = new TextBlock(), Title = "A dialog" };

        _ = dialog.Over(owner);

        Assert.NotNull(Strip(dialog));

        dialog.Close();
        owner.Close();
    }

    [AvaloniaFact]
    public void MainWindowAppliesTheStripInItsOwnConstructor()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var window = new MainWindow(host: null);

        Assert.NotNull(Strip(window));
    }

    // -- A sample of the real windows, not just a synthetic one --

    [AvaloniaFact]
    public void ChangelogWindowCarriesTheStrip()
    {
        var window = new ChangelogWindow("- did a thing");
        CaptionStrip.Apply(window);

        Assert.NotNull(Strip(window));
    }

    [AvaloniaFact]
    public void ConfirmWindowShowsMinimiseAndCloseOnly()
    {
        var window = new ConfirmWindow("Sure?", "Really?", "Yes", "No");
        CaptionStrip.Apply(window);

        Assert.Equal(2, Buttons(window).Count);
    }

    /// <summary>Not ShowInTaskbar, so a minimised picker would have no way back — the reason
    /// <see cref="PickerWindow.ShowAsync"/> passes <c>showMinimize: false</c> (#286).</summary>
    [AvaloniaFact]
    public void PickerWindowShowsCloseOnly()
    {
        var window = PickerWindow.For(new PickerRequest { Prompt = "Choose one" });
        CaptionStrip.Apply(window, showMinimize: false);

        Assert.Single(Buttons(window));
    }

    [AvaloniaFact]
    public void HelpImproveWindowCarriesTheStrip()
    {
        var window = new Controls.HelpImproveWindow(
            new DateTimeOffset(2026, 9, 1, 21, 0, 0, TimeSpan.Zero),
            _ => "a line",
            destination: "donations.example");
        CaptionStrip.Apply(window);

        Assert.NotNull(Strip(window));
    }
}
