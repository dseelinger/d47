using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Windowing;

namespace D47.App.Panel;

/// <summary>Asks before an unsold data total is zeroed (#556).</summary>
public sealed class UnsoldResetDialog : ModalDialog
{
    public const double DialogWidth = 560;

    public const string ConfirmName = "ConfirmReset";

    public const string CancelName = "CancelReset";

    public const string Drifted =
        "Use this after you've sold the data somewhere D47 didn't see, or if the total has drifted.";

    private readonly TaskCompletionSource<bool> _answer = new();

    /// <param name="phrase">The spoken keyword that also reaches the reset.</param>
    public UnsoldResetDialog(string title, string value, string phrase)
    {
        Title = title;
        Width = DialogWidth;

        var held = new Run(value) { FontFamily = new FontFamily(Fonts.MonoFamily) };
        Themed(held, TextElement.ForegroundProperty, ThemeManager.AKey);

        var question = new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            Inlines = new InlineCollection
            {
                new Run("D47 will count this total from zero from now on. The "),
                held,
                new Run(" it holds now can't be brought back."),
            },
        };
        Themed(question, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var drifted = new TextBlock
        {
            Text = Drifted,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(drifted, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var confirm = new Button
        {
            Name = ConfirmName,
            Content = "RESET",
            MinWidth = 76,
            Height = TypeScale.MinimumTarget,
            Classes = { Settings.SettingsView.DestructiveClass },
        };
        var cancel = new Button { Name = CancelName, Content = "CANCEL", MinWidth = 76, Height = TypeScale.MinimumTarget };

        confirm.Click += (_, _) => Answer(true);
        cancel.Click += (_, _) => Answer(false);

        var hint = new TextBlock
        {
            Text = Hint(phrase),
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        Modal.Apply(
            this,
            "Unsold data · cannot be undone",
            title,
            new StackPanel { Spacing = Modal.BlockGap, Children = { question, drifted } },
            [confirm, cancel, hint]);

        // Closing it without choosing is a no.
        Closed += (_, _) => _answer.TrySetResult(false);

        Opened += (_, _) => cancel.Focus();
    }

    /// <summary>The footer line: the model cannot reset the total, and the Commander can.</summary>
    public static string Hint(string phrase) =>
        $"Protected. The model can't do this. You can, here or by saying “{phrase}”.";

    /// <summary>Shows the dialog and waits for the answer.</summary>
    public async Task<bool> AskAsync(Window owner)
    {
        await this.Over(owner).ConfigureAwait(true);
        return await _answer.Task.ConfigureAwait(true);
    }

    private void Answer(bool confirmed)
    {
        _answer.TrySetResult(confirmed);
        Close();
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
