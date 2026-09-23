using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml.MarkupExtensions;
using D47.App.Theming;

using D47.App.Windowing;

namespace D47.App.Controls;

/// <summary>
/// A yes/no question the Commander has to answer before something irreversible or expensive happens.
/// </summary>
public sealed class ConfirmWindow : Window
{
    private readonly TaskCompletionSource<bool> _answer = new();

    public ConfirmWindow(string title, string question, string confirmLabel, string declineLabel)
    {
        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;

        var text = new TextBlock
        {
            Text = question,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var confirm = new Button { Content = confirmLabel, MinWidth = 110 };
        var decline = new Button { Content = declineLabel, MinWidth = 110 };

        confirm.Click += (_, _) => Answer(true);
        decline.Click += (_, _) => Answer(false);

        Modal.Apply(this, "Confirm", title, text, [decline, confirm]);

        // Closing the window without choosing is a no.
        Closed += (_, _) => _answer.TrySetResult(false);

        Opened += (_, _) => decline.Focus();
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);

    private void Answer(bool confirmed)
    {
        _answer.TrySetResult(confirmed);
        Close();
    }

    /// <summary>Shows the dialog and waits for the answer.</summary>
    public async Task<bool> AskAsync(Window owner)
    {
        await this.Over(owner).ConfigureAwait(true);
        return await _answer.Task.ConfigureAwait(true);
    }
}
