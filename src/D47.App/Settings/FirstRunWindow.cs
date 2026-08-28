using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// The guided way in for a fresh install (Phase 16, "Ask for the keys on the first run
/// that needs them").
/// <para>
/// A fresh install has no language-model key, so without this the first thing a Commander does is
/// hunt for the right row in a surface with fourteen sections. This shows the two or three that
/// matter, in order, with what each one sends and where.
/// </para>
/// <para>
/// <b>It is not a wall.</b> Capabilities are state rather than guards (Phase 3), so
/// declining every key leaves d47 running and typed rather than a dead app behind a form. Every
/// step can be skipped, the window can be closed at any point, and nothing here is recorded as
/// having been shown — <see cref="FirstRun"/> decides from live state each time.
/// </para>
/// <para>
/// The rows are the real descriptor rows and the control is the real
/// <see cref="SecretEditor"/>. Nothing about a key is re-authored here.
/// </para>
/// </summary>
public sealed class FirstRunWindow : Window
{
    public FirstRunWindow(IReadOnlyList<FirstRunStep> steps, SettingsService settings)
    {
        Title = "Set up Directive 47";
        Width = 620;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var stack = new StackPanel { Spacing = 16, Margin = new Thickness(24) };

        stack.Children.Add(Heading("Directive 47 needs a key or two"));
        stack.Children.Add(Body(
            "Everything below is optional. Directive 47 runs without any of it — you will get a "
            + "typed companion that reads your journal and answers from what it can see, rather "
            + "than one that talks back."));

        foreach (var step in steps)
        {
            stack.Children.Add(StepCard(step, settings));
        }

        stack.Children.Add(Body(
            "You can change any of this later in Settings, and reopen this window from About. "
            + "Keys get rotated and revoked, so this is not a one-time door."));

        var done = new Button { Content = "Done", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right };
        done.Click += (_, _) => Close();
        stack.Children.Add(done);

        Content = new ScrollViewer { Content = stack, MaxHeight = 720 };
    }

    /// <summary>
    /// One key: what it is for, what it costs you in privacy, and the control to set it.
    /// </summary>
    private Control StepCard(FirstRunStep step, SettingsService settings)
    {
        var card = new StackPanel { Spacing = 6 };

        var title = new TextBlock
        {
            Text = step.Required
                ? $"{step.Row.Label} — needed to hold a conversation"
                : $"{step.Row.Label} — optional",
            FontWeight = FontWeight.Medium,
        };

        Themed(title, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        card.Children.Add(title);

        if (step.Row.Help is { Length: > 0 } help)
        {
            card.Children.Add(Body(help));
        }

        // Computed from EgressDisclosure rather than written here, for the same reason the
        // privacy section computes its own: hand-written copy about what leaves the machine goes
        // stale, and this is the one screen where a Commander is deciding whether to trust it.
        if (step.Egress is { } egress)
        {
            var line = Body($"{egress.Line}\n{egress.What}");
            Themed(line, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            card.Children.Add(line);
        }

        card.Children.Add(new SecretEditor(step.Row, settings));

        var border = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = card,
        };

        Themed(border, Border.BorderBrushProperty, ThemeManager.BorderKey);
        return border;
    }

    private TextBlock Heading(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        return block;
    }

    private TextBlock Body(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        return block;
    }

    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));
}
