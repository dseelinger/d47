using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Windowing;

/// <summary>
/// Draws d47's own caption row in place of the native titlebar: the icon and title on the left,
/// minimise, maximise/restore and close on the right (#286). Windows keeps resizing, Aero Snap,
/// Win+arrow and the Alt+Space system menu, because <see cref="Window.WindowDecorations"/> stays
/// as it was — this only extends the client area into the space that chrome used to paint.
/// </summary>
public static class CaptionStrip
{
    public const double StripHeight = 44;
    private const double ButtonWidth = 46;

    private static Bitmap? _icon;

    /// <summary>
    /// Wraps the window's existing content under the strip. Called last in a window's constructor,
    /// once <see cref="Window.Title"/> and <see cref="Window.CanResize"/> are set and
    /// <see cref="Window.Content"/> is built.
    /// </summary>
    /// <param name="showMinimize">
    /// Off for <see cref="D47.App.Controls.PickerWindow"/>: it is <c>ShowInTaskbar="False"</c>, and a
    /// minimised window with no taskbar entry has no way back (#286).
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

        buttons.Children.Add(CaptionButton(Glyphs.Cross, "Close", window.Close, isClose: true));

        if (maximizeButton is { } toggle)
        {
            void ApplyMaximizeGlyph()
            {
                var maximized = window.WindowState == WindowState.Maximized;
                Glyphs.Mark(toggle, maximized ? Glyphs.Restore : Glyphs.Maximize, ThemeManager.TextKey,
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

        var title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily(Fonts.LabelFamily),
            FontSize = TypeScale.Body,
            LetterSpacing = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.TextProperty, window.GetObservable(Window.TitleProperty));
        title.Bind(TextBlock.ForegroundProperty, title.GetResourceObservable(ThemeManager.TextKey));

        var drag = new Border
        {
            Background = Brushes.Transparent,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(14, 0, 0, 0),
                Children =
                {
                    new Image { Width = 18, Height = 18, Source = Icon(), VerticalAlignment = VerticalAlignment.Center },
                    title,
                },
            },
        };

        drag.PointerPressed += (_, e) => OnDragPressed(window, maximizeButton is not null, e);

        var strip = new Grid
        {
            Name = "CaptionStrip",
            Height = StripHeight,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetColumn(drag, 0);
        Grid.SetColumn(buttons, 1);
        strip.Children.Add(drag);
        strip.Children.Add(buttons);
        strip.Bind(Avalonia.Controls.Panel.BackgroundProperty, strip.GetResourceObservable(ThemeManager.BackgroundKey));

        var wrapper = new DockPanel();
        DockPanel.SetDock(strip, Dock.Top);
        wrapper.Children.Add(strip);

        if (content is not null)
        {
            wrapper.Children.Add(content);
        }

        window.Content = wrapper;

        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaTitleBarHeightHint = StripHeight;

        // A maximised window's invisible resize border sits off the visible screen area, which would
        // otherwise clip the close button (#286).
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
            {
                wrapper.Margin = window.WindowState == WindowState.Maximized ? window.OffScreenMargin : default;
            }
        };
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
            Width = ButtonWidth,
            Height = StripHeight,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Classes = { "caption-button" },
        };

        Glyphs.Mark(button, glyph, ThemeManager.TextKey, says, size: 12, filled: filled);

        button.Click += (_, _) => press();
        button.PointerEntered += (_, _) => button.Background = HoverFill(isClose);
        button.PointerExited += (_, _) => button.Background = Brushes.Transparent;

        return button;
    }

    /// <summary>Close hovers to Danger; minimise and maximise hover to the same faint fill every other
    /// control in the kit does.</summary>
    private static IBrush HoverFill(bool isClose)
    {
        var key = isClose ? ThemeManager.DangerKey : ThemeManager.FillHighKey;

        return Application.Current!.TryGetResource(key, null, out var value) && value is IBrush brush
            ? brush
            : Brushes.Transparent;
    }

    private static Bitmap Icon() =>
        _icon ??= new Bitmap(AssetLoader.Open(new Uri("avares://d47/Assets/directive-47.ico")));
}
