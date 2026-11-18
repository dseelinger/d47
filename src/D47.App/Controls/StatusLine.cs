using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>A status line: an outcome in Grey, a failure as an inline red notice, or the wait mark with its sentence.</summary>
public sealed class StatusLine : StackPanel
{
    private readonly TextBlock _note = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly Notice _failure = new(inline: true);

    private readonly WaitingState _waiting = new(WaitForm.Line) { IsVisible = false };

    private string? _waitingFor;

    public StatusLine()
    {
        IsVisible = false;
        _note[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ThemeManager.GreyKey);
        Children.Add(_waiting);
        Children.Add(_note);
        Children.Add(_failure);
    }

    /// <summary>What the line is showing, or null when it shows nothing.</summary>
    public string? Text =>
        !IsVisible ? null
        : _waiting.IsVisible ? _waitingFor
        : _failure.IsVisible ? _failure.Text
        : _note.Text;

    /// <summary>Shows the wait mark with <paramref name="sentence"/> after it, until <see cref="Say"/>, <see cref="Fail"/> or <see cref="Clear"/>.</summary>
    public void Wait(string sentence)
    {
        _note.IsVisible = false;
        _note.Text = null;
        _failure.IsVisible = false;
        _failure.Text = null;
        _waitingFor = sentence;
        _waiting.SetSentence(sentence);
        _waiting.IsVisible = true;
        IsVisible = true;
    }

    /// <summary>Shows an outcome that is not a failure.</summary>
    public void Say(string text) => Show(text, failed: false);

    /// <summary>Shows a failure.</summary>
    public void Fail(string? text) => Show(text, failed: true);

    public void Clear()
    {
        IsVisible = false;
        StopWaiting();
        _note.Text = null;
        _failure.Text = null;
    }

    private void Show(string? text, bool failed)
    {
        StopWaiting();
        _note.IsVisible = !failed;
        _note.Text = failed ? null : text;
        _failure.IsVisible = failed;
        _failure.Text = failed ? text : null;
        IsVisible = true;
    }

    private void StopWaiting()
    {
        _waiting.IsVisible = false;
        _waitingFor = null;
        _waiting.SetSentence(null);
    }
}
