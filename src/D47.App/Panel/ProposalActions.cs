using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace D47.App.Panel;

/// <summary>
/// The Accept and Decline row a proposal shows while it is still waiting — shared by the Checklist
/// page's own card and the conversation thread's (#277).
/// </summary>
internal static class ProposalActions
{
    public static Control Build(Action accept, Action decline, string footer)
    {
        var acceptButton = new Button { Content = "Accept" };
        acceptButton.Click += (_, _) => accept();

        var declineButton = new Button { Content = "Decline" };
        declineButton.Click += (_, _) => decline();

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Muted(footer),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { acceptButton, declineButton },
                },
            },
        };
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = Theming.TypeScale.Body,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.GreyKey));

        return block;
    }
}
