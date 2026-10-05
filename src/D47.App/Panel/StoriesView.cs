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
    private readonly StoryRatingClient? _ratings;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly StatusLine _status = new();
    private readonly StoryFilterMemory? _memory;
    private readonly StackPanel _filterBar = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 10), IsVisible = false };
    private readonly TextBlock _count;
    private readonly IChoiceControl _level;
    private readonly IChoiceControl _compare;
    private readonly IChoiceControl _length;
    private readonly Control _lengthView;
    private readonly IChoiceControl _minStars;
    private readonly IChoiceControl _sort;
    private readonly StackPanel _ratingFilters = new() { Spacing = 4 };
    private StoryFilter _filter;

    private static readonly string[] LevelLabels = ["Any level", "New", "Mid-range", "Endgame"];
    private static readonly string[] CompareLabels = ["Any length", "At least", "Exactly", "At most"];
    private static readonly string[] RatingLabels = ["Any rating", "4 stars & up", "3 stars & up", "2 stars & up", "1 star & up"];
    private static readonly string[] SortLabels = ["Catalogue order", "Highest rated"];

    public StoriesView(AdventureSurface surface, StoryDirector director, PanelNavigator nav, PanelPrompts prompts)
    {
        _surface = surface;
        _director = director;
        _nav = nav;
        _prompts = prompts;
        _downloads = surface.Downloads;
        _ratings = surface.Ratings;
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

        var ratingView = Choice.Build(RatingLabels, _filter.MinStars is { } least ? Math.Clamp(5 - least, 0, RatingLabels.Length - 1) : 0);
        var sortView = Choice.Build(SortLabels, (int)_filter.Sort);

        (_minStars, _sort) = (ratingView.Choice, sortView.Choice);
        Avalonia.Automation.AutomationProperties.SetName(ratingView.View, "Rating");
        Avalonia.Automation.AutomationProperties.SetName(sortView.View, "Sort");
        ratingView.View.HorizontalAlignment = HorizontalAlignment.Left;
        sortView.View.HorizontalAlignment = HorizontalAlignment.Left;
        _minStars.SelectionChanged += (_, _) => FilterChanged();
        _sort.SelectionChanged += (_, _) => FilterChanged();
        _ratingFilters.Children.Add(ratingView.View);
        _ratingFilters.Children.Add(sortView.View);

        _count = AdventuresPage.Muted(string.Empty);
        _filterBar.Children.Add(levelView.View);
        _filterBar.Children.Add(compareView.View);
        _filterBar.Children.Add(_lengthView);
        _filterBar.Children.Add(_ratingFilters);
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

        if (_ratings is not null)
        {
            _ratings.Changed += OnChanged;
            OpenRatings(_ratings, _status);
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

        if (_ratings is not null)
        {
            _ratings.Changed -= OnChanged;
        }
    }

    /// <summary>Fetches the averages and resends pending votes, and shows a failure on <paramref name="status"/>.</summary>
    private static void OpenRatings(StoryRatingClient ratings, StatusLine status) => _ = Task.Run(async () =>
    {
        if (await ratings.Open().ConfigureAwait(false) is { } failure)
        {
            Dispatcher.UIThread.Post(() => status.Fail(failure));
        }
    });

    /// <summary>The ratings shown, or null when the Story ratings setting is off.</summary>
    private StoryRatings? Ratings => _ratings is { Enabled: true } client ? client.Ratings : null;

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
        var ratings = Ratings;
        var filter = ratings is null ? _filter with { MinStars = null, Sort = StorySort.Catalogue } : _filter;
        var shown = filter.Order(others.Where(card => filter.Matches(card, ratings ?? StoryRatings.Empty)), ratings ?? StoryRatings.Empty).ToList();

        _filterBar.IsVisible = offered.Count > 0;
        _ratingFilters.IsVisible = ratings is not null;
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
            var parts = new List<Control>
            {
                AdventuresPage.RowName(card.Title),
                AdventuresPage.RowSecondary($"{card.Pacing.Name} · {card.LevelName} · {card.CoreName}{(finished ? " · Finished" : string.Empty)}"),
            };

            if (ratings is not null)
            {
                parts.Add(Average(ratings.Get(card.Id)));
            }

            if (CastStrip.For(card, _director.Gender(), _surface.Pictures) is { } cast)
            {
                parts.Add(cast);
            }

            parts.Add(AdventuresPage.Text(card.Blurb, TypeScale.Body));

            var row = Row([.. parts]);

            var crumb = new NavCrumb(ReadPrefix + card.Id, card.Title) { Level = ReadPrefix };
            row.PointerPressed += (_, _) => _nav.Drill(crumb);
            _list.Children.Add(row);
        }
    }

    private void FilterChanged()
    {
        var compare = (StoryLengthCompare)Math.Max(0, _compare.SelectedIndex);
        var level = _level.SelectedIndex > 0 ? StoryCard.Levels[_level.SelectedIndex - 1] : null;
        var length = _length.SelectedIndex >= 0 ? StoryPacing.All[_length.SelectedIndex].Key : null;

        var minStars = _minStars.SelectedIndex > 0 ? 5 - _minStars.SelectedIndex : (int?)null;
        var sort = (StorySort)Math.Max(0, _sort.SelectedIndex);

        _filter = new StoryFilter(level, compare, length, minStars, sort);
        _lengthView.IsVisible = compare != StoryLengthCompare.Any;
        _memory?.Remember(_filter);
        Rebuild();
    }

    private void ClearFilters()
    {
        _level.SelectedIndex = 0;
        _compare.SelectedIndex = 0;
        _minStars.SelectedIndex = 0;
        _sort.SelectedIndex = 0;
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
            done("The story is not waiting on an objective.");
            return;
        }

        var activity = D47.Core.Adventures.RefusedActivities.Phrase(beat.Trigger.Kind, beat.Trigger.MissionFamily);

        prompts.Choose(
            new ChoiceRequest(
                "story.refuse",
                "Not for me",
                "Write a different objective?",
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
                        ? "A different objective needs a language model to write it, and none is configured."
                        : "A different objective needs galaxy search, so its places can be checked. It is off in Settings.");
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
                () => _status.Say("Writing a different objective…"),
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
        row.PointerPressed += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + story.Id, story.Title) { Level = ReadPrefix });
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
            return "Your story — writing a different objective…";
        }

        if (_director.WriteFailed(commander))
        {
            return $"Your story — chapter {next} could not be written.";
        }

        if (story.IsOff)
        {
            return "Your story — switched off. No objectives, nudges or clues until you switch it on.";
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
        var body = new ContentControl { Content = ReadingBody(card, status) };

        page.Children.Add(body);
        page.Children.Add(status);
        FetchOnView(card, body, status);

        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <summary>Fetches the hidden layer of a story whose page is open, then redraws the page if it is still the one shown.</summary>
    private void FetchOnView(StoryCard card, ContentControl body, StatusLine status)
    {
        if (_downloads is not { Enabled: true } downloads || _director.Catalog.Secret(card.Id) is not null)
        {
            return;
        }

        var key = ReadPrefix + card.Id;

        _ = Task.Run(async () =>
        {
            if (!await downloads.FetchStory(card.Id).ConfigureAwait(false) || _director.Catalog.Secret(card.Id) is null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_nav.TrailOf(PanelTab.Stories) is [.., var open] && open.Key == key)
                {
                    body.Content = ReadingBody(_director.Catalog.Find(card.Id) ?? card, status);
                }
            });
        });
    }

    /// <summary>Everything on a story's page above its status line.</summary>
    private StackPanel ReadingBody(StoryCard card, StatusLine status)
    {
        var page = new StackPanel { Spacing = 8 };

        page.Children.Add(RoutingKit.Title(card.Title).Row);

        if (_ratings is { Enabled: true } client)
        {
            page.Children.Add(RatingSection(client, card.Id, status));
        }

        page.Children.Add(AdventuresPage.Muted($"{card.Pacing.Name} · {card.LevelGuideline}"));

        if (card.Tone is { Length: > 0 } tone)
        {
            page.Children.Add(AdventuresPage.Muted(tone));
        }

        page.Children.Add(AdventuresPage.Muted($"Core: {card.CoreName}"));
        page.Children.Add(AdventuresPage.Text(card.Blurb, TypeScale.Body));
        page.Children.Add(Labelled("In your words", card.InYourWords));
        page.Children.Add(Labelled("The beacon", card.Beacon));

        var cast = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        ShowCast(cast, card.Id);
        page.Children.Add(cast);

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
            page.Children.Add(GenderChoice(() =>
            {
                gated?.IsEnabled = !withoutOdyssey && _director.VoicesMissing(card.Id).Count == 0;
                ShowCast(cast, card.Id);
            }));
        }

        var missing = _director.VoicesMissing(card.Id);

        if (missing.Count > 0)
        {
            page.Children.Add(AdventuresPage.Text("Before this story can start, its voices need:", TypeScale.Small, ThemeManager.GreyKey));

            foreach (var need in missing)
            {
                page.Children.Add(AdventuresPage.Text(need, TypeScale.Body));
            }
        }

        if (current is null)
        {
            var pick = Act("Pick", () => Start(card, switching: false, status, gated));
            pick.IsEnabled = !withoutOdyssey && GenderReady(card) && missing.Count == 0;
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
            switchTo.IsEnabled = !withoutOdyssey && GenderReady(card) && missing.Count == 0;
            bar.Children.Add(gated = switchTo);
        }
        else
        {
            page.Children.Add(AdventuresPage.Muted("This is your story."));
        }

        page.Children.Add(bar);

        return page;
    }

    private static Control Average(StoryRating? rating) => StarRating.Summary(rating);

    /// <summary>
    /// A story's average, and the Commander's own stars once they have picked it; redrawn in place while shown, so the
    /// stars keep keyboard focus across a vote.
    /// </summary>
    private StackPanel RatingSection(StoryRatingClient client, string id, StatusLine status)
    {
        var section = new StackPanel { Spacing = 4 };
        var average = new ContentControl();
        var label = AdventuresPage.Text("Your rating", TypeScale.Small, ThemeManager.GreyKey);
        var mine = StarRating.Interactive(null);
        var pickFirst = AdventuresPage.Muted("Pick this story to rate it.");
        var pending = AdventuresPage.Muted("Not sent yet; it will be sent next time the Stories page opens.");

        label.Margin = new Thickness(0, 4, 0, 0);
        section.Children.Add(average);
        section.Children.Add(label);
        section.Children.Add(mine);
        section.Children.Add(pending);
        section.Children.Add(pickFirst);

        void Refresh()
        {
            var story = _director.Stories.Find(_surface.Commander(), id);

            average.Content = Average(client.Ratings.Get(id));
            label.IsVisible = mine.IsVisible = story is not null;
            pickFirst.IsVisible = story is null;
            pending.IsVisible = story is { RatingPending: true };
            mine.Value = story?.Rating ?? 0;
        }

        void Changed() => Dispatcher.UIThread.Post(Refresh);

        mine.Rated += stars =>
        {
            var commander = _surface.Commander();

            _ = Task.Run(async () =>
            {
                var sent = await (stars is { } given ? client.Rate(commander, id, given) : client.Clear(commander, id)).ConfigureAwait(false);

                if (!sent)
                {
                    Dispatcher.UIThread.Post(() => status.Fail(StoryRatingClient.SendFailed));
                }
            });
        };

        section.AttachedToVisualTree += (_, _) =>
        {
            client.Changed += Changed;
            _director.Stories.Changed += Changed;
            Refresh();
        };
        section.DetachedFromVisualTree += (_, _) =>
        {
            client.Changed -= Changed;
            _director.Stories.Changed -= Changed;
        };

        Refresh();
        return section;
    }

    /// <summary>The Cast section: each primary member with its picture, name and voice, and the controls that change them.</summary>
    private void ShowCast(StackPanel holder, string storyId)
    {
        holder.Children.Clear();

        var members = _director.PrimaryCast(storyId);

        if (members.Count == 0)
        {
            return;
        }

        holder.Children.Add(AdventuresPage.Text("Cast", TypeScale.Small, ThemeManager.GreyKey));

        foreach (var member in members)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(AdventuresPage.Text(member.Shown.Name, TypeScale.Body));
            row.Children.Add(AdventuresPage.Text(CastVoiceChooser.Describe(member), TypeScale.Small, ThemeManager.GreyKey));

            if (member.Speaks.Key is { } chosen && _surface.CastVoices?.Failure(chosen) is { } failed)
            {
                row.Children.Add(AdventuresPage.Text($"The chosen voice failed, so the story's own spoke instead: {failed}", TypeScale.Small));
            }

            var buttons = new List<Control>();

            if (_surface.CastVoices is { } voices)
            {
                buttons.Add(Act("Play sample", () => voices.PlaySample(member.Shown.Name, member.Speaks)));
                buttons.Add(Act("Change voice", () => CastVoiceChooser.Open(_prompts, voices, member.Key, () => ShowCast(holder, storyId))));
            }

            if (_surface.Pictures is { } pictures)
            {
                var pictured = new StackPanel { Spacing = 6 };
                CastPicturePanel.Show(this, pictured, pictures, member.Shown.Picture, [.. buttons]);
                row.Children.Add(pictured);
            }
            else
            {
                var bar = AdventuresPage.Buttons();

                foreach (var button in buttons)
                {
                    bar.Children.Add(button);
                }

                row.Children.Add(bar);
            }

            holder.Children.Add(row);
        }
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
