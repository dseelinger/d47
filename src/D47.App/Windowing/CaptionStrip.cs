using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Windowing;

/// <summary>
/// Draws d47's own caption row in place of the native titlebar: a diamond, the title in spaced capitals
/// and any version after its dash on the left,
/// minimise, maximise/restore and close on the right (#286). Windows keeps resizing, Aero Snap,
/// Win+arrow and the Alt+Space system menu, because <see cref="Window.WindowDecorations"/> stays
/// as it was — this only extends the client area into the space that chrome used to paint.
/// </summary>
public static class CaptionStrip
{
    public const double StripHeight = 44;
    private const double ButtonWidth = 46;
    private const double TitleSize = 23;
    private const double TitleTracking = TitleSize * Fonts.ChromeTracking;

    /// <summary>
    /// Wraps the window's existing content under the strip. Called last in a window's constructor,
    /// once <see cref="Window.Title"/> and <see cref="Window.CanResize"/> are set and
    /// <see cref="Window.Content"/> is built.
    /// </summary>
    /// <param name="showMinimize">
    /// Off for a window that is <c>ShowInTaskbar="False"</c>: minimised, it has no way back (#286).
    /// </param>
    public static void Apply(Window window, bool showMinimize = true)
    {
        var content = window.Content as Control;
        window.Content = null;

        Button? maximizeButton = null;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };

        if (showMinimize)
        {
            buttons.Children.Add(CaptionButton(Glyphs.Minimize, "Minimize",
                () => window.WindowState = WindowState.Minimized, filled: true));
        }

        if (window.CanResize)
        {
            maximizeButton = CaptionButton(Glyphs.Maximize, "Maximize", () => ToggleMaximize(window));
            buttons.Children.Add(maximizeButton);
        }

        buttons.Children.Add(CaptionButton(Glyphs.Close, "Close", window.Close, isClose: true));

