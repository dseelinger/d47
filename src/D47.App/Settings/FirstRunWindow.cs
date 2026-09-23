using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// The guided way in for a fresh install (Phase 16, "Ask for the keys on the first run that needs
/// them").
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

        Themed(this, BackgroundProperty, ThemeManager.BgKey);

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

    /// <summary>One key: what it is for, what it costs you in privacy, and the control to set it.</summary>
    private Control StepCard(FirstRunStep step, SettingsService settings)
    {
        var card = new StackPanel { Spacing = 8 };

        var title = new TextBlock
        {
            Text = step.Required
                ? $"{step.Row.Label} — needed to hold a conversation"
                : $"{step.Row.Label} — optional",
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        card.Children.Add(title);

        var rule = new Border { Height = 1 };
        Themed(rule, Border.BackgroundProperty, ThemeManager.LineKey);
        card.Children.Add(rule);

        if (step.Row.Help is { Length: > 0 } help)
        {
            card.Children.Add(Body(help));
        }

        // Computed from EgressDisclosure rather than written here, for the same reason the privacy section
        // computes its own: hand-written copy about what leaves the machine goes stale, and this is the one
        // screen where a Commander is deciding whether to trust it.
        if (step.Egress is { } egress)
        {
            card.Children.Add(Body($"{egress.Line}\n{egress.What}"));
        }

        card.Children.Add(new SecretEditor(step.Row, settings));

        return card;
    }

    /// <summary>The window's Screen title, at Heading size, over a 1px A rule.</summary>
    private static Control Heading(string text)
    {
        var block = TitleText.Build(text, TypeScale.Heading, TitleRank.Screen, sentence: true);
        block.TextWrapping = TextWrapping.Wrap;

        return TitleText.GroupRow(block);
    }

    private TextBlock Body(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));
}
