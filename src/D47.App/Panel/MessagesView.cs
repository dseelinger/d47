using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Messages;

namespace D47.App.Panel;

/// <summary>The Messages page: the list, newest first, and one message read.</summary>
public sealed class MessagesView : UserControl
{
    public const string RootKey = "messages";

    public const string ReadPrefix = "message.read.";

    private readonly MessageStore _store;
    private readonly PanelNavigator _nav;
    private readonly StackPanel _list = new() { Spacing = 2 };

    public MessagesView(MessageStore store, PanelNavigator nav)
    {
        _store = store;
        _nav = nav;

        var root = new DockPanel { Margin = new Thickness(14) };
        var (title, _) = RoutingKit.Title("Messages");
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);
        root.Children.Add(new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = root;
        Rebuild();
    }

    /// <summary>The button label on the Adventures tab, carrying the unread count.</summary>
    public static string ButtonLabel(MessageStore store) =>
        store.UnreadCount is > 0 and var unread
            ? $"Messages ({unread.ToString(CultureInfo.InvariantCulture)})"
            : "Messages";

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _store.Changed += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _store.Changed -= OnChanged;
    }

    /// <summary>The page for a reading crumb; opening it marks the message read.</summary>
    public Control? Build(NavCrumb crumb)
    {
        if (!crumb.Key.StartsWith(ReadPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var key = crumb.Key[ReadPrefix.Length..];
        var message = _store.All.FirstOrDefault(other => other.Key == key);

        if (message is null)
        {
            return AdventuresPage.Muted("That message is gone.");
        }

        _store.MarkRead(key);

        var page = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        page.Children.Add(AdventuresPage.Text(message.Subject, TypeScale.Body));
        page.Children.Add(AdventuresPage.Text(Caption(message), TypeScale.Small, ThemeManager.GreyKey));
        page.Children.Add(AdventuresPage.Text(message.Body, TypeScale.Body));

        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    private static string Caption(D47Message message) =>
        $"{message.From} — {message.Sent.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.CurrentCulture)}";

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        _list.Children.Clear();

        var messages = _store.All;

        if (messages.Count == 0)
        {
            _list.Children.Add(AdventuresPage.Muted("No messages yet. Story beats arrive here as they are said."));
            return;
        }

        foreach (var message in messages)
        {
            var subject = AdventuresPage.Text(message.Subject, TypeScale.Body);
            subject.FontWeight = message.Read ? FontWeight.Normal : FontWeight.Bold;

            var row = ListRow.Dress(new Border
            {
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children = { subject, AdventuresPage.Text(Caption(message), TypeScale.Small, ThemeManager.GreyKey) },
                },
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            });

            var key = message.Key;
            var crumb = message.Subject;
            row.PointerPressed += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + key, crumb));
            _list.Children.Add(row);
        }
    }
}
