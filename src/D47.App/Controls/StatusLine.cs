using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>A status line: an outcome in Grey, or a failure as an inline red notice.</summary>
public sealed class StatusLine : StackPanel
{
    private readonly TextBlock _note = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly Notice _failure = new(inline: true);

    public StatusLine()
    {
        IsVisible = false;
        _note[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ThemeManager.GreyKey);
        Children.Add(_note);
        Children.Add(_failure);
    }

    /// <summary>What the line is showing, or null when it shows nothing.</summary>
    public string? Text => !IsVisible ? null : _failure.IsVisible ? _failure.Text : _note.Text;

    /// <summary>Whether what the line is showing is a failure.</summary>
    public bool Failed => IsVisible && _failure.IsVisible;

    /// <summary>Shows an outcome that is not a failure.</summary>
    public void Say(string text) => Show(text, failed: false);

    /// <summary>Shows a failure.</summary>
    public void Fail(string? text) => Show(text, failed: true);

    public void Clear()
    {
        IsVisible = false;
        _note.Text = null;
        _failure.Text = null;
    }

    private void Show(string? text, bool failed)
    {
        _note.IsVisible = !failed;
        _note.Text = failed ? null : text;
        _failure.IsVisible = failed;
        _failure.Text = failed ? text : null;
        IsVisible = true;
    }
}
