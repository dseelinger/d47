using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Goals;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The story lines kept in Messages: the Waiting for your answer and Other lines lists on Your story, and one line read.</summary>
public sealed class MessagesView
{
    public const string WaitingHeading = "Waiting for your answer";

    public const string OtherHeading = "Other lines";

    public const string ReadPrefix = "message.read.";

    private readonly MessageStore _store;
    private readonly PanelNavigator _nav;
    private readonly AdventureSurface? _surface;
    private readonly PanelPrompts? _prompts;
    private readonly ToldEntries? _entries;

    internal MessagesView(MessageStore store, PanelNavigator nav, AdventureSurface? surface = null, PanelPrompts? prompts = null, ToldEntries? entries = null)
    {
        _prompts = prompts;
        _store = store;
        _nav = nav;
        _surface = surface;
        _entries = entries;
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
            return AdventuresPage.Muted("That line is gone.");
        }

        _store.MarkRead(key);

        var page = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        page.Children.Add(AdventuresPage.Text(message.Subject, TypeScale.Body));
        page.Children.Add(AdventuresPage.Text(Caption(message), TypeScale.Small, ThemeManager.GreyKey));

        var changeVoice = ChangeVoice(message);

        if (_surface?.Pictures is { } pictures && SpeakerPictures.OfMessage(message) is { } picture && pictures.Find(picture) is not null)
        {
            page.Children.Add(new PictureChooser(pictures, picture, 240, null, changeVoice is null ? [] : [changeVoice]));
        }
        else if (changeVoice is not null)
        {
            page.Children.Add(changeVoice);
        }

        page.Children.Add(AdventuresPage.Text(message.Body, TypeScale.Body));

        if (message.Clip is not null && _surface?.PlayMessage is { } play)
        {
            page.Children.Add(Playing(message, play));
        }

        if (message.Answers.Count > 0)
        {
            page.Children.Add(Answering(message));
        }

        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    /// <summary>Change voice, for a message from a story cast member; null for any other sender.</summary>
    private Button? ChangeVoice(D47Message message)
    {
        if (message.Cast is not { } key || _surface?.CastVoices is not { } voices || _prompts is not { } prompts || voices.Member(key) is null)
        {
            return null;
        }

        var button = new Button { Content = "Change voice", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        button.Click += (_, _) => CastVoiceChooser.Open(prompts, voices, key, () => { });
        return button;
    }

    /// <summary>Play, for a message that kept the clip it was spoken in.</summary>
    private static StackPanel Playing(D47Message message, Func<D47Message, string?> play)
    {
        var status = AdventuresPage.Text(string.Empty, TypeScale.Small, ThemeManager.GreyKey);
        var button = new Button { Content = "Play", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };

        button.Click += async (_, _) => status.Text = await Task.Run(() => play(message)) ?? string.Empty;

        return new StackPanel { Spacing = 4, Children = { button, status } };
    }

    /// <summary>The options an ending message offers while it is unanswered, and the choice once it is made.</summary>
    private Control Answering(D47Message message)
    {
        var holder = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        var chosen = Chosen(message);

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

    private string? Chosen(D47Message message) => Chosen(message, _surface);

    private static string? Chosen(D47Message message, AdventureSurface? surface)
    {
        var storyId = message.AdventureKey is { } key && key.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal)
            ? key[StoryEnding.KeyPrefix.Length..]
            : null;

        return storyId is null ? null : surface?.Stories?.Stories.Find(surface.Commander(), storyId)?.EndingChoice;
    }

    private bool IsWaiting(D47Message message) => IsWaiting(message, _surface);

    /// <summary>Whether a message offers answers, the shown Commander has not chosen one and the story is not stopped.</summary>
    internal static bool IsWaiting(D47Message message, AdventureSurface? surface) =>
        message.Answers.Count > 0 && Chosen(message, surface) is null && !Stopped(message, surface);

    private static bool Stopped(D47Message message, AdventureSurface? surface) =>
        message.AdventureKey is { } key
        && key.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal)
        && surface?.Stories?.Stories.Find(surface.Commander(), key[StoryEnding.KeyPrefix.Length..]) is { State: StoryState.Abandoned or StoryState.Ended };

