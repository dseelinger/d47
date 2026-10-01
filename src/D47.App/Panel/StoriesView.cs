using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The Stories page: the running story, the stock story cards, and one card read.</summary>
public sealed class StoriesView : UserControl
{
    public const string RootKey = "stories";

    public const string ReadPrefix = "story.read.";

    private readonly AdventureSurface _surface;
    private readonly StoryDirector _director;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly StatusLine _status = new();

    public StoriesView(AdventureSurface surface, StoryDirector director, PanelNavigator nav, PanelPrompts prompts)
    {
        _surface = surface;
        _director = director;
        _nav = nav;
        _prompts = prompts;

        var root = new DockPanel { Margin = new Thickness(14) };
        var (title, _) = RoutingKit.Title("Stories");
        var intro = AdventuresPage.Muted(
            "Stock stories that run for months, a chapter at a time. Picking one makes its words your Backstory and "
            + "has the ship's AI write chapter one, which ends at a Guardian beacon. When a chapter finishes, the next "
            + "is written and begins. While a story runs, the Guardian cores wait for its beacon scan. Pause or abandon "
            + "the story to have them back at once.");
        intro.Margin = new Thickness(0, 0, 0, 10);

        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(intro, Dock.Top);
        DockPanel.SetDock(_status, Dock.Top);
        root.Children.Add(title);
        root.Children.Add(intro);
        root.Children.Add(_status);
        root.Children.Add(new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        Content = root;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _director.Stories.Changed += OnChanged;
        _director.WritingChanged += OnChanged;
        _director.OdysseyChanged += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _director.Stories.Changed -= OnChanged;
        _director.WritingChanged -= OnChanged;
        _director.OdysseyChanged -= OnChanged;
    }

    /// <summary>The page for a reading crumb.</summary>
    public Control? Build(NavCrumb crumb) =>
        crumb.Key.StartsWith(ReadPrefix, StringComparison.Ordinal)
            ? BuildReading(crumb.Key[ReadPrefix.Length..])
            : null;

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        _list.Children.Clear();

        var current = _director.Stories.Current(_surface.Commander());

        if (_director.WithoutOdyssey(_surface.Commander()))
        {
            var notice = AdventuresPage.Text(StoryDirector.NeedsOdyssey, TypeScale.Body);
            notice.Margin = new Thickness(0, 0, 0, 10);
            _list.Children.Add(notice);
        }

        if (current is not null)
        {
            _list.Children.Add(CurrentCard(current));
        }

        if (current is null && _director.Catalog.Cards.Count == 0)
        {
            _list.Children.Add(AdventuresPage.Muted("No stories yet."));
        }

        foreach (var card in _director.Catalog.Cards.Where(card => !string.Equals(card.Id, current?.Id, StringComparison.OrdinalIgnoreCase)))
        {
            var row = Row(
                AdventuresPage.RowName(card.Title),
                AdventuresPage.RowSecondary(card.Tone is { Length: > 0 } tone ? $"{card.Genre} · {tone}" : card.Genre),
                AdventuresPage.Text(card.Blurb, TypeScale.Body));

            var crumb = new NavCrumb(ReadPrefix + card.Id, card.Title);
            row.PointerPressed += (_, _) => _nav.Drill(crumb);
            _list.Children.Add(row);
        }
    }

    /// <summary>The Story on checkbox for the Commander's current story.</summary>
    internal static Control StoryOnBox(AdventureSurface surface, StoryDirector director, Story story)
    {
        var (box, _) = LabeledCheckBox.Build("Story on");
        box.IsChecked = !story.IsOff;
        box.IsCheckedChanged += (_, _) => director.SetOn(surface.Commander(), box.IsChecked == true, surface.Now());
        return box;
    }