        if (maximizeButton is { } toggle)
        {
            void ApplyMaximizeGlyph()
            {
                var maximized = window.WindowState == WindowState.Maximized;
                Glyphs.Mark(toggle, maximized ? Glyphs.Restore : Glyphs.Maximize,
                    maximized ? "Restore" : "Maximize", size: 12);
            }

            ApplyMaximizeGlyph();
            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty)
                {
                    ApplyMaximizeGlyph();
                }
            };
        }

        var name = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TitleSize,
            FontWeight = FontWeight.Bold,
            LetterSpacing = TitleTracking,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        name.Bind(TextBlock.ForegroundProperty, name.GetResourceObservable(TitleText.ColourKey(TitleRank.Window)));

        var version = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Secondary,
        };

        void ShowTitle() => (name.Text, version.Text) = Split(window.Title);

        ShowTitle();
        version.Bind(TextBlock.ForegroundProperty, version.GetResourceObservable(ThemeManager.AKey));

        var diamond = new Path
        {
            Data = Geometry.Parse("M 8,0 L 16,8 L 8,16 L 0,8 Z"),
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        diamond.Bind(Shape.FillProperty, diamond.GetResourceObservable(ThemeManager.AKey));


        var drag = new Border
        {
            Background = Brushes.Transparent,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Margin = new Thickness(16, 0, 0, 0),
                Children =
                {
                    new BloomStack { Tier = BloomTier.High, VerticalAlignment = VerticalAlignment.Center, Child = diamond },
                    new BloomStack { Tier = BloomTier.High, VerticalAlignment = VerticalAlignment.Center, Child = name },
                    version,
                },
            },
        };

        drag.PointerPressed += (_, e) => OnDragPressed(window, maximizeButton is not null, e);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(drag, 0);
        Grid.SetColumn(buttons, 1);
        row.Children.Add(drag);
        row.Children.Add(buttons);

        // Over the text as well as the ground, sampled pixel for pixel so each line stays 1px, as on the panel.
        var scanlines = new Border { Opacity = ThemeManager.ScanlinesOpacity, IsHitTestVisible = false };
        RenderOptions.SetBitmapInterpolationMode(scanlines, BitmapInterpolationMode.None);

        void ShowScanlines() => scanlines.Background =
            window.TryFindResource(ThemeManager.ScanlinesKey, out var brush) && brush is not null
                ? ThemeManager.Scanlines(window.RenderScaling)
                : null;

        scanlines.GetResourceObservable(ThemeManager.ScanlinesKey)
            .Subscribe(new Avalonia.Reactive.AnonymousObserver<object?>(_ => ShowScanlines()));
        window.ScalingChanged += (_, _) => ShowScanlines();

        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        rule.Bind(Border.BackgroundProperty, rule.GetResourceObservable(ThemeManager.Line2Key));

        var strip = new Grid
        {
            Name = "CaptionStrip",
            Height = StripHeight,
            Children = { row, scanlines, rule },
        };
        strip.Bind(Avalonia.Controls.Panel.BackgroundProperty, strip.GetResourceObservable(ThemeManager.BarKey));

        var wrapper = new DockPanel();
        DockPanel.SetDock(strip, Dock.Top);
        wrapper.Children.Add(strip);

        if (content is not null)
        {
            wrapper.Children.Add(content);
        }

        window.Content = wrapper;

        // Avalonia 12 draws its own title and caption buttons into an extended client area; an empty
        // decorations template keeps it from drawing a second titlebar under this one. Set before the
        // hint, so its own template is never built.
        window.WindowDecorationsTheme = NoDrawnDecorations;
        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaTitleBarHeightHint = StripHeight;

        // A maximised window's invisible resize border sits off the visible screen area, which would
        // otherwise clip the close button (#286).
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.TitleProperty)
            {
                ShowTitle();
            }
            else if (e.Property == Window.WindowStateProperty)
            {
                wrapper.Margin = window.WindowState == WindowState.Maximized ? window.OffScreenMargin : default;
            }
        };
    }

    private static readonly ControlTheme NoDrawnDecorations = new(typeof(WindowDrawnDecorations))
    {
        Setters = { new Setter(WindowDrawnDecorations.TemplateProperty, new EmptyDecorations()) },
    };

    /// <summary>
    /// Builds decorations with nothing in them. Not null: Avalonia skips a change from its own template
    /// to null and keeps drawing the old one.
    /// </summary>
    private sealed class EmptyDecorations : IWindowDrawnDecorationsTemplate
    {
        public TemplateResult<WindowDrawnDecorationsContent> Build() => new(new WindowDrawnDecorationsContent(), new NameScope());

        object? ITemplate.Build() => Build();
    }

    /// <summary>Splits <c>"Directive 47 — 0.1.0"</c> into the upper-cased name and the version after the dash.</summary>
    internal static (string Name, string Version) Split(string? title)
    {
        var text = title ?? "";
        var dash = text.IndexOf(" — ", StringComparison.Ordinal);

        return dash < 0
            ? (text.ToUpperInvariant(), "")
            : (text[..dash].ToUpperInvariant(), text[(dash + 3)..]);
    }

    private static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private static void OnDragPressed(Window window, bool canMaximize, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (canMaximize)
            {
                ToggleMaximize(window);
            }

            return;
        }

        window.BeginMoveDrag(e);
    }

    private static Button CaptionButton(
        string glyph, string says, Action press, bool isClose = false, bool filled = false)
    {
        var button = new Button
        {
            Theme = Application.Current?.FindResource("D47.CaptionButton") as ControlTheme,
            Width = ButtonWidth,
            Height = StripHeight,
            Classes = { "caption-button" },
        };

        if (isClose)
        {
            button.Classes.Add("close");
        }

        Glyphs.Mark(button, glyph, says, size: 12, filled: filled);

        button.Click += (_, _) => press();

        return button;
    }
}