    internal static string Sender(string from) =>
        from == MessageStore.Narrator
            ? VoiceRoles.Called(VoiceRole.Narrator) ?? from
            : PersonaCatalog.Knows(from) ? PersonaCatalog.Resolve(from).Name : from;

    private static string Caption(D47Message message) =>
        $"{Sender(message.From)} — {message.Sent.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.CurrentCulture)}";

    /// <summary>Whether a line belongs to a stock story run, whose page on the Story bench lists it instead.</summary>
    private bool OfARun(D47Message message) =>
        message.AdventureKey is { } key && _surface?.Stories?.Records?.Keeps(key) == true;

    /// <summary>The lines waiting for an answer and the lines of no story run, as Your story lists them.</summary>
    public StoryLog Log() => StoryLog.Of(_store.All, OfARun, IsWaiting);

    /// <summary>Waiting for your answer: each line with its answers, Play and Change voice; null when nothing waits.</summary>
    public Control? Waiting(StoryLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (log.Waiting.Count == 0)
        {
            return null;
        }

        var holder = new StackPanel { Spacing = 2 };
        holder.Children.Add(StarSystemPage.GroupHead(WaitingHeading, string.Empty, log.Waiting.Count.ToString(CultureInfo.InvariantCulture)));

        foreach (var message in log.Waiting)
        {
            var words = new StackPanel { Spacing = 4 };
            words.Children.Add(Caption(message, ThemeManager.WhiteKey));
            words.Children.Add(AdventuresPage.Text(message.Body, TypeScale.Body));
            words.Children.Add(Answering(message));

            var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };

            if (message.Clip is not null && _surface?.PlayMessage is { } play)
            {
                buttons.Children.Add(Playing(message, play));
            }

            if (ChangeVoice(message) is { } changeVoice)
            {
                buttons.Children.Add(changeVoice);
            }

            if (buttons.Children.Count > 0)
            {
                words.Children.Add(buttons);
            }

            var block = new Border
            {
                Padding = new Thickness(11, 12, 12, 12),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Child = Pictured(words, message),
            };
            ThemeInk.Bind(block, Border.BorderBrushProperty, ThemeManager.AKey);
            ThemeInk.Bind(block, Border.BackgroundProperty, ThemeManager.TileKey);
            holder.Children.Add(block);
        }

        return holder;
    }

    /// <summary>Other lines, newest first, each opening its own page; null when there are none.</summary>
    public Control? Other(StoryLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (log.Other.Count == 0)
        {
            return null;
        }

        var holder = new StackPanel { Spacing = 2 };
        holder.Children.Add(StarSystemPage.GroupHead(OtherHeading, string.Empty, log.Other.Count.ToString(CultureInfo.InvariantCulture)));

        foreach (var message in log.Other)
        {
            var subject = AdventuresPage.Text(message.Subject, TypeScale.Body);
            subject.FontWeight = message.Read ? FontWeight.Normal : FontWeight.Bold;

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
            grid.Children.Add(new StackPanel
            {
                Spacing = 2,
                Children = { subject, AdventuresPage.Text(Caption(message), TypeScale.Small, ThemeManager.GreyKey) },
            });

            if (message.Clip is not null && _surface?.PlayMessage is { } play)
            {
                var playing = Playing(message, play);
                Grid.SetColumn(playing, 1);
                grid.Children.Add(playing);
            }

            var row = ListRow.Dress(new Border
            {
                Child = grid,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            });

            var key = message.Key;
            var crumb = message.Subject;
            ListRow.Opens(row, () => _nav.Drill(new NavCrumb(ReadPrefix + key, crumb) { Level = ReadPrefix }));
            holder.Children.Add(row);
        }

        return holder;
    }

    private Control Pictured(Control words, D47Message message) =>
        _entries?.Pictured(words, SpeakerPictures.OfMessage(message)) ?? words;

    private static TextBlock Caption(D47Message message, string key) =>
        AdventuresPage.Text(Caption(message), TypeScale.Small, key);
}
