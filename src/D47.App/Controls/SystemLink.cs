using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// A system name drawn as a link: pressing it opens Search › System on that system. The action comes
/// from the surface the name sits on (<see cref="OpenProperty"/>); a name on a surface with none is
/// drawn as plain text.
/// </summary>
public static class SystemLink
{
    public const string AutomationName = "Open system";

    /// <summary>What a press on a linked name does with the system name; set by the panel that hosts the page.</summary>
    public static readonly AttachedProperty<Action<string>?> OpenProperty =
        AvaloniaProperty.RegisterAttached<Control, Action<string>?>("Open", typeof(SystemLink), inherits: true);

    public static Action<string>? GetOpen(Control control) => control.GetValue(OpenProperty);

    public static void SetOpen(Control control, Action<string>? open) => control.SetValue(OpenProperty, open);

    /// <summary>Set on the pressable row a linked name sits in while the pointer or focus is on the name.</summary>
    public const string RowHeld = ":link";

    private const double FaceTarget = TypeScale.MinimumTarget;
    private const double Edge = 8;
    private const double UnderlineOffset = 4;
    private const double RestOpacity = 0.55;

    /// <summary>The name of <paramref name="system"/> as a link; <paramref name="name"/> keeps its ink and font.</summary>
    public static Button Of(TextBlock name, string system, bool keepFont = false, bool flush = true) =>
        Of(name, () => system, keepFont, flush);

    /// <summary>A link on a name whose system changes; a press does nothing while <paramref name="system"/> returns nothing.</summary>
    public static Button Of(TextBlock name, Func<string?> system, bool keepFont = false, bool flush = true)
    {
        if (!keepFont)
        {
            name.FontFamily = new FontFamily(Fonts.ChromeFamily);
            name.FontWeight = FontWeight.Medium;
            name.LetterSpacing = 0;
        }

        name.VerticalAlignment = VerticalAlignment.Center;
        name.TextWrapping = TextWrapping.Wrap;
        name.TextTrimming = TextTrimming.None;

        var face = new Border { Padding = new Thickness(0), Child = name };

        var button = new Button
        {
            Content = face,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = 0,
            FontFamily = name.FontFamily,
            FontSize = name.FontSize,
            FontWeight = name.FontWeight,
            LetterSpacing = name.LetterSpacing,
            FocusAdorner = null,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        Avalonia.Automation.AutomationProperties.SetName(button, AutomationName);

        var line = new TextDecoration
        {
            Location = TextDecorationLocation.Baseline,
            StrokeThickness = 1,
            StrokeThicknessUnit = TextDecorationUnit.Pixel,
            StrokeOffset = UnderlineOffset,
            StrokeOffsetUnit = TextDecorationUnit.Pixel,
        };

        IBrush? ink = name.Foreground;
        IDisposable? pressedInk = null;
        var changing = false;

        void Paint()
        {
            var linked = GetOpen(button) is not null;
            var solid = linked && button.Classes.Contains(":pressed");
            var lit = linked && (solid || button.Classes.Contains(":pointerover") || button.Classes.Contains(":focus-visible"));

            button.IsHitTestVisible = linked;
            button.Focusable = linked;
            button.Margin = linked && flush ? new Thickness(-Edge, 0, 0, 0) : new Thickness(0);
            face.Padding = linked ? new Thickness(Edge, 0) : new Thickness(0);
            face.MinHeight = linked ? FaceTarget : 0;

            Fill(face, linked ? (solid ? ThemeManager.AKey : lit ? ThemeManager.Tile2Key : null) : null);

            changing = true;
            pressedInk?.Dispose();
            pressedInk = null;

            if (solid && Application.Current?.FindResource(ThemeManager.KnockKey) is IBrush knock)
            {
                pressedInk = name.SetValue(TextBlock.ForegroundProperty, knock, BindingPriority.Animation);
            }

            changing = false;

            var stroke = solid ? name.Foreground : lit ? ink : Faded(ink);

            line.Stroke = stroke;
            name.TextDecorations = linked ? [line] : null;

            HoldRow(button, linked && lit);
        }

        name.GetObservable(TextBlock.ForegroundProperty).Subscribe(new Observer<IBrush?>(brush =>
        {
            if (!changing)
            {
                ink = brush;
                Paint();
            }
        }));

        button.GetObservable(OpenProperty).Subscribe(new Observer<Action<string>?>(_ => Paint()));
        button.Classes.CollectionChanged += (_, _) => Paint();
        button.DetachedFromVisualTree += (_, _) => HoldRow(button, false);

        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (system() is { Length: > 0 } target)
            {
                GetOpen(button)?.Invoke(target);
            }
        };

        Paint();
        return button;
    }

