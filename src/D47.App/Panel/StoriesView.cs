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
public sealed class StoriesView : UserControl, IPageSummary
{
    public const string RootKey = "stories";
    public const string PageName = "Catalogue";

    /// <summary>The American spelling, which is what the transcriber writes.</summary>
    private static readonly string[] SpokenNames = ["catalog"];

    public static NavCrumb Crumb => new(RootKey, PageName) { Spoken = SpokenNames };

    public const string ReadPrefix = "story.read.";

    public string Summary => "Stock stories, three days to a year, told a chapter at a time.";

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    private readonly AdventureSurface _surface;
    private readonly StoryDirector _director;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly StoryDownloader? _downloads;
    private readonly StoryRatingClient? _ratings;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly StatusLine _status = new();
    private readonly StoryFilterMemory? _memory;
    private readonly CatalogueFilters _filters;
    private StoryFilter _filter;

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
        _filters = new CatalogueFilters(_filter) { IsVisible = false };
        _filters.Changed += (_, _) => FilterChanged();

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(_filters, Dock.Top);
        DockPanel.SetDock(_status, Dock.Top);
        root.Children.Add(_filters);
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
        _surface.Book.Store.Changed += OnChanged;
        _director.WritingChanged += OnChanged;
        _director.WritingAgain += OnWritingAgain;
        _director.OdysseyChanged += OnChanged;

        if (_surface.Switched is { } switched)
        {
            switched.Changed += OnSwitched;
        }

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
        _surface.Book.Store.Changed -= OnChanged;
        _director.WritingChanged -= OnChanged;
        _director.WritingAgain -= OnWritingAgain;
        _director.OdysseyChanged -= OnChanged;

        if (_surface.Switched is { } switched)
        {
            switched.Changed -= OnSwitched;
        }

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

    private void OnWritingAgain(string line) => Dispatcher.UIThread.Post(() => _status.Say(line));

    private void OnSwitched() => Dispatcher.UIThread.Post(() =>
    {
        _status.Clear();
        Rebuild();
    });

    internal const string CommanderChangedRefusal = "The Commander changed. Nothing was done.";

