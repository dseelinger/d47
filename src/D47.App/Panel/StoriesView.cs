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
    private readonly StoryDownloader? _downloads;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly StatusLine _status = new();
    private readonly StoryFilterMemory? _memory;
    private readonly StackPanel _filterBar = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 10), IsVisible = false };
    private readonly TextBlock _count;
    private readonly IChoiceControl _level;
    private readonly IChoiceControl _compare;
    private readonly IChoiceControl _length;
    private readonly Control _lengthView;
    private StoryFilter _filter;

    private static readonly string[] LevelLabels = ["Any level", "New", "Mid-range", "Endgame"];
    private static readonly string[] CompareLabels = ["Any length", "At least", "Exactly", "At most"];

    public StoriesView(AdventureSurface surface, StoryDirector director, PanelNavigator nav, PanelPrompts prompts)
    {
        _surface = surface;
        _director = director;
        _nav = nav;
        _prompts = prompts;
        _downloads = surface.Downloads;
        _memory = surface.StoryFilters;
        _filter = _memory?.Filter ?? new StoryFilter();

        var lengths = StoryPacing.All.Select(pacing => pacing.Name).ToList();
        var levelView = Choice.Build(LevelLabels, _filter.Level is null ? 0 : Math.Max(0, StoryCard.Levels.ToList().IndexOf(_filter.Level) + 1));
        var compareView = Choice.Build(CompareLabels, (int)_filter.Compare);
        var lengthIndex = StoryPacing.Find(_filter.Length) is { } known ? StoryPacing.All.ToList().IndexOf(known) : StoryPacing.All.ToList().IndexOf(StoryPacing.OneMonth);
        var lengthChoice = Choice.Build(lengths, lengthIndex, alwaysStepper: true);

        (_level, _compare, _length, _lengthView) = (levelView.Choice, compareView.Choice, lengthChoice.Choice, lengthChoice.View);
        Avalonia.Automation.AutomationProperties.SetName(levelView.View, "Level");
        Avalonia.Automation.AutomationProperties.SetName(compareView.View, "Length");
        Avalonia.Automation.AutomationProperties.SetName(lengthChoice.View, "Story length");
        levelView.View.HorizontalAlignment = HorizontalAlignment.Left;
        compareView.View.HorizontalAlignment = HorizontalAlignment.Left;
        lengthChoice.View.HorizontalAlignment = HorizontalAlignment.Left;
        _lengthView.IsVisible = _filter.Compare != StoryLengthCompare.Any;

        _level.SelectionChanged += (_, _) => FilterChanged();
        _compare.SelectionChanged += (_, _) => FilterChanged();
        _length.SelectionChanged += (_, _) => FilterChanged();

        _count = AdventuresPage.Muted(string.Empty);
        _filterBar.Children.Add(levelView.View);
        _filterBar.Children.Add(compareView.View);
        _filterBar.Children.Add(_lengthView);
        _filterBar.Children.Add(_count);

        var root = new DockPanel { Margin = new Thickness(14) };
        var (title, _) = RoutingKit.Title("Stories");
        var intro = AdventuresPage.Muted(
            "Stock stories that run from three days to a year, a chapter at a time. Picking one makes its words your Backstory and "
            + "has the ship's AI write chapter one. Act one ends at a Guardian beacon once your ship can reach it. When a chapter finishes, the next "
            + "is written and begins. While a story runs, the Guardian cores wait for its beacon scan. Pause or abandon "
            + "the story to have them back at once.");
        intro.Margin = new Thickness(0, 0, 0, 10);

        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(intro, Dock.Top);
        DockPanel.SetDock(_status, Dock.Top);
        DockPanel.SetDock(_filterBar, Dock.Top);
        root.Children.Add(title);
        root.Children.Add(intro);
        root.Children.Add(_filterBar);
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

        if (_downloads is not null)
        {
            _downloads.Landed += OnChanged;
            _ = _downloads.AskForList();
        }

        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _director.Stories.Changed -= OnChanged;
        _director.WritingChanged -= OnChanged;
        _director.OdysseyChanged -= OnChanged;

        if (_downloads is not null)
        {
            _downloads.Landed -= OnChanged;
        }
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

        var offered = _director.Catalog.Cards.Where(card => _downloads is not { Enabled: false } || _director.Catalog.Secret(card.Id) is not null).ToList();

        if (current is null && offered.Count == 0)
        {
            _list.Children.Add(AdventuresPage.Muted("No stories yet."));
        }

        var others = offered.Where(card => !string.Equals(card.Id, current?.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var shown = others.Where(_filter.Matches).ToList();

        _filterBar.IsVisible = offered.Count > 0;
        _count.IsVisible = shown.Count > 0 && shown.Count < others.Count;
        _count.Text = string.Create(CultureInfo.InvariantCulture, $"{shown.Count} of {others.Count} stories.");

        if (shown.Count == 0 && others.Count > 0)
        {
            _list.Children.Add(AdventuresPage.Muted("No stories match these filters."));
            _list.Children.Add(Act("Clear filters", ClearFilters));
        }

        foreach (var card in shown)
        {
            var finished = _director.Stories.Find(_surface.Commander(), card.Id) is { State: StoryState.Finished };
            var row = Row(
                AdventuresPage.RowName(card.Title),
                AdventuresPage.RowSecondary($"{card.Pacing.Name} · {card.LevelName} · {card.CoreName}{(finished ? " · Finished" : string.Empty)}"),
                AdventuresPage.Text(card.Blurb, TypeScale.Body));

            var crumb = new NavCrumb(ReadPrefix + card.Id, card.Title);
            row.PointerPressed += (_, _) => _nav.Drill(crumb);
            _list.Children.Add(row);
        }
    }

    private void FilterChanged()
    {
        var compare = (StoryLengthCompare)Math.Max(0, _compare.SelectedIndex);
        var level = _level.SelectedIndex > 0 ? StoryCard.Levels[_level.SelectedIndex - 1] : null;
        var length = _length.SelectedIndex >= 0 ? StoryPacing.All[_length.SelectedIndex].Key : null;

        _filter = new StoryFilter(level, compare, length);
        _lengthView.IsVisible = compare != StoryLengthCompare.Any;
        _memory?.Remember(_filter);
        Rebuild();
    }

    private void ClearFilters()
    {
        _level.SelectedIndex = 0;
        _compare.SelectedIndex = 0;
        FilterChanged();
    }

    /// <summary>The Story on checkbox for the Commander's current story.</summary>
    internal static Control StoryOnBox(AdventureSurface surface, StoryDirector director, Story story)
    {
        var (box, _) = LabeledCheckBox.Build("Story on");
        box.IsChecked = !story.IsOff;
        box.IsCheckedChanged += (_, _) => director.SetOn(surface.Commander(), box.IsChecked == true, surface.Now());
        return box;
    }

    /// <summary>
    /// Asks whether to replace the beat the current story's chapter is waiting on, and on yes has a different one written.
    /// <paramref name="doing"/> runs when the write starts; <paramref name="done"/> gets its refusal, or null.
    /// </summary>
    internal static void NotForMe(AdventureSurface surface, PanelPrompts prompts, Action doing, Action<string?> done)
    {
        if (surface.Stories is not { } director || director.RefusableBeat(surface.Commander()) is not { } beat)
        {
            done("The story is not waiting on a beat.");
            return;
        }

        var activity = D47.Core.Adventures.RefusedActivities.Phrase(beat.Trigger.Kind, beat.Trigger.MissionFamily);

        prompts.Choose(
            new ChoiceRequest(
                "story.refuse",
                "Not for me",
                "Write a different beat?",
                activity is null ? string.Empty : $"This story won't ask you to {activity} again.",
                [new ChoiceOption("keep", "Keep it"), new ChoiceOption("yes", "Write a different one")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "yes")
                {
                    return;
                }

                if (!surface.ModelAvailable() || !surface.GalaxySearchOn())
                {
                    done(!surface.ModelAvailable()
                        ? "A different beat needs a language model to write it, and none is configured."
                        : "A different beat needs galaxy search, so its places can be checked. It is off in Settings.");
                    return;
                }

                doing();

                _ = Task.Run(async () =>
                {
                    var refusal = await director.RefuseBeatAsync(surface.Commander(), CancellationToken.None).ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() => done(refusal));
                });
            });
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

        if (_director.RefusableBeat(commander) is not null && !_director.IsRewriting(commander))
        {
            buttons.Children.Add(Act("Not for me", () => NotForMe(
                _surface,
                _prompts,
                () => _status.Say("Writing a different beat…"),
                refusal =>
                {
                    if (refusal is null)
                    {
                        _status.Clear();
                    }
                    else
                    {
                        _status.Fail(refusal);
                    }
                })));
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
            "The story ends here. Its chapters leave the Adventures page, and any Guardian core it held back is available at once.",
            "Abandon it",
            () =>
            {
                if (_director.Abandon(commander, _surface.Now()) is { } refusal)
                {
                    _status.Fail(refusal);
                }
            }), destructive: true));

        var row = story.Refused.Count == 0
            ? Row(AdventuresPage.RowName(story.Title), AdventuresPage.RowSecondary(Standing(story, commander)), buttons)
            : Row(
                AdventuresPage.RowName(story.Title),
                AdventuresPage.RowSecondary(Standing(story, commander)),
                AdventuresPage.Muted(RefusedLine(story)),
                buttons);

        row.Margin = new Thickness(0, 0, 0, 10);
        row.PointerPressed += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + story.Id, story.Title));
        return row;
    }

    /// <summary>The activities the Commander refused in this story, in words.</summary>
    internal static string RefusedLine(Story story) =>
        "Refused: " + string.Join(", ", story.Refused.Select(D47.Core.Adventures.RefusedActivities.Phrase)) + ".";

    private string Standing(Story story, string? commander)
    {
        var told = story.Chapters.Count;
        var next = (told + 1).ToString(CultureInfo.InvariantCulture);

        if (_director.IsWriting(commander))
        {
            return $"Your story — writing chapter {next}…";
        }

        if (_director.IsRewriting(commander))
        {
            return "Your story — writing a different beat…";
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

        page.Children.Add(AdventuresPage.Muted($"{card.Pacing.Name} · {card.LevelGuideline}"));

        if (card.Tone is { Length: > 0 } tone)
        {
            page.Children.Add(AdventuresPage.Muted(tone));
        }

        page.Children.Add(AdventuresPage.Muted($"Core: {card.CoreName}"));
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

        Button? gated = null;

        if (_director.NeedsGenderFor(card.Id))
        {
            page.Children.Add(GenderChoice(() => gated?.IsEnabled = !withoutOdyssey));
        }

        if (current is null)
        {
            var pick = Act("Pick", () => Start(card, switching: false, status, gated));
            pick.IsEnabled = !withoutOdyssey && GenderReady(card);
            bar.Children.Add(gated = pick);
        }
        else if (!string.Equals(current.Id, card.Id, StringComparison.OrdinalIgnoreCase))
        {
            var switchTo = Act("Switch", () => Confirm(
                "story.switch",
                "Switch",
                $"Switch from {current.Title} to {card.Title}?",
                $"{current.Title} is abandoned and its chapters leave the Adventures page. Your Backstory becomes this "
                + "story's words, and the Guardian cores wait for its own beacon scan.",
                "Switch",
                () => Start(card, switching: true, status, gated)));
            switchTo.IsEnabled = !withoutOdyssey && GenderReady(card);
            bar.Children.Add(gated = switchTo);
        }
        else
        {
            page.Children.Add(AdventuresPage.Muted("This is your story."));
        }

        page.Children.Add(bar);
        page.Children.Add(status);

        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private bool GenderReady(StoryCard card) =>
        !_director.NeedsGenderFor(card.Id) || CommanderGender.IsSet(_director.Gender());

    /// <summary>"Your Commander is: a man / a woman", with the reason Pick waits while it is unset.</summary>
    private StackPanel GenderChoice(Action chosen)
    {
        var genders = new[] { CommanderGender.Man, CommanderGender.Woman };
        var segment = new Segment
        {
            ItemsSource = ["A man", "A woman"],
            SelectedIndex = Array.IndexOf(genders, _director.Gender()),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var why = AdventuresPage.Muted(StoryDirector.NeedsGender);
        why.IsVisible = segment.SelectedIndex < 0;

        Avalonia.Automation.AutomationProperties.SetName(segment, "Your Commander is");
        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex >= 0)
            {
                _director.SetGender(genders[segment.SelectedIndex]);
                why.IsVisible = false;
                chosen();
            }
        };

        var stack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        stack.Children.Add(AdventuresPage.Text("Your Commander is", TypeScale.Small, ThemeManager.GreyKey));
        stack.Children.Add(segment);
        stack.Children.Add(why);
        return stack;
    }

    private void Start(StoryCard card, bool switching, StatusLine status, Button? button)
    {
        if (!_surface.ModelAvailable() || !_surface.GalaxySearchOn())
        {
            status.Fail(!_surface.ModelAvailable()
                ? "A story needs a language model to write its chapters, and none is configured."
                : "A story needs galaxy search, so its chapters' places can be checked. It is off in Settings.");
            return;
        }

        if (_downloads is not null && _director.Catalog.Secret(card.Id) is null)
        {
            Download(card, status, button, () => Begin(card, switching, status));
            return;
        }

        Begin(card, switching, status);
    }

    /// <summary>Fetches the story's files with the button reading Downloading, then runs <paramref name="then"/>; on a failure nothing starts.</summary>
    private void Download(StoryCard card, StatusLine status, Button? button, Action then)
    {
        var label = button?.Content;

        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "Downloading";
        }

        status.Say("Downloading…");

        _ = Task.Run(async () =>
        {
            var landed = await _downloads!.FetchStory(card.Id).ConfigureAwait(false) && _director.Catalog.Secret(card.Id) is not null;

            Dispatcher.UIThread.Post(() =>
            {
                if (button is not null)
                {
                    button.Content = label;
                    button.IsEnabled = true;
                }

                if (landed)
                {
                    then();
                }
                else
                {
                    status.Fail(DownloadFailed);
                }
            });
        });
    }

    internal const string DownloadFailed = "The story could not be downloaded. Check your connection and pick it again.";

    private void Begin(StoryCard card, bool switching, StatusLine status)
    {
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