    /// <summary>
    /// Puts a link on the first whole occurrence of <paramref name="system"/> in the text of <paramref name="line"/>,
    /// leaving the rest of the sentence in the block; false, with the block unchanged, where it does not name it.
    /// <paramref name="name"/> makes the block the name is drawn in, in the sentence's ink.
    /// </summary>
    public static bool InSentence(TextBlock line, string system, Func<string, TextBlock> name)
    {
        var text = line.Text;

        if (string.IsNullOrEmpty(text) || Naming(text, system) is not { } at)
        {
            return false;
        }

        var end = at + system.Length;
        var inlines = new InlineCollection();
        var shown = name(text[at..end]);

        if (shown.Foreground is null)
        {
            shown.Bind(TextBlock.ForegroundProperty, line.GetObservable(TextBlock.ForegroundProperty));
        }

        if (at > 0)
        {
            inlines.Add(new Run(text[..at]));
        }

        inlines.Add(new InlineUIContainer { BaselineAlignment = BaselineAlignment.Center, Child = Of(shown, system, keepFont: true, flush: false) });

        if (end < text.Length)
        {
            inlines.Add(new Run(text[end..]));
        }

        line.Text = null;
        line.Inlines = inlines;

        return true;
    }

    /// <summary>Where <paramref name="text"/> names <paramref name="system"/> as a whole name, or null.</summary>
    public static int? Naming(string text, string? system)
    {
        if (string.IsNullOrWhiteSpace(system))
        {
            return null;
        }

        for (var at = text.IndexOf(system, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = text.IndexOf(system, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + system.Length;

            if ((at == 0 || !char.IsLetterOrDigit(text[at - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end])))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>
    /// Draws a system name that is not a link in the link's look: underlined at 55% of its ink, in Knock
    /// with a full underline while the pressable row it sits in is pressed.
    /// </summary>
    public static TextBlock Look(TextBlock name)
    {
        var line = new TextDecoration
        {
            Location = TextDecorationLocation.Baseline,
            StrokeThickness = 1,
            StrokeThicknessUnit = TextDecorationUnit.Pixel,
            StrokeOffset = UnderlineOffset,
            StrokeOffsetUnit = TextDecorationUnit.Pixel,
        };

        IBrush? ink = name.Foreground;
        IDisposable? pressedInk = null;
        var changing = false;
        Button? row = null;

        void Paint()
        {
            var solid = row is not null && row.Classes.Contains(":pressed");

            changing = true;
            pressedInk?.Dispose();
            pressedInk = null;

            if (solid && Application.Current?.FindResource(ThemeManager.KnockKey) is IBrush knock)
            {
                pressedInk = name.SetValue(TextBlock.ForegroundProperty, knock, BindingPriority.Animation);
            }

            changing = false;

            line.Stroke = solid ? name.Foreground : Faded(ink);
        }

        name.GetObservable(TextBlock.ForegroundProperty).Subscribe(new Observer<IBrush?>(brush =>
        {
            if (!changing)
            {
                ink = brush;
                Paint();
            }
        }));

        name.AttachedToVisualTree += (_, _) =>
        {
            row = name.FindAncestorOfType<Button>();

            if (row is not null)
            {
                row.Classes.CollectionChanged += (_, _) => Paint();
            }

            Paint();
        };

        name.TextDecorations = [line];
        Paint();

        return name;
    }

    private static IBrush? Faded(IBrush? brush) =>
        brush is ISolidColorBrush solid
            ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * RestOpacity)
            : brush;

    private static void Fill(Border face, string? key)
    {
        if (key is null)
        {
            face.Background = Brushes.Transparent;
            return;
        }

        ThemeInk.Bind(face, Border.BackgroundProperty, key);
    }

    private static void HoldRow(Button link, bool held)
    {
        if (link.FindAncestorOfType<Button>() is { } row)
        {
            ((IPseudoClasses)row.Classes).Set(RowHeld, held);
        }
    }

    internal sealed class Observer<T>(Action<T> next) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value) => next(value);
    }
}
