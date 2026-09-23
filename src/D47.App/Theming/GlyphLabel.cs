using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace D47.App.Theming;

/// <summary>
/// Opens a glyph button's <c>Label</c> popup while the button is hovered or keyboard-focused, enabled
/// and visible, and closes it otherwise.
/// </summary>
public static class GlyphLabel
{
    public static readonly AttachedProperty<bool> FollowsProperty =
        AvaloniaProperty.RegisterAttached<Button, bool>("Follows", typeof(GlyphLabel));

    static GlyphLabel() => FollowsProperty.Changed.AddClassHandler<Button>(OnFollowsChanged);

    public static bool GetFollows(Button button) => button.GetValue(FollowsProperty);

    public static void SetFollows(Button button, bool value) => button.SetValue(FollowsProperty, value);

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
            if (args.Property == Visual.IsVisibleProperty || args.Property == InputElement.IsEffectivelyEnabledProperty)
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
