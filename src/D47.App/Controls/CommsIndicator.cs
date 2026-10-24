using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// The selected comms channel and who is on it, as a read-only block on Slab: COMMS, the channel in A, a dot, then the
/// name in White. Full is 32px tall; compact is the size of the switch warning.
/// </summary>
public sealed class CommsIndicator : Border
{
    /// <summary>The widest the full indicator draws; a longer name is cut with an ellipsis.</summary>
    public const double FullMaxWidth = 360;

    public static readonly StyledProperty<CommsIndicatorState?> StateProperty =
        AvaloniaProperty.Register<CommsIndicator, CommsIndicatorState?>(nameof(State));

    private readonly DockPanel _row = new() { VerticalAlignment = VerticalAlignment.Center };
    private bool _compact;

    public CommsIndicator()
    {
        Child = _row;
        ClipToBounds = true;
        IsHitTestVisible = false;
        Focusable = false;
        this[!BackgroundProperty] = new DynamicResourceExtension(ThemeManager.SlabKey);

        Size();
        Draw();
    }

    /// <summary>The size of the switch warning, for the mini panel, in place of the 32px block.</summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            Size();
            Draw();
        }
    }

    public CommsIndicatorState? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty)
        {
            Draw();
        }
    }

    private void Size()
    {
        Padding = _compact ? new Thickness(4, 1) : new Thickness(8, 0);
        Height = _compact ? double.NaN : 32;
        MaxWidth = _compact ? double.PositiveInfinity : FullMaxWidth;
    }

    private void Draw()
    {
        var state = State ?? CommsIndicatorState.NoChannel;
        var text = _compact ? 13d : 14d;
        var label = _compact ? 11d : 12d;

        var gap = _compact ? 4d : 6d;

        _row.Children.Clear();
        Add(Part("COMMS", label, FontWeight.Medium, ThemeManager.GreyKey, tracked: true), Dock.Left, 0);

        if (state.IsNoChannel)
        {
            Add(Part("NO CHANNEL", text, FontWeight.Medium, ThemeManager.GreyKey, tracked: true), Dock.Left, gap);
        }
        else
        {
            Add(Part(state.Channel!, text, FontWeight.SemiBold, ThemeManager.AKey, tracked: true), Dock.Left, gap);
            Add(Part("·", text, FontWeight.Normal, ThemeManager.GreyKey, tracked: false), Dock.Left, gap);

            // Docked before the name, which fills what is left and is the part cut with an ellipsis.
            if (state.Note is { Length: > 0 } note)
            {
                Add(Part(note, label, FontWeight.Medium, ThemeManager.GreyKey, tracked: true), Dock.Right, gap);
            }

            Add(
                state.IsNoOne
                    ? Part("NO ONE", text, FontWeight.Medium, ThemeManager.GreyKey, tracked: true)
                    : Part(state.Who!, text, FontWeight.Medium, ThemeManager.WhiteKey, tracked: false, shrink: true),
                Dock.Left,
                gap);
        }

        AutomationProperties.SetName(this, Spoken(state));
    }

    private void Add(TextBlock part, Dock dock, double gap)
    {
        DockPanel.SetDock(part, dock);
        part.Margin = new Thickness(gap, 0, 0, 0);
        _row.Children.Add(part);
    }

    private static string Spoken(CommsIndicatorState state) =>
        state.IsNoChannel ? "Comms, no channel"
        : state.IsNoOne ? $"Comms, {state.Channel}, no one"
        : $"Comms, {state.Channel}, {state.Who}{(state.Note is { } note ? $", {note}" : string.Empty)}";

    private static TextBlock Part(string text, double size, FontWeight weight, string ink, bool tracked, bool shrink = false)
    {
        var part = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = size,
            FontWeight = weight,
            LetterSpacing = tracked ? size * Fonts.ChromeTracking : 0,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = shrink ? TextTrimming.CharacterEllipsis : TextTrimming.None,
        };

        part[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ink);
        return part;
    }
}
