using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>What a Commander has to do about a field (#253).</summary>
public enum FieldNeed
{
    /// <summary>Leave it empty and it is filled with a fixed default, or simply not used.</summary>
    Optional,

    /// <summary>Nothing happens without it.</summary>
    Required,

    /// <summary>Leave it empty and d47 answers it from the game.</summary>
    Supplied,
}

/// <summary>A labelled box that says what it needs (#253).</summary>
public sealed class FormField
{
    /// <summary>The mark for a field nothing happens without.</summary>
    public const string RequiredMark = "*";

    /// <summary>The mark for a field d47 fills from the game.</summary>
    public const string SuppliedMark = "◆";

    private readonly string _label;
    private readonly string _placeholder;
    private readonly Func<string?>? _supplied;

    private readonly TextBox _box = new()
    {
        MinHeight = 30,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    /// <summary>
    /// <param name="supplied"> The value d47 would use if this is left empty, read fresh each time it
    /// is asked for.
    /// </summary>
    /// <param name="supplied">
    /// The value d47 would use if this is left empty, read fresh each time it is asked for.
    /// </param>
    /// <param name="width">How wide the box is.</param>
    public FormField(
        string label,
        string placeholder,
        FieldNeed need = FieldNeed.Optional,
        Func<string?>? supplied = null,
        double width = 190)
    {
        _label = label;
        _placeholder = placeholder;
        _supplied = supplied;
        Need = need;

        _box.Width = width;

        // Set for correctness and for the day it does something. **It announces nothing on Avalonia 12.1.1**
        // — measured rather than assumed, which the issue asked for: no member of TextBoxAutomationPeer or of
        // AutomationPeer itself mentions it, and the platform provider builds its answers from the peer.
        Announce(_box, label, need);

        Refresh();
    }

    /// <summary>What this field needs, for a caller deciding which legend to draw.</summary>
    public FieldNeed Need { get; }

    /// <summary>What the Commander typed, or null.</summary>
    public string? Text => _box.Text;

    /// <summary>The box itself, for a caller that wants to name it or focus it.</summary>
    public TextBox Box => _box;

    /// <summary>The label and the box, drawn.</summary>
    public Control Control => Draw();

    /// <summary>Re-reads the value d47 would supply.</summary>
    public void Refresh()
    {
        _box.PlaceholderText = _supplied?.Invoke() is { Length: > 0 } value
            ? $"{_placeholder} ({value})"
            : _placeholder;
    }

    private Control Draw() =>
        new StackPanel { Spacing = 3, Children = { Label(_label, Need), _box } };

    /// <summary>
    /// A field's label, carrying its mark — for the pages that build their own boxes and only want the
    /// convention (#253).
    /// </summary>
    public static Control Label(string label, FieldNeed need)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { Caption(label, TypeScale.Small, ThemeManager.TextMutedKey) },
        };

        if (Mark(need) is { } mark)
        {
            row.Children.Add(mark);
        }

        // Not read by a screen reader, which would otherwise say the label twice — once here and once from
        // the box's own name, which already carries the state.
        AutomationProperties.SetAccessibilityView(row, AccessibilityView.Raw);

        return row;
    }

    /// <summary>Tells a screen reader what this box needs (#253).</summary>
    public static void Announce(TextBox box, string label, FieldNeed need)
    {
        AutomationProperties.SetIsRequiredForForm(box, need == FieldNeed.Required);

        AutomationProperties.SetName(box, need switch
        {
            FieldNeed.Required => $"{label}, required",
            FieldNeed.Supplied => $"{label}, optional, filled from your ship",
            _ => label,
        });
    }

    /// <summary>The mark itself, or null for a field that needs no comment.</summary>
    private static Control? Mark(FieldNeed need)
    {
        if (need == FieldNeed.Optional)
        {
            return null;
        }

        var required = need == FieldNeed.Required;

        var mark = Caption(
            required ? RequiredMark : SuppliedMark,
            TypeScale.Body,
            required ? ThemeManager.AccentKey : ThemeManager.InfoKey);

        mark.FontWeight = FontWeight.Bold;

        // Not colour alone: the two are different shapes as well as different colours, so a Commander who
        // cannot tell the accent from the info colour still has the answer.
        mark.VerticalAlignment = VerticalAlignment.Center;

        return mark;
    }

    /// <summary>The key to the marks, for the foot of a form.</summary>
    /// <param name="supplied">Whether this form has a ship-supplied field on it.</param>
    public static Control Legend(bool required = true, bool supplied = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };

        if (required)
        {
            row.Children.Add(Key(RequiredMark, "required", ThemeManager.AccentKey));
        }

        if (supplied)
        {
            row.Children.Add(Key(SuppliedMark, "filled from your ship", ThemeManager.InfoKey));
        }

        return row;
    }

    private static Control Key(string mark, string says, string colourKey)
    {
        var glyph = Caption(mark, TypeScale.Body, colourKey);

        glyph.FontWeight = FontWeight.Bold;
        glyph.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { glyph, Caption(says, TypeScale.Small, ThemeManager.TextMutedKey) },
        };

        AutomationProperties.SetAccessibilityView(row, AccessibilityView.Raw);

        return row;
    }

    private static TextBlock Caption(string text, double size, string colourKey)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }
}
