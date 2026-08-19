using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml.MarkupExtensions;
using D47.App.Theming;

using D47.App.Windowing;

namespace D47.App.Controls;

/// <summary>
/// A yes/no question the Commander has to answer before something irreversible or expensive
/// happens. Built in code rather than as an axaml pair because it has one layout and no state —
/// a markup file and a code-behind for two buttons is structure that buys nothing.
/// <para>
/// Defaults to <b>no</b>. The button that costs nothing is focused, so pressing Enter out of
/// habit declines rather than starting a several-hundred-megabyte download.
/// </para>
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

        // Themed the same way every control the settings view builds in code is: a dynamic
        // resource, so the dialog repaints with the rest of the app rather than pinning a
        // literal (list.md Phase 4, "Themes").
        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var text = new TextBlock
        {
            Text = question,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20),
        };

        Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var confirm = new Button { Content = confirmLabel, MinWidth = 110 };
        var decline = new Button { Content = declineLabel, MinWidth = 110, Margin = new Thickness(0, 0, 10, 0) };

        confirm.Click += (_, _) => Answer(true);
        decline.Click += (_, _) => Answer(false);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                text,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { decline, confirm },
                },
            },
        };

        // Closing the window without choosing is a no. Anything else would treat a dismissed
        // dialog as consent, which is exactly what consent is not.
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

    /// <summary>Shows the dialog and waits for the answer. False if it was dismissed.</summary>
    public async Task<bool> AskAsync(Window owner)
    {
        await this.Over(owner).ConfigureAwait(true);
        return await _answer.Task.ConfigureAwait(true);
    }
}
