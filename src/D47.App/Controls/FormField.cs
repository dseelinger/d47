using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// What a Commander has to do about a field
/// (<a href="https://github.com/dseelinger/d47/issues/253">#253</a>).
/// <para>
/// <b>Three states, because d47 has a third one that generic forms do not.</b> Standard practice
/// knows required and optional; this app also has <em>optional because d47 already knows the
/// answer</em> — the jump range of the ship you are flying, the system you are standing in. The
/// Commander's own wording for it is that those fields <em>"can be supplied from the current
/// ship"</em>, and it is not the same thing as a static default of 60.
/// </para>
/// </summary>
public enum FieldNeed
{
    /// <summary>Leave it empty and it is filled with a fixed default, or simply not used.</summary>
    Optional,

    /// <summary>Nothing happens without it.</summary>
    Required,

    /// <summary>Leave it empty and d47 answers it from the game.</summary>
    Supplied,
}

/// <summary>
/// A labelled box that says what it needs (#253).
/// <para>
/// <b>The mark goes on the label, never in the box.</b> A placeholder is not a label and it
/// disappears the moment the Commander types — which is exactly when "this one is required" still
/// needs to be true. That is why the Trade run card's <c>required</c> placeholder was the worst
/// cell in the survey rather than the best: somebody hit this problem, had nowhere to put the
/// answer, and put it where it would vanish.
/// </para>
/// <para>
/// <b>Required is marked and optional is not</b>, because required is the minority by a distance —
/// one field of four on the Neutron Plotter, none of four on Road to Riches. Marking the majority
/// would put <em>(optional)</em> on nine labels to spare three.
/// </para>
/// <para>
/// <b>One implementation, not one per page.</b> This was a private class inside
/// <c>RoutePlanPage</c>, and the alternative to lifting it was writing the convention out by hand
/// in four more files — which is how two of them end up spelling it differently.
/// </para>
/// </summary>
public sealed class FormField
{
    /// <summary>The mark for a field nothing happens without.</summary>
    public const string RequiredMark = "*";

    /// <summary>The mark for a field d47 fills from the game.</summary>
    /// <remarks>
    /// A filled diamond rather than a second asterisk. An asterisk means one thing, and giving it
    /// two meanings is the problem this issue is about — so the third state gets a shape of its
    /// own, solid so that it survives being read at a metre in a headset.
    /// </remarks>
    public const string SuppliedMark = "◆";

    private readonly string _label;
    private readonly string _placeholder;
    private readonly Func<string?>? _supplied;

    private readonly TextBox _box = new()
    {
        MinHeight = 30,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    /// <param name="supplied">
    /// The value d47 would use if this is left empty, read fresh each time it is asked for. Null
    /// where there is nothing to say yet — before the journal has been read, or with no ship
    /// known — and then the bare phrase is drawn rather than <c>this ship's ()</c> or a zero.
    /// </param>
    /// <param name="width">
    /// How wide the box is. The default suits a number or a short name; a field whose placeholder
    /// quotes a live value needs more, because a clipped value is worse than no value — "where you
    /// are now (Shinra…" tells a Commander less than the bare phrase did.
    /// </param>
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

        // Set for correctness and for the day it does something. **It announces nothing on
        // Avalonia 12.1.1** — measured rather than assumed, which the issue asked for: no member
        // of TextBoxAutomationPeer or of AutomationPeer itself mentions it, and the platform
        // provider builds its answers from the peer. So the state a screen reader actually reads
        // is carried in the name below, which peers do surface.
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

    /// <summary>
    /// Re-reads the value d47 would supply.
    /// <para>
    /// <b>It has to stay current.</b> Jump range changes on a ship swap and on a refit; where you
    /// are changes on every jump. A placeholder quoting the ship a Commander was flying an hour
    /// ago is worse than the bare phrase was, because the phrase was never wrong.
    /// </para>
    /// </summary>
    public void Refresh()
    {
        _box.PlaceholderText = _supplied?.Invoke() is { Length: > 0 } value
            ? $"{_placeholder} ({value})"
            : _placeholder;
    }

    private Control Draw() =>
        new StackPanel { Spacing = 3, Children = { Label(_label, Need), _box } };

    /// <summary>
    /// A field's label, carrying its mark — for the pages that build their own boxes and only
    /// want the convention (#253).
    /// <para>
    /// <b>This is the "one implementation" half of the ask.</b> Routing's Course and Market pages
    /// and <c>PersonaWindow</c> each construct their own boxes at their own widths, so making them
    /// adopt the whole field would be a rewrite for no gain — but writing the mark out by hand in
    /// three more files is how two of them end up spelling it differently.
    /// </para>
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

        // Not read by a screen reader, which would otherwise say the label twice — once here and
        // once from the box's own name, which already carries the state.
        AutomationProperties.SetAccessibilityView(row, AccessibilityView.Raw);

        return row;
    }

    /// <summary>
    /// Tells a screen reader what this box needs (#253).
    /// <para>
    /// <b>The visible mark is a glyph, and a glyph is not read aloud</b>, so the state is spelled
    /// into the box's name — which is what peers actually surface.
    /// </para>
    /// <para>
    /// <b><c>IsRequiredForForm</c> is set and announces nothing on Avalonia 12.1.1.</b> Measured
    /// rather than assumed, which is what the issue asked for: neither
    /// <c>TextBoxAutomationPeer</c> nor <c>AutomationPeer</c> itself has a member mentioning it,
    /// and the platform provider builds its answers from the peer — so the property is stored and
    /// read back and reaches no screen reader. It is set anyway, because it is the correct thing
    /// to say and costs nothing on the day Avalonia surfaces it; the name is what does the work
    /// today.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The mark itself, or null for a field that needs no comment.
    /// <para>
    /// <b>Drawn larger than the label it sits beside</b>, and bold. Routing is furnished on both
    /// surfaces, and a small glyph next to a small label is exactly the thing that is legible on a
    /// monitor and gone at a metre in a headset.
    /// </para>
    /// </summary>
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

        // Not colour alone: the two are different shapes as well as different colours, so a
        // Commander who cannot tell the accent from the info colour still has the answer.
        mark.VerticalAlignment = VerticalAlignment.Center;

        return mark;
    }

    /// <summary>
    /// The key to the marks, for the foot of a form.
    /// <para>
    /// <b>The legend is not optional.</b> A mark without a key is a convention a reader has to
    /// already know, and the third state is one nobody could know — no other application has a
    /// "your ship answers this" field.
    /// </para>
    /// </summary>
    /// <param name="supplied">
    /// Whether this form has a ship-supplied field on it. A key naming a mark the form does not
    /// use sends a Commander looking for something that is not there.
    /// </param>
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