    /// <summary>Runs <paramref name="act"/> if <paramref name="drawnFor"/> is still the shown Commander; otherwise says nothing was done.</summary>
    private bool IfShown(string? drawnFor, StatusLine status, Action act)
    {
        if (!string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
        {
            status.Fail(CommanderChangedRefusal);
            return false;
        }

        act();
        return true;
    }

    internal const string BeaconAgainLabel = "Fly to a Guardian beacon again in my next story";
    internal const string BeaconAgainNote =
        "Your next story of a month or longer opens after the beacon you have scanned. Tick this to fly to one again.";

    /// <summary>The checkbox that forgets the recorded scan for the next pick; null while no scan is on record and the box is unticked.</summary>
    private StackPanel? BeaconAgain()
    {
        var commander = _surface.Commander();
        var forgotten = _director.Stories.BeaconForgotten(commander);

        if (!forgotten && !_director.HasScanOnRecord(commander))
        {
            return null;
        }

        var (box, _) = LabeledCheckBox.Build(BeaconAgainLabel);
        box.Name = "BeaconAgain";
        box.IsChecked = forgotten;
        box.IsCheckedChanged += (_, _) => IfShown(commander, _status, () => _director.Stories.SetBeaconForgotten(commander, box.IsChecked == true));

        var stack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(box);
        stack.Children.Add(AdventuresPage.Muted(BeaconAgainNote));

        return stack;
    }

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

        if (BeaconAgain() is { } beaconAgain)
        {
            _list.Children.Add(beaconAgain);
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

        _filters.IsVisible = offered.Count > 0;
        _filters.RatingsShown = ratings is not null;
        _filters.ShowResults(shown.Count, others.Count, !filter.IsDefault);

        if (shown.Count == 0 && others.Count > 0)
        {
            _list.Children.Add(AdventuresPage.Muted("No stories match these filters."));
        }

        var grid = new CardGrid();

        foreach (var card in shown)
        {
            grid.Children.Add(CardFor(card, ratings));
        }

        if (grid.Children.Count > 0)
        {
            _list.Children.Add(grid);
        }
    }

    private Border CardFor(StoryCard card, StoryRatings? ratings)
    {
        var finished = _director.Stories.Find(_surface.Commander(), card.Id) is { State: StoryState.Finished };
        var parts = new List<Control>();

        if (CastStrip.For(card, _director.Gender(), _surface.Pictures) is { } cast)
        {
            parts.Add(cast);
        }

        var name = AdventuresPage.RowName(card.Title);
        name.FontSize = TypeScale.Subheading;
        name.LetterSpacing = TypeScale.Subheading * 0.03;
        parts.Add(name);
        parts.Add(AdventuresPage.RowSecondary($"{card.Pacing.Name} · {card.LevelName} · {card.CoreName}{(finished ? " · Finished" : string.Empty)}"));

        var blurb = new ClampedText { FullText = card.Blurb, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = TypeScale.Tip };
        AdventuresPage.Themed(blurb, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        parts.Add(blurb);

        if (ratings is not null)
        {
            parts.Add(Average(ratings.Get(card.Id)));
        }

        var row = Row(6, [.. parts]);
        var crumb = new NavCrumb(ReadPrefix + card.Id, card.Title) { Level = ReadPrefix };

        row.PointerPressed += (_, _) => _nav.Drill(crumb);
        return row;
    }

    private void FilterChanged()
    {
        _filter = _filters.Filter;
        _memory?.Remember(_filter);
        Rebuild();
    }

    /// <summary>The Story on checkbox for the Commander's current story.</summary>
    internal static Control StoryOnBox(AdventureSurface surface, StoryDirector director, Story story, string? commander, StatusLine? status = null)
    {
        var (box, _) = LabeledCheckBox.Build("Story on");
        box.IsChecked = !story.IsOff;
        box.IsCheckedChanged += (_, _) =>
        {
            if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
            {
                box.IsChecked = !story.IsOff;
                status?.Fail(CommanderChangedRefusal);
                return;
            }

            director.SetOn(commander, box.IsChecked == true, surface.Now());
        };
        return box;
    }

    /// <summary>
    /// Asks whether to abandon the Commander's current story and on yes abandons it.
    /// <paramref name="done"/> gets the refusal, or null once the story is abandoned; it is not called on Keep going.
    /// </summary>
    internal static void AbandonStory(AdventureSurface surface, PanelPrompts prompts, StoryDirector director, Story story, string? commander, Action<string?> done)
    {
        if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
        {
            done(CommanderChangedRefusal);
            return;
        }

        prompts.Choose(
            new ChoiceRequest(
                "story.abandon",
                "Abandon",
                $"Abandon {story.Title}?",
                "The story ends here. Its chapters leave the list on the Stories tab, any Guardian core it held back is available at once, and your Backstory goes back to the one you had before the story.",
                [new ChoiceOption("keep", "Keep going"), new ChoiceOption("yes", "Abandon it")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "yes")
                {
                    return;
                }

                done(string.Equals(commander, surface.Commander(), StringComparison.Ordinal)
                    ? director.Abandon(commander, surface.Now())
                    : CommanderChangedRefusal);
            });
    }

    /// <summary>
    /// Asks whether to replace the beat the current story's chapter, or the short story <paramref name="adventureKey"/>, is
    /// waiting on, and on yes has a different one written. <paramref name="doing"/> runs when the write starts;
    /// <paramref name="done"/> gets its refusal, or null.
    /// </summary>
    internal static void NotForMe(
        AdventureSurface surface, PanelPrompts prompts, string? commander, Action doing, Action<string?> done, string? adventureKey = null)
    {
        if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
        {
            done(CommanderChangedRefusal);
            return;
        }

        if (surface.Stories is not { } director || RefusableBeat(surface, director, commander, adventureKey) is not { } beat)
        {
            done("The story is not waiting on an objective.");
            return;
        }

        var activity = D47.Core.Adventures.RefusedActivities.Phrase(beat.Trigger.Kind, beat.Trigger.MissionFamily);

        prompts.Choose(
            new ChoiceRequest(
                "story.refuse",
                "Never ask this",
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

                if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
                {
                    done(CommanderChangedRefusal);
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
                    var refusal = adventureKey is null
                        ? await director.RefuseBeatAsync(commander, CancellationToken.None).ConfigureAwait(false)
                        : await director.RefuseAdventureBeatAsync(commander, adventureKey, CancellationToken.None).ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() => done(refusal));
                });
            });
    }

