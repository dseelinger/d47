using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// The Journal page's right pane: the selected event as a headline, its time, labelled rows, the
/// paragraph for its kind and every field it carries (#817).
/// </summary>
internal sealed class JournalReadingPane : Border
{
    /// <summary>Below this pane width, a label sits above its value.</summary>
    public const double NarrowWidth = 480;

    /// <summary>A row shows this many values before the rest fold behind a count.</summary>
    public const int ValuesShown = 6;

    private const double LabelColumn = 148;
    private const double FieldNameColumn = 212;

    private static readonly FontFamily Chrome = new(Fonts.ChromeFamily);
    private static readonly FontFamily Prose = new(Fonts.ProseFamily);
    private static readonly FontFamily Mono = new(Fonts.MonoFamily);

    private readonly StackPanel _content = new() { Spacing = 18 };
    private readonly List<Action<bool>> _layouts = [];
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };

    private JournalEntry? _shown;
    private bool _drawn;
    private bool _narrow;
    private Run? _age;

    /// <summary>The entry the pane is drawing, or null for the empty state.</summary>
    public JournalEntry? Shown => _shown;

    /// <summary>Whether labels sit above their values.</summary>
    public bool Narrow => _narrow;

    private readonly Func<ReadingLink, Action?> _open;
    private readonly Func<string?> _currentSystem;
    private readonly Action<SelectableTextBlock>? _watch;

    /// <param name="open">The action a link performs, or null where the link draws as plain text.</param>
    /// <param name="currentSystem">The system the Commander is in now.</param>
    /// <param name="watch">Hooks a selectable block into the panel's copy control.</param>
    public JournalReadingPane(
        Func<ReadingLink, Action?> open,
        Func<string?> currentSystem,
        Action<SelectableTextBlock>? watch = null)
    {
        _open = open;
        _currentSystem = currentSystem;
        _watch = watch;

        Padding = new Thickness(24, 20, 24, 32);
        Child = _content;

        _clock.Tick += (_, _) => RefreshAge();
        SizeChanged += (_, changed) => Relayout(changed.NewSize.Width < NarrowWidth);
        AttachedToVisualTree += (_, _) => _clock.Start();
        DetachedFromVisualTree += (_, _) => _clock.Stop();

        Show(null);
    }

    /// <summary>Draws an entry; the same entry, compared by reference, is not drawn again.</summary>
    public void Show(JournalEntry? entry)
    {
        if (_drawn && ReferenceEquals(entry, _shown))
        {
            return;
        }

        _drawn = true;
        _shown = entry;
        _layouts.Clear();
        _content.Children.Clear();
        _age = null;

        if (entry is null)
        {
            DrawEmpty();
            return;
        }

        var reading = EventReadings.For(entry);

        _content.Children.Add(Heading(reading));

        if (reading.Rows.Count > 0)
        {
            var rows = new StackPanel { Spacing = 2 };

            foreach (var row in reading.Rows)
            {
                rows.Children.Add(Row(row, entry.Kind));
            }

            _content.Children.Add(rows);
        }

        if (JournalExplainers.For(entry.Kind) is { } paragraph)
        {
            _content.Children.Add(Meaning(paragraph));
        }

        _content.Children.Add(EveryField(reading.Plumbing));

        foreach (var layout in _layouts)
        {
            layout(_narrow);
        }
    }

    /// <summary>Redraws the age against the clock.</summary>
    public void RefreshAge()
    {
        if (_age is not null && _shown is { } entry)
        {
            _age.Text = EventReadings.Age(entry.Timestamp, DateTimeOffset.Now);
        }
    }

    private void Relayout(bool narrow)
    {
        if (narrow == _narrow)
        {
            return;
        }

        _narrow = narrow;

        foreach (var layout in _layouts)
        {
            layout(narrow);
        }
    }

    // ---- Empty, headline and time --------------------------------------------------------------------

    private void DrawEmpty()
    {
        var title = Text("No event selected", Prose, TypeScale.Headline, FontWeight.Normal, ThemeManager.GreyKey);
        title.LineHeight = 26;

        var hint = Text(
            "Select an event in the list. D47 shows it here in words, with every field the journal wrote one fold below.",
            Prose,
            TypeScale.Tip,
            FontWeight.Normal,
            ThemeManager.GreyKey);
        hint.LineHeight = 21;
        hint.MaxWidth = 330;
        hint.HorizontalAlignment = HorizontalAlignment.Left;

        _content.Children.Add(new StackPanel { Spacing = 6, Children = { title, hint } });
    }

    private StackPanel Heading(EventReading reading)
    {
        var headline = Text(reading.Headline, Prose, TypeScale.Headline, FontWeight.Normal, ThemeManager.WhiteKey);
        headline.LineHeight = 26;
        headline.Name = "ReadingHeadline";

        var time = new Run(reading.Timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture))
        {
            FontFamily = Mono,
            FontSize = TypeScale.Meta,
        };
        LoadoutPages.Themed(time, TextElement.ForegroundProperty, ThemeManager.AKey);

        var dot = new Run(" · ") { FontFamily = Prose, FontSize = TypeScale.Small };
        _age = new Run(EventReadings.Age(reading.Timestamp, DateTimeOffset.Now)) { FontFamily = Prose, FontSize = TypeScale.Small };

        var line = new TextBlock { Name = "ReadingTime", Inlines = [time, dot, _age] };
        LoadoutPages.Themed(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return new StackPanel { Spacing = 6, Children = { headline, line } };
    }

    // ---- Rows ----------------------------------------------------------------------------------------

    private Border Row(ReadingRow row, string kind)
    {
        var values = Values(row.Values);
        Control? label = row.Label.Length == 0 ? null : Label(row, kind);

        var grid = new Grid();

        if (label is not null)
        {
            grid.Children.Add(label);
        }

        grid.Children.Add(values);

        _layouts.Add(narrow =>
        {
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            if (label is null)
            {
                return;
            }

            if (narrow)
            {
                grid.RowDefinitions = new RowDefinitions("Auto,4,Auto");
                Grid.SetColumn(values, 0);
                Grid.SetRow(values, 2);
            }
            else
            {
                grid.ColumnDefinitions = new ColumnDefinitions($"{LabelColumn},16,*");
                Grid.SetColumn(values, 2);
                Grid.SetRow(values, 0);
            }
        });

        var strip = new Border { Padding = new Thickness(14, 10), Child = grid };
        LoadoutPages.Themed(strip, BackgroundProperty, ThemeManager.SlabKey);

        return strip;
    }

    private Control Label(ReadingRow row, string kind)
    {
        var label = Text(row.Label.ToUpperInvariant(), Chrome, TypeScale.Caption, FontWeight.Medium, ThemeManager.GreyKey);
        label.LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking;
        label.LineHeight = 18;
        label.Margin = new Thickness(0, 1, 0, 0);

        if (row.Field is not { } name || JournalFields.Find(name, kind) is not { } field)
        {
            return label;
        }

        label.Cursor = new Cursor(StandardCursorType.Help);
        label.Focusable = true;

        var named = new Run(name) { FontFamily = Mono, FontSize = TypeScale.Meta };
        LoadoutPages.Themed(named, TextElement.ForegroundProperty, ThemeManager.AKey);

        var tip = Text(string.Empty, Prose, TypeScale.Small, FontWeight.Normal, ThemeManager.WhiteKey);
        tip.LineHeight = TypeScale.Small * 1.45;
        tip.Inlines = [named, new Run(" — "), new Run(field.Meaning)];

        var box = Box(tip, new Thickness(10, 8));
        box.Width = 300;

        return Hover(
            label,
            box,
            PlacementMode.BottomEdgeAlignedLeft,
            horizontal: -14,
            vertical: 10,
            shown => LoadoutPages.Themed(label, TextBlock.ForegroundProperty, shown ? ThemeManager.WhiteKey : ThemeManager.GreyKey),
            narrowWidth: box);
    }

    private WrapPanel Values(IReadOnlyList<ReadingValue> values)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        var folded = true;

        void Draw()
        {
            panel.Children.Clear();

            var shown = folded && values.Count > ValuesShown ? ValuesShown : values.Count;

            for (var i = 0; i < shown; i++)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                item.Children.Add(Value(values[i]));

                if (i < shown - 1)
                {
                    var separator = Text("·", Chrome, TypeScale.Secondary, FontWeight.Medium, ThemeManager.GreyKey);
                    separator.Margin = new Thickness(8, 0);
                    separator.LineHeight = 20;
                    separator.VerticalAlignment = VerticalAlignment.Center;
                    item.Children.Add(separator);
                }

                panel.Children.Add(item);
            }

            if (values.Count > ValuesShown)
            {
                panel.Children.Add(More(folded ? values.Count - ValuesShown : 0, () =>
                {
                    folded = !folded;
                    Draw();
                }));
            }
        }

        Draw();
        return panel;
    }

    /// <summary>The "+N more" tile, or "fewer" once the row is open.</summary>
    private static Button More(int hidden, Action toggle)
    {
        var words = new TextBlock { VerticalAlignment = VerticalAlignment.Center, LetterSpacing = 0 };

        if (hidden > 0)
        {
            var count = new Run($"+{hidden.ToString(CultureInfo.InvariantCulture)} ") { FontFamily = Mono, FontSize = TypeScale.Caption };
            var more = new Run("MORE")
            {
                FontFamily = Chrome,
                FontSize = TypeScale.Caption,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
            };
            words.Inlines = [count, more];
        }
        else
        {
            words.Inlines =
            [
                new Run("FEWER")
                {
                    FontFamily = Chrome,
                    FontSize = TypeScale.Caption,
                    FontWeight = FontWeight.SemiBold,
                    LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
                },
            ];
        }

        var face = new Border { Height = 26, Padding = new Thickness(8, 0), Child = words };
        var button = Flat(face, words, toggle, ThemeManager.AKey);
        button.Name = "ReadingMore";
        button.Margin = new Thickness(12, -9, 0, -9);

        return button;
    }

    private Control Value(ReadingValue value)
    {
        if (value.Typed)
        {
            var typed = Text(value.Text, Prose, TypeScale.Secondary, FontWeight.Normal, ThemeManager.GreyKey);
            typed.LineHeight = 22;
            typed.TextWrapping = TextWrapping.Wrap;
            typed.Classes.Add("typed");
            return typed;
        }

        if (value.Link is { } link && _open(link) is { } act)
        {
            return Link(value, link, act);
        }

        var ink = Ink(value.Tone, value.Text);
        var text = new TextBlock
        {
            FontFamily = Chrome,
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Medium,
            LineHeight = 20,
            LetterSpacing = 0,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        LoadoutPages.Themed(text, TextBlock.ForegroundProperty, ink);

        text.Inlines = [.. value.Runs.Select(run => run.Number
            ? new Run(run.Text) { FontFamily = Mono, FontSize = TypeScale.Small, FontWeight = FontWeight.Normal }
            : new Run(run.Text))];

        if (value.Symbol is not { } symbol)
        {
            return text;
        }

        text.Focusable = true;
        Underline(text, ThemeManager.GreyKey);

        var caption = Text("JOURNAL", Chrome, TypeScale.MetaSmall, FontWeight.Medium, ThemeManager.GreyKey);
        caption.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;
        caption.VerticalAlignment = VerticalAlignment.Center;

        var token = Text(symbol, Mono, TypeScale.Meta, FontWeight.Normal, ThemeManager.WhiteKey);
        token.VerticalAlignment = VerticalAlignment.Center;

        var box = Box(
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { caption, token } },
            new Thickness(10, 0));
        box.Height = 32;

        return Hover(
            text,
            box,
            PlacementMode.BottomEdgeAlignedLeft,
            horizontal: 0,
            vertical: 8,
            shown => Underline(text, shown ? ThemeManager.AKey : ThemeManager.GreyKey));
    }

    /// <summary>The palette key a value's tone draws in.</summary>
    internal string Ink(ReadingTone tone, string text) => tone switch
    {
        ReadingTone.Name => ThemeManager.WhiteKey,
        ReadingTone.Warning => ThemeManager.RedKey,
        ReadingTone.System => string.Equals(_currentSystem(), text, StringComparison.OrdinalIgnoreCase)
            ? ThemeManager.CyanKey
            : ThemeManager.WhiteKey,
        _ => ThemeManager.AKey,
    };

    private static void Underline(TextBlock text, string key)
    {
        var line = new TextDecoration
        {
            Location = TextDecorationLocation.Baseline,
            StrokeThickness = 1,
            StrokeThicknessUnit = TextDecorationUnit.Pixel,
            StrokeDashArray = new AvaloniaList<double> { 1, 2 },
            StrokeOffset = 3,
            StrokeOffsetUnit = TextDecorationUnit.Pixel,
        };
        LoadoutPages.Themed(line, TextDecoration.StrokeProperty, key);

        text.TextDecorations = [line];

        foreach (var run in text.Inlines ?? [])
        {
            run.TextDecorations = [line];
        }
    }

    private static Control Link(ReadingValue value, ReadingLink link, Action act)
    {
        var name = Text(value.Text, Chrome, TypeScale.Secondary, FontWeight.Medium, ThemeManager.WhiteKey);
        name.VerticalAlignment = VerticalAlignment.Center;

        var glyph = Text("›", Chrome, TypeScale.Secondary, FontWeight.Medium, ThemeManager.AKey);
        glyph.VerticalAlignment = VerticalAlignment.Center;

        var face = new Border
        {
            Height = 30,
            Padding = new Thickness(10, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { name, glyph } },
        };

        var button = Flat(face, name, act, ThemeManager.WhiteKey, glyph);
        button.Name = "ReadingLink";

        var tip = Text(
            link.Kind == ReadingLinkKind.Engineer ? "OPEN ON THE ENGINEERS TAB" : "OPEN ON THE SHIPS TAB",
            Chrome,
            TypeScale.Meta,
            FontWeight.Medium,
            ThemeManager.WhiteKey);
        tip.LetterSpacing = 0.48;
        tip.VerticalAlignment = VerticalAlignment.Center;

        var box = Box(tip, new Thickness(10, 0));
        box.Height = 32;

        var popup = new Popup
        {
            PlacementTarget = face,
            Placement = PlacementMode.Right,
            HorizontalOffset = 6,
            IsLightDismissEnabled = false,
            Child = box,
        };

        button.Classes.CollectionChanged += (_, _) =>
            popup.IsOpen = button.IsEffectivelyVisible
                && (button.Classes.Contains(":pointerover") || button.Classes.Contains(":focus-visible"));
        button.DetachedFromVisualTree += (_, _) => popup.IsOpen = false;

        return new Avalonia.Controls.Panel { Margin = new Thickness(0, -7), Children = { button, popup } };
    }

    /// <summary>
    /// A button drawn as a face inside a 44px hit area: Tile at rest, Tile2 on hover, solid A with Knock
    /// ink when pressed or focused from the keyboard.
    /// </summary>
    private static Button Flat(Border face, TextBlock ink, Action act, string inkKey, TextBlock? glyph = null)
    {
        var button = new Button
        {
            Content = face,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            MinHeight = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        void Paint()
        {
            var solid = button.Classes.Contains(":pressed") || button.Classes.Contains(":focus-visible");
            var over = button.Classes.Contains(":pointerover");

            LoadoutPages.Themed(face, BackgroundProperty, solid ? ThemeManager.AKey : over ? ThemeManager.Tile2Key : ThemeManager.TileKey);
            LoadoutPages.Themed(ink, TextBlock.ForegroundProperty, solid ? ThemeManager.KnockKey : inkKey);

            if (glyph is not null)
            {
                LoadoutPages.Themed(glyph, TextBlock.ForegroundProperty, solid ? ThemeManager.KnockKey : ThemeManager.AKey);
            }
        }

        Paint();
        button.Classes.CollectionChanged += (_, _) => Paint();
        button.Click += (_, _) => act();

        return button;
    }

    // ---- Folds ---------------------------------------------------------------------------------------

    private StackPanel Meaning(string paragraph)
    {
        var body = Text(paragraph, Prose, TypeScale.Secondary, FontWeight.Normal, ThemeManager.WhiteKey);
        body.LineHeight = TypeScale.Secondary * 1.55;
        body.TextWrapping = TextWrapping.Wrap;
        body.MaxWidth = 580;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        body.Margin = new Thickness(0, 12, 0, 0);
        body.Name = "ReadingParagraph";

        var preview = Text(paragraph, Prose, TypeScale.Small, FontWeight.Normal, ThemeManager.GreyKey);
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        preview.Margin = new Thickness(12, 0, 0, 0);
        preview.VerticalAlignment = VerticalAlignment.Center;

        return Fold("WHAT THIS MEANS", body, open: true, preview, aside: null, "ReadingMeaning");
    }

    private StackPanel EveryField(IReadOnlyList<PlumbingField> fields)
    {
        var list = new StackPanel { Name = "ReadingFields" };

        foreach (var field in fields)
        {
            list.Children.Add(Field(field));
        }

        var count = Text(fields.Count.ToString(CultureInfo.InvariantCulture), Mono, TypeScale.Meta, FontWeight.Normal, ThemeManager.GreyKey);
        count.VerticalAlignment = VerticalAlignment.Center;

        return Fold("EVERY FIELD", list, open: false, preview: null, aside: count, "ReadingEveryField");
    }

    private Border Field(PlumbingField field)
    {
        var name = Selectable(field.Name, ThemeManager.GreyKey);
        var value = Selectable(field.Value, ThemeManager.WhiteKey);
        var grid = new Grid { Children = { name, value } };

        _layouts.Add(narrow =>
        {
            grid.ColumnDefinitions = narrow ? [] : new ColumnDefinitions($"{FieldNameColumn},*");
            grid.RowDefinitions = narrow ? new RowDefinitions("Auto,Auto") : [];
            Grid.SetColumn(value, narrow ? 0 : 1);
            Grid.SetRow(value, narrow ? 1 : 0);
        });

        var line = new Border
        {
            Padding = new Thickness(2, 5),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        };
        LoadoutPages.Themed(line, BorderBrushProperty, ThemeManager.Line2Key);

        return line;
    }

    private SelectableTextBlock Selectable(string text, string key)
    {
        var block = new SelectableTextBlock
        {
            Text = text,
            FontFamily = Mono,
            FontSize = TypeScale.Meta,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);
        _watch?.Invoke(block);

        return block;
    }

    /// <summary>A disclosure head over its body: a 44px row with a rule under it, Tile on hover.</summary>
    private static StackPanel Fold(string title, Control body, bool open, TextBlock? preview, Control? aside, string name)
    {
        var glyph = Text(string.Empty, Chrome, TypeScale.MetaSmall, FontWeight.Normal, ThemeManager.AKey);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.Width = 22;

        var heading = Text(title, Chrome, TypeScale.Control, FontWeight.SemiBold, ThemeManager.WhiteKey);
        heading.LetterSpacing = TypeScale.Control * Fonts.ChromeTracking;
        heading.VerticalAlignment = VerticalAlignment.Center;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(heading, 1);
        row.Children.Add(glyph);
        row.Children.Add(heading);

        if (preview is not null)
        {
            Grid.SetColumn(preview, 2);
            row.Children.Add(preview);
        }

        if (aside is not null)
        {
            Grid.SetColumn(aside, 3);
            row.Children.Add(aside);
        }

        var face = new Border
        {
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };
        LoadoutPages.Themed(face, BorderBrushProperty, ThemeManager.AKey);

        var button = new Button
        {
            Name = name,
            Content = face,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            MinHeight = TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        void Draw()
        {
            glyph.Text = open ? "▼" : "▶";
            body.IsVisible = open;

            if (preview is not null)
            {
                preview.IsVisible = !open;
            }
        }

        void Paint()
        {
            if (button.Classes.Contains(":pointerover") || button.Classes.Contains(":focus-visible"))
            {
                LoadoutPages.Themed(face, BackgroundProperty, ThemeManager.TileKey);
            }
            else
            {
                face.Background = Brushes.Transparent;
            }
        }

        button.Click += (_, _) =>
        {
            open = !open;
            Draw();
        };
        button.Classes.CollectionChanged += (_, _) => Paint();

        Draw();
        Paint();

        return new StackPanel { Children = { button, body } };
    }

    // ---- Shared pieces -------------------------------------------------------------------------------

    private static TextBlock Text(string text, FontFamily family, double size, FontWeight weight, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = family,
            FontSize = size,
            FontWeight = weight,
            LetterSpacing = 0,
            TextWrapping = TextWrapping.Wrap,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    /// <summary>A hover box: Bg ground with a 1px A border.</summary>
    private static Border Box(Control child, Thickness padding)
    {
        var box = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = padding,
            IsHitTestVisible = false,
            Child = child,
        };
        LoadoutPages.Themed(box, BackgroundProperty, ThemeManager.BgKey);
        LoadoutPages.Themed(box, BorderBrushProperty, ThemeManager.AKey);

        return box;
    }

    /// <summary>Opens <paramref name="box"/> beside <paramref name="target"/> while it is hovered or focused.</summary>
    private Avalonia.Controls.Panel Hover(
        Control target,
        Control box,
        PlacementMode placement,
        double horizontal,
        double vertical,
        Action<bool> changed,
        Border? narrowWidth = null)
    {
        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = placement,
            HorizontalOffset = horizontal,
            VerticalOffset = vertical,
            IsLightDismissEnabled = false,
            Child = box,
        };

        var over = false;
        var focused = false;

        if (target is TextBlock { Background: null } text)
        {
            text.Background = Brushes.Transparent;
        }

        void Update()
        {
            var shown = over || focused;

            if (narrowWidth is not null)
            {
                narrowWidth.Width = _narrow ? 260 : 300;
            }

            if (popup.IsOpen != shown)
            {
                popup.IsOpen = shown;
                changed(shown);
            }
        }

        target.PointerEntered += (_, _) => { over = true; Update(); };
        target.PointerExited += (_, _) => { over = false; Update(); };
        target.GotFocus += (_, _) => { focused = true; Update(); };
        target.LostFocus += (_, _) => { focused = false; Update(); };
        target.DetachedFromVisualTree += (_, _) => { over = focused = false; Update(); };

        return new Avalonia.Controls.Panel { Children = { target, popup } };
    }
}
