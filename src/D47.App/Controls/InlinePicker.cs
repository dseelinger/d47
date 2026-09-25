using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>One line of an <see cref="InlinePicker"/>'s list.</summary>
public abstract record InlinePickerEntry;

/// <summary>A choice, its label drawn in <paramref name="InkKey"/> unless it is the current one.</summary>
public sealed record InlinePickerOption(string Id, string Label, string InkKey) : InlinePickerEntry;

/// <summary>A small uppercase heading over the options that follow it.</summary>
public sealed record InlinePickerHeading(string Text) : InlinePickerEntry;

/// <summary>A line of grey text on Slab where a run of options is empty.</summary>
public sealed record InlinePickerNote(string Text) : InlinePickerEntry;

/// <summary>Blank space between two runs of options.</summary>
public sealed record InlinePickerSpace(double Height) : InlinePickerEntry;

/// <summary>
/// A dropdown whose list opens inside the page, under the button, and pushes what follows it down. No
/// popup or flyout, which the headset overlay does not draw. Picking an option raises
/// <see cref="Picked"/> and closes the list.
/// </summary>
public sealed class InlinePicker : ContentControl
{
    /// <summary>Marks the button that shows the value and opens the list, for a test to find it.</summary>
    public const string ButtonName = "InlinePickerButton";

    /// <summary>Marks the open list, for a test to find it.</summary>
    public const string ListName = "InlinePickerList";

    /// <summary>Marks one option in the list.</summary>
    public const string OptionClass = "inline-picker-option";

    /// <summary>Raised with the option's id when the Commander picks one.</summary>
    public event EventHandler<string>? Picked;

    private readonly Button _button;
    private readonly TextBlock _value = new()
    {
        Name = "InlinePickerValue",
        FontFamily = new FontFamily(Fonts.ChromeFamily),
        FontSize = TypeScale.Tip,
        FontWeight = FontWeight.Medium,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly Border _list;
    private readonly StackPanel _options = new() { Spacing = 2 };
    private readonly List<IDisposable> _inks = [];
    private IDisposable? _openInk;

    public InlinePicker()
    {
        var caret = new TextBlock
        {
            Text = "▼",
            FontSize = TypeScale.Caption,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Ink(caret, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var layout = new DockPanel();
        DockPanel.SetDock(caret, Dock.Right);
        layout.Children.Add(caret);
        layout.Children.Add(_value);

        _button = new Button
        {
            Name = ButtonName,
            Content = layout,
            Height = 40,
            MinHeight = 40,
            Padding = new Thickness(12, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _button.Classes.Add(ListRow.Class);
        _button.Click += (_, _) => IsOpen = !IsOpen;

        _list = new Border
        {
            Name = ListName,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            Margin = new Thickness(0, 2, 0, 0),
            IsVisible = false,
            Child = _options,
        };
        Ink(_list, Border.BackgroundProperty, ThemeManager.BarKey);
        Ink(_list, Border.BorderBrushProperty, ThemeManager.AKey);

        Content = new StackPanel { Children = { _button, _list } };
    }

    /// <summary>Whether the list is showing.</summary>
    public bool IsOpen
    {
        get => _list.IsVisible;
        set
        {
            _list.IsVisible = value;
            _openInk?.Dispose();
            _openInk = null;

            if (value)
            {
                _openInk = Ink(_button, Button.BackgroundProperty, ThemeManager.Tile2Key);
            }
            else
            {
                _button.ClearValue(Button.BackgroundProperty);
            }
        }
    }

    /// <summary>The name the button is announced by.</summary>
    public string Label
    {
        set => AutomationProperties.SetName(_button, value);
    }

    /// <summary>
    /// Draws the value the button shows, in <paramref name="valueInk"/>, with <paramref name="suffix"/>
    /// after it in Grey, and the list with <paramref name="current"/> marked.
    /// </summary>
    public void Show(IReadOnlyList<InlinePickerEntry> entries, string current, string value, string valueInk, string? suffix)
    {
        _inks.ForEach(ink => ink.Dispose());
        _inks.Clear();

        var shown = new Run(value);
        _inks.Add(shown.Bind(TextElement.ForegroundProperty, Resource(valueInk)));
        _value.Inlines ??= [];
        _value.Inlines.Clear();
        _value.Inlines.Add(shown);

        if (suffix is not null)
        {
            var after = new Run(suffix);
            _inks.Add(after.Bind(TextElement.ForegroundProperty, Resource(ThemeManager.GreyKey)));
            _value.Inlines.Add(after);
        }

        _options.Children.Clear();

        foreach (var entry in entries)
        {
            _options.Children.Add(entry switch
            {
                InlinePickerOption option => Option(option, option.Id == current),
                InlinePickerHeading heading => Heading(heading.Text),
                InlinePickerNote note => Note(note.Text),
                InlinePickerSpace space => new Border { Height = space.Height },
                _ => throw new ArgumentOutOfRangeException(nameof(entries)),
            });
        }
    }

    private Button Option(InlinePickerOption option, bool isCurrent)
    {
        var text = new TextBlock
        {
            Text = option.Label,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Tip,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // The current option takes the selected row's Knock ink from the button.
        if (!isCurrent)
        {
            _inks.Add(Ink(text, TextBlock.ForegroundProperty, option.InkKey));
        }

        var button = new Button
        {
            Content = text,
            MinHeight = 36,
            Padding = new Thickness(12, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add(ListRow.Class);
        button.Classes.Add(OptionClass);
        AutomationProperties.SetName(button, option.Label);

        if (isCurrent)
        {
            button.Classes.Add(ListRow.SelectedClass);
        }

        button.Click += (_, _) =>
        {
            IsOpen = false;
            Picked?.Invoke(this, option.Id);
        };

        return button;
    }

    private TextBlock Heading(string text)
    {
        var heading = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
            Margin = new Thickness(12, 10, 12, 4),
        };
        _inks.Add(Ink(heading, TextBlock.ForegroundProperty, ThemeManager.GreyKey));

        return heading;
    }

    private Border Note(string text)
    {
        var words = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Tip,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _inks.Add(Ink(words, TextBlock.ForegroundProperty, ThemeManager.GreyKey));

        var note = new Border { MinHeight = 36, Padding = new Thickness(12, 6), Child = words };
        _inks.Add(Ink(note, Border.BackgroundProperty, ThemeManager.SlabKey));

        return note;
    }

    private static IObservable<object?> Resource(string key) =>
        Application.Current!.Resources.GetResourceObservable(key);

    private static IDisposable Ink(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Resource(key));
}
