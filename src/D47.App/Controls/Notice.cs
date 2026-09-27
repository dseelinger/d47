using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>How serious a <see cref="Notice"/> is.</summary>
public enum NoticeLevel
{
    /// <summary>Something failed or is blocked, in Red.</summary>
    Error,

    /// <summary>A caution the Commander can act on, in Warn.</summary>
    Warning,
}

/// <summary>
/// An error or warning message: a 3px bar in the level colour on a 12% ground of it, an upper-case chrome
/// label, White prose, an optional mono detail line and optional tiles at the right.
/// </summary>
public sealed class Notice : Border
{
    /// <summary>The text of a notice.</summary>
    public const double TextSize = 14;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Notice, string?>(nameof(Text));

    private readonly TextBlock _label;
    private readonly TextBlock _text;
    private readonly TextBlock _detail;
    private readonly StackPanel _actions;

    private NoticeLevel _level;
    private string? _labelText;

    public Notice()
        : this(NoticeLevel.Error)
    {
    }

    /// <param name="text">A block to draw as the text part in place of the notice's own, keeping its type.</param>
    public Notice(NoticeLevel level = NoticeLevel.Error, bool inline = false, TextBlock? text = null)
    {
        _label = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };

        _text = text ?? new TextBlock
        {
            FontFamily = new FontFamily(Fonts.ProseFamily),
            FontSize = TextSize,
            LineHeight = TextSize * 1.4,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(_text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        _detail = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.MetaSmall,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(_detail, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
        _actions.Children.CollectionChanged += (_, _) => _actions.IsVisible = _actions.Children.Count > 0;
        Grid.SetColumn(_actions, 1);

        var main = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _label, _text, _detail },
        };

        Child = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Children = { main, _actions },
        };

        BorderThickness = new Thickness(3, 0, 0, 0);
        Padding = inline ? new Thickness(11, 6, 10, 6) : new Thickness(13, 10, 14, 10);
        MinHeight = inline ? 0 : TypeScale.MinimumTarget;

        Level = level;
    }

    /// <summary>Red for an error, Warn for a warning; the label defaults to the level's name.</summary>
    public NoticeLevel Level
    {
        get => _level;
        set
        {
            _level = value;

            var warning = value == NoticeLevel.Warning;
            var ink = warning ? ThemeManager.WarnKey : ThemeManager.RedKey;

            Themed(this, BorderBrushProperty, ink);
            Themed(this, BackgroundProperty, warning ? ThemeManager.WarnGroundKey : ThemeManager.RedGroundKey);
            Themed(_label, TextBlock.ForegroundProperty, ink);

            AutomationProperties.SetLiveSetting(this, warning ? AutomationLiveSetting.Polite : AutomationLiveSetting.Assertive);

            ShowLabel();
        }
    }

    /// <summary>What failed, drawn upper-case; null for "Error" or "Warning".</summary>
    public string? Label
    {
        get => _labelText;
        set
        {
            _labelText = value;
            ShowLabel();
        }
    }

    /// <summary>What happened, then what to do.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>A code or source, drawn upper-case in mono under the text; null for none.</summary>
    public string? Detail
    {
        get => _detail.Text;
        set
        {
            _detail.Text = value?.ToUpperInvariant();
            _detail.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>The tiles at the right, 2px apart.</summary>
    public Avalonia.Controls.Controls Actions => _actions.Children;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            var text = change.GetNewValue<string?>();

            _text.Text = text;
            _text.IsVisible = !string.IsNullOrEmpty(text);
            AutomationProperties.SetName(this, text);
        }
    }

    private void ShowLabel() =>
        _label.Text = (_labelText ?? (_level == NoticeLevel.Warning ? "Warning" : "Error")).ToUpperInvariant();

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