    /// <summary>
    /// Takes the Commander's remark about the current objective, has a replacement written, and offers it to apply, keep or
    /// reword. Returns a refusal when none can be started; <paramref name="done"/> gets the refusal of a later step, or null.
    /// </summary>
    internal static string? ChangeObjective(
        AdventureSurface surface, PanelPrompts prompts, string? commander, Action doing, Action<string?> done,
        IReadOnlyList<D47.Core.Adventures.AdventureRemark>? exchange = null, string? adventureKey = null)
    {
        if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
        {
            return CommanderChangedRefusal;
        }

        if (surface.Stories is not { } director || RefusableBeat(surface, director, commander, adventureKey) is null)
        {
            return "The story is not waiting on an objective.";
        }

        if (director.IsRewriting(commander))
        {
            return "An objective is already being written.";
        }

        if (!surface.ModelAvailable())
        {
            return "A different objective needs a language model to write it, and none is configured.";
        }

        if (!surface.GalaxySearchOn())
        {
            return "A different objective needs galaxy search, so its places can be checked. It is off in Settings.";
        }

        var earlier = exchange ?? [];

        prompts.Enter(
            new EntryRequest(
                "story.objective",
                "Change objective",
                "What would you rather do?",
                "Say it as you would to the core — \"a bounty instead\", \"something closer to home\".",
                string.Empty,
                EntrySurface.Voice,
                value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("Say what you would rather do.") : EntryVerdict.Ok),
            remark =>
            {
                if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
                {
                    done(CommanderChangedRefusal);
                    return;
                }

                doing();

                _ = Task.Run(async () =>
                {
                    var outcome = adventureKey is null
                        ? await director.ReviseObjectiveAsync(commander, remark, earlier, CancellationToken.None).ConfigureAwait(false)
                        : await director.ReviseAdventureObjectiveAsync(commander, adventureKey, remark, earlier, CancellationToken.None).ConfigureAwait(false);

                    Dispatcher.UIThread.Post(() => Propose(surface, prompts, commander, doing, done, outcome, adventureKey));
                });
            });

        return null;
    }

