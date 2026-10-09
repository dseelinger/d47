using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Timing;

namespace D47.App.Controls;

/// <summary>
/// The one button that copies to the clipboard: a <c>D47.GlyphButton</c> whose face is two
/// overlapping squares, through the seam every draw site shares (#157).
/// </summary>
public static class CopyGlyph
{
    public const string Name = "Copy";
    public const string Copied = "Copied";
    public const string Failed = "Copy failed";

    /// <summary>Each square's side, and the stroke drawn inside it.</summary>
    public const double Square = 10;
    public const double Stroke = 2;

    /// <summary>The glyph's side. The back square sits at its top left and the front square at its bottom right.</summary>
    public const double Size = 14;

    private static readonly TimeSpan Shown = TimeSpan.FromSeconds(2);

    /// <summary>What a copy glyph built by <see cref="For"/> puts on the clipboard.</summary>
    public static readonly AttachedProperty<string?> CopiesProperty =
        AvaloniaProperty.RegisterAttached<Button, string?>("Copies", typeof(CopyGlyph));

    public static string? GetCopies(Button button) => button.GetValue(CopiesProperty);

    /// <summary>A copy glyph that copies <paramref name="value"/> through <paramref name="copy"/>.</summary>
    public static Button For(string value, Func<string, Task<bool>> copy)
    {
        var button = Dress(new Button { VerticalAlignment = VerticalAlignment.Center });
        button.SetValue(CopiesProperty, value);

        button.Click += async (_, _) =>
        {
            bool worked;

            try
            {
                worked = await copy(value);
            }
            catch (Exception)
            {
                worked = false;
            }

            Show(button, worked);
        };

        return button;
    }

    /// <summary>Makes <paramref name="button"/> the copy glyph, named <see cref="Name"/>.</summary>
    public static Button Dress(Button button)
    {
        button.Theme = Application.Current?.FindResource("D47.GlyphButton") as ControlTheme;
        button.Width = Theming.TypeScale.MinimumTarget;
        button.Height = Theming.TypeScale.MinimumTarget;
        button.Content = Face(button);

        AutomationProperties.SetName(button, Name);

        return button;
    }

    /// <summary>Names a copy's result on <paramref name="button"/>, and goes back to <see cref="Name"/> after two seconds.</summary>
    public static void Show(Button button, bool worked)
    {
        AutomationProperties.SetName(button, worked ? Copied : Failed);

        OneShot.Dispatcher(Shown, () => AutomationProperties.SetName(button, Name));
    }

    /// <summary>
    /// Two squares outlined in the button's foreground, the front one filled with its background so
    /// it covers the back one's corner on every face state.
    /// </summary>
    private static Canvas Face(Button button)
    {
        var back = Outline(button);
        var front = Outline(button);

        front.Bind(Border.BackgroundProperty, button.GetObservable(TemplatedControl.BackgroundProperty));
        Canvas.SetLeft(front, Size - Square);
        Canvas.SetTop(front, Size - Square);

        return new Canvas
        {
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { back, front },
        };
    }

    private static Border Outline(Button button)
    {
        var square = new Border { Width = Square, Height = Square, BorderThickness = new Thickness(Stroke) };

        square.Bind(Border.BorderBrushProperty, button.GetObservable(TemplatedControl.ForegroundProperty));

        return square;
    }
}
