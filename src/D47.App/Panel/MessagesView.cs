using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The Messages page: the list, newest first, and one message read.</summary>
public sealed class MessagesView : UserControl
{
    public const string RootKey = "messages";

    public const string ReadPrefix = "message.read.";

    private readonly MessageStore _store;
    private readonly PanelNavigator _nav;
    private readonly AdventureSurface? _surface;
    private readonly StackPanel _list = new() { Spacing = 2 };

    public MessagesView(MessageStore store, PanelNavigator nav, AdventureSurface? surface = null)
    {
        _store = store;
        _nav = nav;
        _surface = surface;

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

        if (_surface?.Pictures is { } pictures && message.Picture is { } picture && pictures.Find(picture) is not null)
        {
            var holder = new StackPanel { Spacing = 6 };
            ShowPicture(holder, pictures, picture);
            page.Children.Add(holder);
        }

        page.Children.Add(AdventuresPage.Text(message.Body, TypeScale.Body));

        if (message.Answers.Count > 0)
        {
            page.Children.Add(Answering(message));
        }

        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    /// <summary>A cast member's picture, the Commander's own where they chose one, with the buttons that replace it.</summary>
    private void ShowPicture(StackPanel holder, CastPictures pictures, string picture)
    {
        holder.Children.Clear();

        if (pictures.Find(picture) is { } file)
        {
            try
            {
                using var bytes = new MemoryStream(File.ReadAllBytes(file));
                holder.Children.Add(new Image
                {
                    Source = new Bitmap(bytes),
                    MaxWidth = 240,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                holder.Children.Add(AdventuresPage.Muted("The picture could not be read."));
            }
        }

        var status = AdventuresPage.Text(string.Empty, TypeScale.Small, ThemeManager.GreyKey);
        var change = new Button { Content = "Change picture" };
        var restore = new Button { Content = "Use the default", IsEnabled = pictures.IsChosen(picture) };

        change.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
            {
                status.Text = "No file picker here.";
                return;
            }

            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a picture",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = CastPictureImport.Patterns }],
            });

            if (picked.Count == 0)
            {
                return;
            }

            change.IsEnabled = false;
            string? refusal;

            try
            {
                await using var stream = await picked[0].OpenReadAsync();
                var name = picked[0].Name;
                refusal = await Task.Run(() => CastPictureImport.Save(stream, name, pictures.Chosen(picture)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                refusal = $"The picture could not be saved: {ex.Message}";
            }

            if (refusal is not null)
            {
                change.IsEnabled = true;
                status.Text = refusal;
                return;
            }

            ShowPicture(holder, pictures, picture);
        };

        restore.Click += (_, _) =>
        {
            try
            {
                pictures.UseDefault(picture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                status.Text = $"Your picture could not be removed: {ex.Message}";
                return;
            }

            ShowPicture(holder, pictures, picture);
        };

        holder.Children.Add(new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Children = { change, restore },
        });
        holder.Children.Add(status);
    }

    /// <summary>The options an ending message offers while it is unanswered, and the choice once it is made.</summary>
    private Control Answering(D47Message message)
    {
        var holder = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        var storyId = message.AdventureKey is { } key && key.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal)
            ? key[StoryEnding.KeyPrefix.Length..]
            : null;
        var chosen = storyId is null ? null : _surface?.Stories?.Stories.Find(_surface.Commander(), storyId)?.EndingChoice;

        if (chosen is not null)
        {
            var label = message.Answers.FirstOrDefault(answer => answer.Id == chosen)?.Label ?? chosen;
            holder.Children.Add(AdventuresPage.Text($"You chose: {label}", TypeScale.Body, ThemeManager.GreyKey));
            return holder;
        }

        if (_surface?.AnswerEnding is not { } answer)
        {
            return holder;
        }

        var refusal = AdventuresPage.Text(string.Empty, TypeScale.Small, ThemeManager.GreyKey);

        for (var at = 0; at < message.Answers.Count; at++)
        {
            var position = at + 1;
            var label = message.Answers[at].Label;
            var button = new Button { Content = label, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };

            button.Click += (_, _) =>
            {
                if (answer(position) is { } refused)
                {
                    refusal.Text = refused;
                    return;
                }

                holder.Children.Clear();
                holder.Children.Add(AdventuresPage.Text($"You chose: {label}", TypeScale.Body, ThemeManager.GreyKey));
            };

            holder.Children.Add(button);
        }

        holder.Children.Add(refusal);
        return holder;
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
