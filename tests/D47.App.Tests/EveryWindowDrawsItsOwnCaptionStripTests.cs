using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
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
            .Single(t => t.Text == "BEFORE" && !BloomStack.IsGhost(t));

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

    [AvaloniaFact]
    public void TheNameAndVersionAreSetInTheDesignsTitleBarType()
    {
        using var look = AppLook.Put();
        var window = new Window { Content = new TextBlock(), Title = "Directive 47 — 0.1.0" };
        CaptionStrip.Apply(window);
        window.Show();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => !BloomStack.IsGhost(t)).ToList();
        var name = texts.Single(t => t.Text == "DIRECTIVE 47");
        var version = texts.Single(t => t.Text == "0.1.0");

        Assert.Equal(19, name.FontSize);
        Assert.Equal(FontWeight.SemiBold, name.FontWeight);
        Assert.Equal(19 * 0.12, name.LetterSpacing, 3);
        Assert.Equal(Colour(ThemeManager.WhiteKey), (name.Foreground as ISolidColorBrush)?.Color);

        Assert.Equal(Fonts.MonoFamily, version.FontFamily.ToString());
        Assert.Equal(12, version.FontSize);
        Assert.Equal(Colour(ThemeManager.Grey2Key), (version.Foreground as ISolidColorBrush)?.Color);
    }

    [AvaloniaFact]
    public void TheRowIsInsetEighteenWithFourteenBetweenItsParts()
    {
        var window = new Window { Content = new TextBlock(), Title = "Directive 47 — 0.1.0" };
        CaptionStrip.Apply(window);
        window.Show();

        // The version shares a row of its own with anything drawn after it.
        var version = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "0.1.0");
        var row = (StackPanel)((StackPanel)version.Parent!).Parent!;

        Assert.Equal(18, row.Margin.Left);
        Assert.Equal(14, row.Spacing);
        Assert.All(row.Children, child => Assert.Equal(0, child.Margin.Right));
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

    private static Color Colour(string key) =>
        ((SolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color? Ground(Button button) => (button.Background as ISolidColorBrush)?.Color;

    private static Color? Ink(Button button) => (((Avalonia.Controls.Shapes.Path)button.Content!).Stroke as ISolidColorBrush)?.Color
        ?? (((Avalonia.Controls.Shapes.Path)button.Content!).Fill as ISolidColorBrush)?.Color;

    private static (Window Window, Button Minimize, Button Maximize, Button Close) Themed()
    {
        var window = new Window { Content = new TextBlock(), CanResize = true, Width = 400, Height = 300 };
        CaptionStrip.Apply(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button Named(string name) =>
            Buttons(window).Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);

        return (window, Named("Minimize"), Named("Maximize"), Named("Close"));
    }

    private static void PointAt(Window window, Button button) =>
        window.MouseMove(button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value);

    [AvaloniaFact]
    public void AtRestNoCaptionButtonHasAGroundAndEachGlyphIsGrey()
    {
        using var look = AppLook.Put();
        var (_, minimize, maximize, close) = Themed();

        Assert.All([minimize, maximize, close], button =>
        {
            Assert.Equal(Colors.Transparent, Ground(button));
            Assert.Equal(Colour(ThemeManager.GreyKey), Ink(button));
        });
    }

    [AvaloniaFact]
    public void MinimiseAndMaximiseHoverToATileGroundWithAnAGlyph()
    {
        using var look = AppLook.Put();
        var (window, minimize, maximize, _) = Themed();

        foreach (var button in new[] { minimize, maximize })
        {
            PointAt(window, button);

            Assert.Equal(Colour(ThemeManager.TileKey), Ground(button));
            Assert.Equal(Colour(ThemeManager.AKey), Ink(button));
        }

        Assert.Equal(Colors.Transparent, Ground(minimize));
    }

    [AvaloniaFact]
    public void CloseHoversToSolidRedWithAWhiteGlyph()
    {
        using var look = AppLook.Put();
        var (window, _, _, close) = Themed();

        PointAt(window, close);

        Assert.Equal(Colour(ThemeManager.RedKey), Ground(close));
        Assert.Equal(Colour(ThemeManager.WhiteKey), Ink(close));
    }

    [AvaloniaFact]
    public void KeyboardFocusFillsACaptionButtonAsHoverDoes()
    {
        using var look = AppLook.Put();
        var (_, minimize, _, close) = Themed();

        minimize.Focus(Avalonia.Input.NavigationMethod.Tab);
        Assert.Equal(Colour(ThemeManager.TileKey), Ground(minimize));
        Assert.Equal(Colour(ThemeManager.AKey), Ink(minimize));

        close.Focus(Avalonia.Input.NavigationMethod.Tab);
        Assert.Equal(Colour(ThemeManager.RedKey), Ground(close));
        Assert.Equal(Colour(ThemeManager.WhiteKey), Ink(close));
        Assert.Equal(Colors.Transparent, Ground(minimize));
    }

    [AvaloniaFact]
    public void TheCaptionButtonsAreCapturedAtRestHoveredAndFocused()
    {
        using var look = AppLook.Put();
        var (window, minimize, maximize, close) = Themed();

        void Save(string state)
        {
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame()!;
            frame.Save(Path.Combine(TestSurface.CaptureDirectory, $"caption-buttons-{state}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        Save("rest");
        PointAt(window, minimize);
        Save("hover-minimise");
        PointAt(window, close);
        Save("hover-close");
        window.MouseMove(new Point(20, 200));
        maximize.Focus(Avalonia.Input.NavigationMethod.Tab);
        Save("focus-maximise");

        Assert.True(File.Exists(Path.Combine(TestSurface.CaptureDirectory, "caption-buttons-focus-maximise.png")));
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

}