    private Control CurrentCard(Story story)
    {
        var commander = _surface.Commander();
        var buttons = AdventuresPage.Buttons();
        buttons.Margin = new Thickness(0, 6, 0, 0);

        if (story.CurrentChapter is { } key && _surface.Book.Store.Find(commander, key) is { } chapter)
        {
            buttons.Children.Add(Act("Read the chapter", () => _nav.Drill(new NavCrumb(AdventuresPage.ReadPrefix + key, chapter.Name))));
        }

        if (story.State == StoryState.Paused)
        {
            buttons.Children.Add(Act("Resume", () => Run(
                "Resuming…", () => _director.ResumeAsync(commander, _surface.Now(), CancellationToken.None))));
        }
        else if (_director.WriteFailed(commander) && !_director.IsWriting(commander))
        {
            buttons.Children.Add(Act("Write it again", () => Run(
                "Writing…", () => _director.WriteNextAsync(commander, _surface.Now(), CancellationToken.None))));
        }

        buttons.Children.Add(StoryOnBox(_surface, _director, story));

        buttons.Children.Add(Act("Abandon", () => Confirm(
            "story.abandon",
            "Abandon",
            $"Abandon {story.Title}?",
            "The story ends here. Its chapters stay on the Adventures page, and any Guardian core it held back is available at once.",
            "Abandon it",
            () =>
            {
                if (_director.Abandon(commander, _surface.Now()) is { } refusal)
                {
                    _status.Fail(refusal);
                }
            }), destructive: true));

        var row = Row(
            AdventuresPage.RowName(story.Title),
            AdventuresPage.RowSecondary(Standing(story, commander)),
            buttons);

        row.Margin = new Thickness(0, 0, 0, 10);
        row.PointerPressed += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + story.Id, story.Title));
        return row;
    }

    private string Standing(Story story, string? commander)
    {
        var told = story.Chapters.Count;
        var next = (told + 1).ToString(CultureInfo.InvariantCulture);

        if (_director.IsWriting(commander))
        {
            return $"Your story — writing chapter {next}…";
        }

        if (_director.WriteFailed(commander))
        {
            return $"Your story — chapter {next} could not be written.";
        }

        if (story.IsOff)
        {
            return "Your story — switched off. No beats, nudges or clues until you switch it on.";
        }

        return story.State == StoryState.Paused
            ? $"Your story — paused, chapter {told.ToString(CultureInfo.InvariantCulture)} was abandoned."
            : $"Your story — chapter {told.ToString(CultureInfo.InvariantCulture)} under way.";
    }

    private Control BuildReading(string id)
    {
        var page = new StackPanel { Spacing = 8, Margin = new Thickness(14) };

        if (_director.Catalog.Find(id) is not { } card)
        {
            page.Children.Add(AdventuresPage.Muted("That story is not on offer."));
            return page;
        }

        var status = new StatusLine();

        page.Children.Add(RoutingKit.Title(card.Title).Row);

        page.Children.Add(AdventuresPage.Muted(card.Genre));

        if (card.Tone is { Length: > 0 } tone)
        {
            page.Children.Add(AdventuresPage.Muted(tone));
        }

        page.Children.Add(AdventuresPage.Text(card.Blurb, TypeScale.Body));
        page.Children.Add(Labelled("In your words", card.InYourWords));
        page.Children.Add(Labelled("The beacon", card.Beacon));

        var commander = _surface.Commander();
        var current = _director.Stories.Current(commander);
        var bar = AdventuresPage.Buttons();
        bar.Margin = new Thickness(0, 10, 0, 0);

        var withoutOdyssey = _director.WithoutOdyssey(commander);

        if (withoutOdyssey)
        {
            page.Children.Add(AdventuresPage.Text(StoryDirector.NeedsOdyssey, TypeScale.Body));
        }

        if (current is null)
        {
            var pick = Act("Pick", () => Start(card, switching: false, status));
            pick.IsEnabled = !withoutOdyssey;
            bar.Children.Add(pick);
        }
        else if (!string.Equals(current.Id, card.Id, StringComparison.OrdinalIgnoreCase))
        {
            var switchTo = Act("Switch", () => Confirm(
                "story.switch",
                "Switch",
                $"Switch from {current.Title} to {card.Title}?",
                $"{current.Title} is abandoned and its chapters stay on the Adventures page. Your Backstory becomes this "
                + "story's words, and the Guardian cores wait for its own beacon scan.",
                "Switch",
                () => Start(card, switching: true, status)));
            switchTo.IsEnabled = !withoutOdyssey;
            bar.Children.Add(switchTo);
        }
        else
        {
            page.Children.Add(AdventuresPage.Muted("This is your story."));
        }

        page.Children.Add(bar);
        page.Children.Add(status);

        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private void Start(StoryCard card, bool switching, StatusLine status)
    {
        if (!_surface.ModelAvailable() || !_surface.GalaxySearchOn())
        {
            status.Fail(!_surface.ModelAvailable()
                ? "A story needs a language model to write its chapters, and none is configured."
                : "A story needs galaxy search, so its chapters' places can be checked. It is off in Settings.");
            return;
        }

        var commander = _surface.Commander();
        var now = _surface.Now();

        status.Say("Writing chapter one…");

        _ = Task.Run(async () =>
        {
            var refusal = switching
                ? await _director.SwitchAsync(commander, card.Id, now, CancellationToken.None).ConfigureAwait(false)
                : await _director.PickAsync(commander, card.Id, now, CancellationToken.None).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                if (refusal is not null)
                {
                    status.Fail(refusal);
                    _surface.Say(refusal);
                    return;
                }

                status.Say("Chapter one has begun.");

                if (_director.Stories.Current(commander)?.CurrentChapter is { } key
                    && _surface.Book.Store.Find(commander, key) is { } chapter)
                {
                    _nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + key, chapter.Name));
                }
            });
        });
    }

    private void Run(string doing, Func<Task<string?>> act)
    {
        _status.Say(doing);

        _ = Task.Run(async () =>
        {
            var refusal = await act().ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                if (refusal is null)
                {
                    _status.Clear();
                }
                else
                {
                    _status.Fail(refusal);
                }
            });
        });
    }

    private void Confirm(string key, string word, string title, string context, string yes, Action act) => _prompts.Choose(
        new ChoiceRequest(
            key,
            word,
            title,
            context,
            [new ChoiceOption("keep", "Keep going"), new ChoiceOption("yes", yes)],
            null,
            ChoiceSurface.Layer),
        option =>
        {
            if (option.Key == "yes")
            {
                act();
            }
        });

    private static Border Row(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 2 };

        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return ListRow.Dress(new Border
        {
            Child = stack,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        });
    }

    private static Control Labelled(string label, string? text)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 0), IsVisible = !string.IsNullOrWhiteSpace(text) };
        stack.Children.Add(AdventuresPage.Text(label, TypeScale.Small, ThemeManager.GreyKey));
        stack.Children.Add(AdventuresPage.Text(text ?? string.Empty, TypeScale.Body));
        return stack;
    }

    private static Button Act(string label, Action act, bool destructive = false)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        button.Classes.Set("destructive", destructive);
        button.Click += (_, _) => act();
        return button;
    }
}
