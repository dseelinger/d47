using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using D47.App.Panel;

namespace D47.App.Theming;

/// <summary>
/// Opens a glyph button's <c>Label</c> popup while the button is hovered or keyboard-focused, enabled
/// and visible, and closes it otherwise. The label reads <see cref="TipProperty"/> when it is set and the
/// button's accessible name otherwise; an empty tip opens no label.
/// </summary>
public static class GlyphLabel
{
    public static readonly AttachedProperty<bool> FollowsProperty =
        AvaloniaProperty.RegisterAttached<Button, bool>("Follows", typeof(GlyphLabel));

    public static readonly AttachedProperty<string?> TipProperty =
        AvaloniaProperty.RegisterAttached<Button, string?>("Tip", typeof(GlyphLabel));

    /// <summary>The label text in capitals: the tip when there is one, else the accessible name.</summary>
    public static readonly IMultiValueConverter Words = new FuncMultiValueConverter<object?, object?>(values =>
    {
        var list = values.ToList();
        var said = list.Count > 0 && list[0] is string { Length: > 0 } tip ? tip : list.Count > 1 ? list[1] as string : null;

        return said?.ToUpperInvariant();
    });

    static GlyphLabel() => FollowsProperty.Changed.AddClassHandler<Button>(OnFollowsChanged);

    public static bool GetFollows(Button button) => button.GetValue(FollowsProperty);

    public static void SetFollows(Button button, bool value) => button.SetValue(FollowsProperty, value);

    public static string? GetTip(Button button) => button.GetValue(TipProperty);

    public static void SetTip(Button button, string? value) => button.SetValue(TipProperty, value);

    /// <summary>Gives <paramref name="button"/> the hover label <paramref name="words"/> in the window and none on the headset's panel.</summary>
    public static void Tip(Button button, string words)
    {
        SetTip(button, words);

        button.AttachedToVisualTree += (_, _) => SetTip(button, PanelView.OnHeadset(button) ? string.Empty : words);
    }

    private static void OnFollowsChanged(Button button, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.NewValue is not true)
        {
            return;
        }

        Popup? label = null;

        void Update()
        {
            if (label is null)
            {
                return;
            }

            var shown = GetFollows(button)
                && button.IsEffectivelyEnabled
                && button.IsEffectivelyVisible
                && GetTip(button) != string.Empty
                && (button.Classes.Contains(":pointerover") || button.Classes.Contains(":focus-visible"));

            if (label.IsOpen != shown)
            {
                label.IsOpen = shown;
            }
        }

        button.TemplateApplied += (_, applied) =>
        {
            if (label is not null)
            {
                label.IsOpen = false;
            }

            label = applied.NameScope.Find<Popup>("Label");
            Update();
        };

        button.Classes.CollectionChanged += (_, _) => Update();

        button.PropertyChanged += (_, args) =>
        {
            if (args.Property == Visual.IsVisibleProperty || args.Property == InputElement.IsEffectivelyEnabledProperty || args.Property == TipProperty)
            {
                Update();
            }
        };

        button.DetachedFromVisualTree += (_, _) =>
        {
            if (label is not null)
            {
                label.IsOpen = false;
            }
        };
    }
}