    internal static void Propose(
        AdventureSurface surface, PanelPrompts prompts, string? commander, Action doing, Action<string?> done, ObjectiveProposalOutcome outcome,
        string? adventureKey = null)
    {
        if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
        {
            if (surface.Stories is { } held)
            {
                Discard(held, commander, adventureKey);
            }

            done(CommanderChangedRefusal);
            return;
        }

        if (outcome.Proposal is not { } proposal || surface.Stories is not { } director)
        {
            done(outcome.Refusal ?? "Nothing came back.");
            return;
        }

        done(null);

        if (!string.IsNullOrWhiteSpace(proposal.Reply))
        {
            surface.Say(proposal.Reply);
        }

        prompts.Choose(
            new ChoiceRequest(
                "story.objective.proposal",
                "Change objective",
                "Use this objective?",
                string.IsNullOrWhiteSpace(proposal.Reply) ? proposal.Beats.FirstOrDefault()?.Trigger.Describe() : proposal.Reply,
                [new ChoiceOption("use", "Use this"), new ChoiceOption("keep", "Keep the current one"), new ChoiceOption("again", "Say something else")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (!string.Equals(commander, surface.Commander(), StringComparison.Ordinal))
                {
                    Discard(director, commander, adventureKey);
                    done(CommanderChangedRefusal);
                    return;
                }

                switch (option.Key)
                {
                    case "use":
                        done(adventureKey is null ? director.ApplyObjective(commander) : director.ApplyAdventureObjective(commander, adventureKey));
                        break;

                    case "again":
                        if (ChangeObjective(surface, prompts, commander, doing, done, proposal.Exchange, adventureKey) is { } refusal)
                        {
                            done(refusal);
                        }

                        break;

                    default:
                        Discard(director, commander, adventureKey);
                        break;
                }
            });
    }

    private static D47.Core.Adventures.AdventureBeat? RefusableBeat(AdventureSurface surface, StoryDirector director, string? commander, string? adventureKey) =>
        adventureKey is null
            ? director.RefusableBeat(commander)
            : surface.Book.Standing(commander, adventureKey) is { IsShortStoryRefusable: true } standing ? standing.CurrentStepBeat : null;

    private static void Discard(StoryDirector director, string? commander, string? adventureKey)
    {
        if (adventureKey is null)
        {
            director.DiscardObjective(commander);
        }
        else
        {
            director.DiscardAdventureObjective(commander, adventureKey);
        }
    }

    private Control? ObjectiveGroup(string? commander, string chapterKey)
    {
        if (_director.RefusableBeat(commander) is null
            || _director.IsRewriting(commander)
            || _surface.Book.Standing(commander, chapterKey)?.NextTrigger() is not { } next)
        {
            return null;
        }

        var change = Act("Change objective", () =>
        {
            if (ChangeObjective(_surface, _prompts, commander, () => _status.Say("Writing a different objective…"), Finish) is { } refusal)
            {
                _status.Fail(refusal);
            }
        });

        void Finish(string? refusal)
        {
            if (refusal is null)
            {
                _status.Clear();
            }
            else
            {
                _status.Fail(refusal);
            }
        }

        var refuse = Act("Never ask this", () => NotForMe(
            _surface,
            _prompts,
            commander,
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
            }));

        var group = new StackPanel { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        group.Children.Add(AdventuresPage.Trigger(AdventuresPage.Sentence(next), _surface.State()?.Location?.StarSystem));

        var ship = ShipObjectiveButtons.For(
            _surface, _surface.Book.Standing(commander, chapterKey)?.CurrentTrigger, _nav, _prompts);

        group.Children.Add(ship is null ? AdventuresPage.Buttons(change, refuse) : AdventuresPage.Buttons(ship, change, refuse));
        return group;
    }

    private Control CurrentCard(Story story)
    {
        var commander = _surface.Commander();
        var buttons = AdventuresPage.Buttons();
        buttons.Margin = new Thickness(0, 6, 0, 0);

        if (story.CurrentChapter is { } key && _surface.Book.Store.Find(commander, key) is { } chapter)
        {
            buttons.Children.Add(Act("Read the chapter", () => IfShown(commander, _status, () => _nav.Drill(new NavCrumb(AdventuresPage.ReadPrefix + key, chapter.Name)))));
        }

        if (story.State == StoryState.Paused)
        {
            buttons.Children.Add(Act("Resume", () => IfShown(commander, _status, () => Run(
                "Resuming…", () => _director.ResumeAsync(commander, _surface.Now(), CancellationToken.None)))));
        }
        else if (_director.WriteFailed(commander) && !_director.IsWriting(commander))
        {
            buttons.Children.Add(Act("Write it again", () => IfShown(commander, _status, () => Run(
                "Writing…", () => _director.WriteNextAsync(commander, _surface.Now(), CancellationToken.None)))));
        }

        buttons.Children.Add(StoryOnBox(_surface, _director, story, commander, _status));

        buttons.Children.Add(Act("Abandon", () => AbandonStory(
            _surface,
            _prompts,
            _director,
            story,
            commander,
            refusal =>
            {
                if (refusal is not null)
                {
                    _status.Fail(refusal);
                }
            }), destructive: true));

        var parts = new List<Control> { AdventuresPage.RowName(story.Title), AdventuresPage.RowSecondary(Standing(story, commander)) };

        if (_director.IsWriting(commander) && _director.Progress(commander) is { } writing)
        {
            var progress = new WriteProgressBar("Writing the chapter", () => _director.Progress(commander));
            progress.Show(writing);
            parts.Add(progress);
        }

        if (story.Refused.Count > 0)
        {
            parts.Add(AdventuresPage.Muted(RefusedLine(story)));
        }

        if (story.CurrentChapter is { } chapterKey && ObjectiveGroup(commander, chapterKey) is { } objective)
        {
            parts.Add(objective);
        }

        parts.Add(buttons);

        var row = Row([.. parts]);

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
        var drawnFor = _surface.Commander();
        var body = new ContentControl { Content = ReadingBody(card, status, drawnFor) };

        void Redraw()
        {
            if (string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
            {
                return;
            }

            drawnFor = _surface.Commander();
            status.Clear();
            body.Content = ReadingBody(_director.Catalog.Find(card.Id) ?? card, status, drawnFor);
        }

        void OnSwitched() => Dispatcher.UIThread.Post(Redraw);

        page.Children.Add(body);
        page.Children.Add(status);
        FetchOnView(card, body, status);

        var view = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        view.AttachedToVisualTree += (_, _) =>
        {
            if (_surface.Switched is { } switched)
            {
                switched.Changed += OnSwitched;
            }

            Redraw();
        };
        view.DetachedFromVisualTree += (_, _) =>
        {
            if (_surface.Switched is { } switched)
            {
                switched.Changed -= OnSwitched;
            }
        };

        return view;
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
                    body.Content = ReadingBody(_director.Catalog.Find(card.Id) ?? card, status, _surface.Commander());
                }
            });
        });
    }

    /// <summary>Everything on a story's page above its status line.</summary>
    private StackPanel ReadingBody(StoryCard card, StatusLine status, string? commander)
    {
        var page = new StackPanel { Spacing = 8 };

        page.Children.Add(RoutingKit.Title(card.Title).Row);

        if (_ratings is { Enabled: true } client)
        {
            page.Children.Add(RatingSection(client, card.Id, status, commander));
        }

        page.Children.Add(AdventuresPage.Muted($"{card.Pacing.Name} · {card.LevelGuideline}"));

        if (card.Tone is { Length: > 0 } tone)
        {
            page.Children.Add(AdventuresPage.Muted(tone));
        }

        page.Children.Add(AdventuresPage.Muted($"Core: {card.CoreName}"));
        page.Children.Add(AdventuresPage.Text(card.Blurb, TypeScale.Body));
        page.Children.Add(Labelled("In your words", card.InYourWords));
        if (!_director.Remembers(commander, card))
        {
            page.Children.Add(Labelled("The beacon", card.Beacon));
        }

        var cast = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        ShowCast(cast, card.Id);
        page.Children.Add(cast);

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

        var actions = new ContentControl();

        void Draw()
        {
            if (string.Equals(commander, _surface.Commander(), StringComparison.Ordinal))
            {
                actions.Content = ReadingActions(card, status, commander, withoutOdyssey, missing.Count, out gated);
            }
        }

        void Changed() => Dispatcher.UIThread.Post(Draw);

        void Again(string line) => Dispatcher.UIThread.Post(() => status.Say(line));

        actions.AttachedToVisualTree += (_, _) =>
        {
            _director.Stories.Changed += Changed;
            _director.WritingChanged += Changed;
            _director.WritingAgain += Again;
            Draw();
        };
        actions.DetachedFromVisualTree += (_, _) =>
        {
            _director.Stories.Changed -= Changed;
            _director.WritingChanged -= Changed;
            _director.WritingAgain -= Again;
        };

        Draw();
        page.Children.Add(actions);

        return page;
    }

    /// <summary>
    /// The story page's buttons: Pick or Switch while the story is not the Commander's, otherwise "This is your story." with
    /// Write it again once its chapter could not be written. <paramref name="gated"/> is the button the gender choice enables.
    /// </summary>
    private StackPanel ReadingActions(StoryCard card, StatusLine status, string? commander, bool withoutOdyssey, int missing, out Button? gated)
    {
        var current = _director.Stories.Current(commander);
        var stack = new StackPanel { Spacing = 8 };
        var bar = AdventuresPage.Buttons();
        bar.Margin = new Thickness(0, 10, 0, 0);
        gated = null;

        if (current is null || !string.Equals(current.Id, card.Id, StringComparison.OrdinalIgnoreCase))
        {
            stack.Children.Add(AdventuresPage.Muted(_director.Remembers(commander, card) ? PickingLineRemembered : PickingLine));
        }

        if (current is null)
        {
            Button? pick = null;
            pick = Act("Pick", () => Start(card, switching: false, status, pick, commander));
            pick.IsEnabled = !withoutOdyssey && GenderReady(card) && missing == 0;
            bar.Children.Add(gated = pick);
        }
        else if (!string.Equals(current.Id, card.Id, StringComparison.OrdinalIgnoreCase))
        {
            Button? switchTo = null;
            switchTo = Act("Switch", () => Confirm(
                commander,
                status,
                "story.switch",
                "Switch",
                $"Switch from {current.Title} to {card.Title}?",
                $"{current.Title} is abandoned and its chapters leave the list on the Stories tab. Your Backstory becomes this "
                + (_director.Remembers(commander, card)
                    ? "story's words, and it opens with your last beacon scan behind you."
                    : "story's words, and the Guardian cores wait for its own beacon scan."),
                "Switch",
                () => Start(card, switching: true, status, switchTo, commander)));
            switchTo.IsEnabled = !withoutOdyssey && GenderReady(card) && missing == 0;
            bar.Children.Add(gated = switchTo);
        }
        else
        {
            stack.Children.Add(AdventuresPage.Muted("This is your story."));

            if (_surface.Recap is { } recap)
            {
                bar.Children.Add(Act("Story so far", () => IfShown(commander, status, () =>
                {
                    if (recap(null) is { } refusal)
                    {
                        status.Fail(refusal);
                        _surface.Say(refusal);
                    }
                })));
            }

            if (_director.WriteFailed(commander) && !_director.IsWriting(commander))
            {
                bar.Children.Add(Act("Write it again", () => IfShown(commander, status, () => WriteAgain(status, commander))));
            }
        }

        if (bar.Children.Count > 0)
        {
            stack.Children.Add(bar);
        }

        return stack;
    }

    /// <summary>Writes the running story's owed chapter again from its page, saying the refusal if every draft fails.</summary>
    private void WriteAgain(StatusLine status, string? commander)
    {
        status.Say("Writing…");

        _ = Task.Run(async () =>
        {
            var refusal = await _director.WriteNextAsync(commander, _surface.Now(), CancellationToken.None).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() => Written(refusal, status, commander, "The chapter has begun."));
        });
    }

    /// <summary>Shows and says a refusal, or shows <paramref name="begun"/> and opens the chapter.</summary>
    private void Written(string? refusal, StatusLine status, string? commander, string begun)
    {
        if (refusal is not null)
        {
            status.Fail(refusal);
            _surface.Say(refusal);
            return;
        }

        status.Say(begun);

        if (string.Equals(commander, _surface.Commander(), StringComparison.Ordinal)
            && _director.Stories.Current(commander)?.CurrentChapter is { } key
            && _surface.Book.Store.Find(commander, key) is { } chapter)
        {
            _nav.SelectRoot(PanelTab.Stories, AdventuresPage.RootKey);
            _nav.GoTo(PanelTab.Stories, new NavCrumb(AdventuresPage.ReadPrefix + key, chapter.Name));
        }
    }

    private static Control Average(StoryRating? rating) => StarRating.Summary(rating);

    /// <summary>
    /// A story's average, and the Commander's own stars once they have picked it; redrawn in place while shown, so the
    /// stars keep keyboard focus across a vote.
    /// </summary>
    private StackPanel RatingSection(StoryRatingClient client, string id, StatusLine status, string? commander)
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
            var story = _director.Stories.Find(commander, id);

            average.Content = Average(client.Ratings.Get(id));
            label.IsVisible = mine.IsVisible = story is not null;
            pickFirst.IsVisible = story is null;
            pending.IsVisible = story is { RatingPending: true };
            mine.Value = story?.Rating ?? 0;
        }

        void Changed() => Dispatcher.UIThread.Post(Refresh);

        mine.Rated += stars => IfShown(commander, status, () => _ = Task.Run(async () =>
        {
            var sent = await (stars is { } given ? client.Rate(commander, id, given) : client.Clear(commander, id)).ConfigureAwait(false);

            if (!sent)
            {
                Dispatcher.UIThread.Post(() => status.Fail(StoryRatingClient.SendFailed));
            }
        }));

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

            if (member.Role is { Length: > 0 } role)
            {
                row.Children.Add(AdventuresPage.Text(char.ToUpperInvariant(role[0]) + role[1..], TypeScale.Body));
            }

            row.Children.Add(AdventuresPage.Text(CastVoiceChooser.Describe(member), TypeScale.Small, ThemeManager.GreyKey));

            if (member.Speaks.Key is { } chosen && _surface.CastVoices?.Failure(chosen) is { } failed)
            {
                row.Children.Add(AdventuresPage.Text($"The chosen voice failed, so the story's own spoke instead: {failed}", TypeScale.Small));
            }

            var buttons = new List<Control>();

            if (_surface.CastVoices is { } voices)
            {
                buttons.Add(Act("Play sample", () => voices.PlaySample(member.Shown, member.Speaks)));
                buttons.Add(Act("Change voice", () => CastVoiceChooser.Open(_prompts, voices, member.Key, () => ShowCast(holder, storyId))));
            }

            if (_surface.Pictures is { } pictures)
            {
                row.Children.Add(new PictureChooser(pictures, member.Shown.Picture, 240, null, [.. buttons]));
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

    private void Start(StoryCard card, bool switching, StatusLine status, Button? button, string? commander)
    {
        if (!string.Equals(commander, _surface.Commander(), StringComparison.Ordinal))
        {
            status.Fail(CommanderChangedRefusal);
            return;
        }

        if (!_surface.ModelAvailable() || !_surface.GalaxySearchOn())
        {
            status.Fail(!_surface.ModelAvailable()
                ? "A story needs a language model to write its chapters, and none is configured."
                : "A story needs galaxy search, so its chapters' places can be checked. It is off in Settings.");
            return;
        }

        if (_downloads is not null && _director.Catalog.Secret(card.Id) is null)
        {
            Download(card, status, button, () => Begin(card, switching, status, commander));
            return;
        }

        Begin(card, switching, status, commander);
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

    internal const string PickingLine =
        "Picking one makes its words your Backstory until it stops, and has the core write chapter one. Act one ends at a Guardian beacon "
        + "once your ship can reach it. While a story runs, the Guardian cores wait for its beacon scan. Pause or abandon the story to have them back at once.";

    internal const string PickingLineRemembered =
        "Picking one makes its words your Backstory until it stops, and has the core write chapter one. You scanned a Guardian beacon in an "
        + "earlier story, so this one opens with that scan behind you and its core comes aboard as it begins.";

    internal const string DownloadFailed = "The story could not be downloaded. Check your connection and pick it again.";

    private void Begin(StoryCard card, bool switching, StatusLine status, string? commander)
    {
        if (!string.Equals(commander, _surface.Commander(), StringComparison.Ordinal))
        {
            status.Fail(CommanderChangedRefusal);
            return;
        }

        var now = _surface.Now();

        status.Say("Writing chapter one…");

        _ = Task.Run(async () =>
        {
            var refusal = switching
                ? await _director.SwitchAsync(commander, card.Id, now, CancellationToken.None).ConfigureAwait(false)
                : await _director.PickAsync(commander, card.Id, now, CancellationToken.None).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() => Written(refusal, status, commander, "Chapter one has begun."));
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

    private void Confirm(string? drawnFor, StatusLine status, string key, string word, string title, string context, string yes, Action act)
    {
        if (!string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
        {
            status.Fail(CommanderChangedRefusal);
            return;
        }

        _prompts.Choose(
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
                    IfShown(drawnFor, status, act);
                }
            });
    }

    private static Border Row(params Control[] children) => Row(2, children);

    private static Border Row(double spacing, Control[] children)
    {
        var stack = new StackPanel { Spacing = spacing };

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
