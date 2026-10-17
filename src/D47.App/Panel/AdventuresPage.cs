using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Stories tab (Phase 47).</summary>
public sealed class AdventuresPage : UserControl, IPageSummary, IPageChrome
{
    public Control? BarTool { get; }

    public string? HelpTopic => null;

    public string Summary => "Stories you fly, told by the core. Progress comes from your own journal.";

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    public const string RootKey = "adventures";

    public const string ReadPrefix = "adventure.read.";

    public const string AskKey = "adventure.ask";

    public static NavCrumb AskCrumb => new(AskKey, "Short Story");

    /// <summary>The ask form for the next chapter of the finished adventure keyed after it.</summary>
    public const string NextPrefix = "adventure.next.";

    /// <summary>Whether a Stories-tab level is one Commander's page: a chapter or its next-chapter form.</summary>
    internal static bool BelongsToACommander(string key) =>
        key.StartsWith(ReadPrefix, StringComparison.Ordinal) || key.StartsWith(NextPrefix, StringComparison.Ordinal);

    private readonly AdventureSurface _surface;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly Func<Func<string, Task<bool>>?>? _copy;

    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly Notice _problems = new() { IsVisible = false };

    private StoryBenchPage? _benchPage;
    private MessagesView? _inbox;
    private StoriesView? _storiesView;

    /// <summary>The revision conversation per draft, for this session.</summary>
    private readonly Dictionary<string, List<AdventureRemark>> _exchanges = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What each draft was asked with, so a revision keeps the reach and the length.</summary>
    private readonly Dictionary<string, AdventureAsk> _asks = new(StringComparer.OrdinalIgnoreCase);

    private bool _showAside;

    /// <summary>The Ask form's brief entry that presses Go on commit, and the form it belongs to.</summary>
    private (Action BriefThenGo, Control View)? _askForm;

    public AdventuresPage(
        AdventureSurface surface, PanelNavigator nav, PanelPrompts prompts, Control? settingsStrip = null,
        Func<Func<string, Task<bool>>?>? copy = null)
    {
        _copy = copy;
        BarTool = PageChrome.ToolOf(settingsStrip);
        _surface = surface;
        _nav = nav;
        _prompts = prompts;

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(_problems, Dock.Top);
        _problems.Margin = new Thickness(0, 0, 0, 10);

        root.Children.Add(_problems);

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

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
        _surface.Book.Store.Changed += OnChanged;

        // A beat firing does not write the file — nothing on disk moves until the line has been said — so the
        // waiting indicator needs the book's own event as well as the store's.
        _surface.Book.StirringChanged += OnChanged;

        if (_surface.Switched is { } switched)
        {
            switched.Changed += OnChanged;
        }

        if (_surface.Messages is { } messages)
        {
            messages.Changed += OnChanged;
        }

        if (_surface.Stories is { } director)
        {
            director.Stories.Changed += OnChanged;
        }

        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _surface.Book.Store.Changed -= OnChanged;
        _surface.Book.StirringChanged -= OnChanged;

        if (_surface.Switched is { } switched)
        {
            switched.Changed -= OnChanged;
        }

        if (_surface.Messages is { } messages)
        {
            messages.Changed -= OnChanged;
        }

        if (_surface.Stories is { } director)
        {
            director.Stories.Changed -= OnChanged;
        }
    }

    /// <summary>The levels below the root, by crumb.</summary>
    public Control? Build(NavCrumb crumb)
    {
        if (crumb.Key == AskKey)
        {
            return BuildAsk(follows: null);
        }

        if (crumb.Key.StartsWith(NextPrefix, StringComparison.Ordinal))
        {
            return BuildAsk(follows: crumb.Key[NextPrefix.Length..]);
        }

        if (_surface.Messages is { } inbox
            && (crumb.Key == MessagesView.RootKey || crumb.Key.StartsWith(MessagesView.ReadPrefix, StringComparison.Ordinal)))
        {
            _inbox ??= new MessagesView(inbox, _nav, _surface, _prompts);
            return crumb.Key == MessagesView.RootKey ? _inbox : _inbox.Build(crumb);
        }

        if (crumb.Key == StoryBenchPage.RootKey)
        {
            return _surface.Stories is { } stories && _surface.Bench is { } bench
                ? _benchPage ??= new StoryBenchPage(_surface, bench, stories, _prompts)
                : null;
        }

        if (_surface.Stories is { } director
            && (crumb.Key == StoriesView.RootKey || crumb.Key.StartsWith(StoriesView.ReadPrefix, StringComparison.Ordinal)))
        {
            _storiesView ??= new StoriesView(_surface, director, _nav, _prompts);
            return crumb.Key == StoriesView.RootKey ? _storiesView : _storiesView.Build(crumb);
        }

        if (crumb.Key.StartsWith(ReadPrefix, StringComparison.Ordinal))
        {
            return BuildReading(crumb.Key[ReadPrefix.Length..]);
        }

        return null;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    /// <summary>Redraws the root after something it reads outside the book has changed.</summary>
    public void Refresh() => Rebuild();

    // ---- the root ----------------------------------------------------------------------------

    private void Rebuild()
    {
        _list.Children.Clear();

        var problems = _surface.Book.Store.Problems;
        _problems.IsVisible = problems.Count > 0;
        _problems.Text = string.Join("\n", problems.Select(problem => $"{problem.Where}: {problem.Reason}"));

        var standings = _surface.Book.Standings(_surface.Commander());

        if (standings.Count == 0)
        {
            _list.Children.Add(Muted(
                "No stories yet. Pick one under Catalogue, or ask for a short story — D47 proposes one and waits for your yes."));

            if (!CanAsk(_surface))
            {
                _list.Children.Add(Muted(AskShutBecause()));
            }

            return;
        }

        if (!CanAsk(_surface))
        {
            _list.Children.Add(Muted(AskShutBecause()));
        }

        var drafts = standings.Where(standing => standing.Adventure.IsDraft).ToList();
        var live = standings.Where(standing => !standing.Adventure.IsDraft && standing.Adventure.IsActive && !standing.IsDone).ToList();
        var unbegun = standings.Where(standing => !standing.Adventure.IsDraft && !standing.Adventure.IsBegun).ToList();
        var aside = standings.Where(standing => standing.Adventure.IsAbandoned || standing.IsDone).ToList();

        foreach (var standing in drafts)
        {
            _list.Children.Add(DraftCard(standing));
        }

        foreach (var standing in live.Concat(unbegun))
        {
            _list.Children.Add(Card(standing));
        }

        if (aside.Count > 0)
        {
            var fold = new Button
            {
                Content = _showAside
                    ? "Hide set aside"
                    : $"Set aside ({aside.Count.ToString(CultureInfo.InvariantCulture)})",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0),
            };

            fold.Click += (_, _) =>
            {
                _showAside = !_showAside;
                Rebuild();
            };

            _list.Children.Add(fold);

            if (_showAside)
            {
                foreach (var standing in aside)
                {
                    _list.Children.Add(Card(standing));
                }
            }
        }
    }

    /// <summary>Whether the shown Commander has a story or short story under way, or a story ending waiting for an answer.</summary>
    internal static bool InAStory(AdventureSurface surface, D47.Core.Messages.MessageStore store)
    {
        var commander = surface.Commander();

        return surface.Stories?.Stories.Current(commander) is not null
            || surface.Book.Standings(commander).Any(standing =>
                !standing.Adventure.IsDraft && standing.Adventure.IsActive && !standing.IsDone)
            || store.All.Any(message => MessagesView.IsWaiting(message, surface));
    }

    /// <summary>Whether a short story can be asked for: a model to write it and galaxy search to check its places.</summary>
    internal static bool CanAsk(AdventureSurface surface) => surface.ModelAvailable() && surface.GalaxySearchOn();

    private string AskShutBecause() => !_surface.ModelAvailable()
        ? "A short story needs a language model, and none is configured."
        : "A short story needs galaxy search, so the story's places can be checked. It is off in Settings.";

    private Control Card(AdventureStanding standing)
    {
        var adventure = standing.Adventure;
        var card = Row();
        var body = (StackPanel)card.Child!;

        body.Children.Add(RowName(adventure.Name));

        // The step joins the place on the same line rather than taking one of its own: it is the second half
        // of "where am I", and a card is read at a glance (asked for 2026-08-22).
        var where = standing.Step() is { } step
            ? $"{By(adventure)} — {standing.Place()} · {step}"
            : $"{By(adventure)} — {standing.Place()}";

        body.Children.Add(RowSecondary(where));

        if (standing.NextTrigger() is { } next)
        {
            body.Children.Add(Trigger($"Next: {next}.", Here(), standing.CurrentBeat?.Trigger.System, Copy()));
        }

        if (_surface.Book.IsStirring(_surface.Commander(), adventure.Key))
        {
            body.Children.Add(new AdventureThinking());
        }

        card.PointerPressed += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + adventure.Key, adventure.Name));
        return card;
    }

    private Control DraftCard(AdventureStanding standing)
    {
        var adventure = standing.Adventure;
        var card = Row();
        var body = (StackPanel)card.Child!;

        body.Children.Add(RowName(adventure.Name));
        body.Children.Add(RowSecondary($"{By(adventure)} — waiting for your yes"));

        if (FollowsLine(adventure) is { } follows)
        {
            body.Children.Add(Muted(follows));
        }

        if (ShowsSpine(adventure) && !string.IsNullOrWhiteSpace(adventure.Spine?.Premise))
        {
            body.Children.Add(Text(adventure.Spine.Premise, TypeScale.Body));
        }

        var buttons = Buttons();
        buttons.Margin = new Thickness(0, 6, 0, 0);

        var accept = new Button { Content = "Accept" };
        accept.Click += (_, _) => Begin(adventure.Key);

        var change = new Button { Content = "Change something" };
        change.Click += (_, _) => Revise(adventure);

        var decline = new Button { Content = "Decline", Classes = { "destructive" } };
        decline.Click += (_, _) => Remove(adventure, confirm: false, _surface.Commander());

        buttons.Children.Add(accept);
        buttons.Children.Add(change);

        if (adventure.Previous is not null)
        {
            var back = new Button { Content = "Put it back" };
            back.Click += (_, _) =>
            {
                var refusal = _surface.Book.Write(_surface.Commander(), adventure.Previous with { Previous = null });

                if (refusal is not null)
                {
                    _surface.Say(refusal);
                }
            };

            buttons.Children.Add(back);
        }

        buttons.Children.Add(decline);

        var read = new Button { Content = "Read it" };
        read.Click += (_, _) => _nav.Drill(new NavCrumb(ReadPrefix + adventure.Key, adventure.Name));
        buttons.Children.Add(read);

        body.Children.Add(buttons);
        return card;
    }

    // ---- the reading level -------------------------------------------------------------------

    private Control BuildReading(string key)
    {
        var page = new StackPanel { Spacing = 8, Margin = new Thickness(14) };

        void Fill()
        {
            page.Children.Clear();

            if (_surface.Book.Standing(_surface.Commander(), key) is not { } standing)
            {
                page.Children.Add(Muted("That story is no longer on file."));
                return;
            }

            var adventure = standing.Adventure;

            page.Children.Add(RoutingKit.Title(adventure.Name).Row);

            var where = standing.Step() is { } step
                ? $"{By(adventure)} — {standing.Place()} · {step}"
                : $"{By(adventure)} — {standing.Place()}";

            page.Children.Add(Muted(where));

            if (FollowsLine(adventure) is { } follows)
            {
                page.Children.Add(Muted(follows));
            }

            if (ShowsSpine(adventure) && adventure.Spine is { } spine && !string.IsNullOrWhiteSpace(spine.Premise))
            {
                page.Children.Add(Text(spine.Premise, TypeScale.Body));
            }

            Told(page, standing, Here(), SpeakerName, Copy());

            // The wait, drawn (asked for 2026-08-22).
            if (_surface.Book.IsStirring(_surface.Commander(), adventure.Key))
            {
                page.Children.Add(new AdventureThinking());
            }

            AddObjective(page, standing);

            if (standing.IsDone && adventure.Beats.Count > 0)
            {
                page.Children.Add(Muted("The end."));
            }

            page.Children.Add(ReadingBar(standing));
        }

        Fill();

        var scroller = new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        void Follow() => Dispatcher.UIThread.Post(Fill);

        scroller.AttachedToVisualTree += (_, _) =>
        {
            _surface.Book.Store.Changed += Follow;

            // And the book's own event, for the same reason the root takes it: a beat firing advances the
            // step and starts the wait, and neither of those writes the file.
            _surface.Book.StirringChanged += Follow;
            if (_surface.Stories is { } writer)
            {
                writer.WritingChanged += Follow;
            }

            Fill();
        };

        scroller.DetachedFromVisualTree += (_, _) =>
        {
            _surface.Book.Store.Changed -= Follow;
            _surface.Book.StirringChanged -= Follow;
            if (_surface.Stories is { } writer)
            {
                writer.WritingChanged -= Follow;
            }
        };

        return scroller;
    }

    /// <summary>The NEXT group: the objective, the wait for a different one, and Change objective and Never ask this under it.</summary>
    private void AddObjective(StackPanel page, AdventureStanding standing)
    {
        var rewriting = _surface.Stories?.IsRewriting(_surface.Commander()) == true;
        var next = standing.NextTrigger();

        if (next is null && !rewriting)
        {
            return;
        }

        page.Children.Add(RoutingKit.Section("Next"));

        if (rewriting)
        {
            page.Children.Add(new AdventureThinking());
            page.Children.Add(Muted("Writing a different objective…"));
        }

        if (next is not null)
        {
            page.Children.Add(Trigger(Sentence(next), Here(), standing.CurrentBeat?.Trigger.System, Copy()));
        }

        var drawnFor = _surface.Commander();
        var row = Buttons();

        if (!rewriting
            && ShipObjectiveButtons.For(_surface, standing.CurrentBeat?.Trigger, _nav, _prompts) is { } ship)
        {
            row.Children.Add(ship);
        }

        if (standing.IsRefusable && !rewriting && _surface.Stories is not null)
        {
            row.Children.Add(Action("Change objective", () => ChangeObjective(drawnFor)));

            row.Children.Add(Action("Never ask this", () => StoriesView.NotForMe(
                _surface,
                _prompts,
                drawnFor,
                () => _surface.Say("Writing a different objective…"),
                refusal =>
                {
                    if (refusal is not null)
                    {
                        _surface.Say(refusal);
                    }
                })));
        }

        if (row.Children.Count > 0)
        {
            row.Margin = new Thickness(0, 6, 0, 0);
            page.Children.Add(row);
        }
    }

    /// <summary>Opens the entry for a different objective, saying why it cannot; returns the refusal, or null.</summary>
    internal string? ChangeObjective(string? drawnFor)
    {
        var refusal = StoriesView.ChangeObjective(_surface, _prompts, drawnFor, () => _surface.Say("Writing a different objective…"), SayRefusal);

        if (refusal is not null)
        {
            _surface.Say(refusal);
        }

        return refusal;
    }

    /// <summary>Shows the Stories tab and opens the entry for a different objective; returns why it cannot, or null.</summary>
    internal string? ChangeObjectiveByVoice()
    {
        _prompts.Abandon();
        _nav.Show(RootKey);
        return StoriesView.ChangeObjective(
            _surface, _prompts, _surface.Commander(), () => _surface.Say("Writing a different objective…"), SayRefusal);
    }

    private void SayRefusal(string? refusal)
    {
        if (refusal is not null)
        {
            _surface.Say(refusal);
        }
    }

    /// <summary>Whether the page may show an adventure's Premise, Turn and Ending: never for a stock story's chapter.</summary>
    internal static bool ShowsSpine(Adventure adventure) => adventure.StoryId is null;

    private Control ReadingBar(AdventureStanding standing)
    {
        var adventure = standing.Adventure;
        var drawnFor = _surface.Commander();
        var bar = Buttons();
        bar.Margin = new Thickness(0, 10, 0, 0);

        foreach (var label in standing.ReadingButtons())
        {
            bar.Children.Add(label switch
            {
                "Accept" or "Begin" or "Begin again" => Action(label, () => IfShown(drawnFor, () => Begin(adventure.Key))),
                "Change something" => Action(label, () => IfShown(drawnFor, () => Revise(adventure))),
                "Decline" => Action(label, () => IfShown(drawnFor, () => Remove(adventure, confirm: false, drawnFor)), destructive: true),
                "Abandon" => Action(label, () => IfShown(drawnFor, () => Abandon(adventure, drawnFor))),
                "Write the next chapter" => Action(label, () => _nav.Drill(new NavCrumb(NextPrefix + adventure.Key, "Next chapter"))),
                _ => Action(label, () => IfShown(drawnFor, () => Remove(adventure, confirm: true, drawnFor)), destructive: true),
            });
        }

        if (adventure is { StoryId: null, IsActive: true } && !standing.IsDone && _surface.Recap is { } recap)
        {
            bar.Children.Add(Action("Story so far", () => IfShown(drawnFor, () =>
            {
                if (recap(adventure.Key) is { } refusal)
                {
                    _surface.Say(refusal);
                }
            })));
        }

        if (_surface.Stories is { } stories
            && stories.Stories.Current(drawnFor) is { } story
            && string.Equals(story.CurrentChapter, adventure.Key, StringComparison.Ordinal))
        {
            bar.Children.Add(StoriesView.StoryOnBox(_surface, stories, story, drawnFor));
        }

        return bar;
    }

    // ---- the ask form ------------------------------------------------------------------------

    /// <summary>The ask form; with <paramref name="follows"/>, for the next chapter of that adventure.</summary>
    private Control BuildAsk(string? follows)
    {
        var page = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
        var reach = AdventureReach.NearHere;
        var length = AdventureLength.Evening;
        var thisShipOnly = false;
        var brief = string.Empty;

        var state = _surface.State();
        var hasChoice = state is not null && (state.Fleet.Ships.Count > 0 || state.Carrier.Owned);

        var after = follows is null ? null : _surface.Book.Store.Find(_surface.Commander(), follows);

        if (follows is null)
        {
            page.Children.Add(RoutingKit.Title("Ask for a short story").Row);
            page.Children.Add(Muted(
                "The core writes a story for you to fly and waits for your yes. Three choices with "
                + "defaults, and a brief if you want one — pressing Go on an untouched form is a complete ask."));
        }
        else
        {
            page.Children.Add(RoutingKit.Title("Write the next chapter").Row);
            page.Children.Add(Muted(
                $"The core writes the story that follows {after?.Name ?? "that story"}, from how it ended, "
                + "and waits for your yes. The same three choices, and a brief if you want one."));
        }

        var reachCombo = new Segment
        {
            ItemsSource = ["Reach: near here", "Reach: a session's flying", "Reach: anywhere"],
            SelectedIndex = 0,
        };
        AutomationProperties.SetName(reachCombo, "Reach");

        var lengthCombo = new Segment
        {
            ItemsSource = ["Length: short", "Length: an evening", "Length: long"],
            SelectedIndex = 1,
        };
        AutomationProperties.SetName(lengthCombo, "Length");

        var (thisShip, _) = LabeledCheckBox.Build("This ship only");
        var briefButton = new Button { HorizontalAlignment = HorizontalAlignment.Left };
        var status = new StatusLine();
        var go = new Button { Content = "Go" };
        var stop = new Button { Content = "Stop", IsVisible = false };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { go, stop } };
        var shut = !_surface.ModelAvailable() || !_surface.GalaxySearchOn();

        var progress = new WriteProgressBar("Writing the story") { IsVisible = false };

        void Label()
        {
            briefButton.Content = string.IsNullOrWhiteSpace(brief) ? "Brief: none" : $"Brief: \"{brief}\"";
        }

        reachCombo.SelectionChanged += (_, _) => reach = reachCombo.SelectedIndex switch
        {
            1 => AdventureReach.Session,
            2 => AdventureReach.Anywhere,
            _ => AdventureReach.NearHere,
        };

        lengthCombo.SelectionChanged += (_, _) => length = lengthCombo.SelectedIndex switch
        {
            0 => AdventureLength.Short,
            2 => AdventureLength.Long,
            _ => AdventureLength.Evening,
        };

        thisShip.IsChecked = thisShipOnly;
        thisShip.IsCheckedChanged += (_, _) => thisShipOnly = thisShip.IsChecked == true;

        void Brief(Action then) => _prompts.Enter(
            new EntryRequest(
                "adventure.brief",
                "Brief",
                "What should it be about?",
                "A theme, a mood, a place it must include. Empty is fine.",
                brief,
                EntrySurface.Voice),
            value =>
            {
                brief = value.Trim();
                Label();
                then();
            });

        briefButton.Click += (_, _) => Brief(() => { });

        var view = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var showing = false;

        // Idle again after a write this form was showing: stopped, or offered on another page.
        void Ready()
        {
            showing = false;
            progress.IsVisible = false;
            stop.IsVisible = false;
            go.IsEnabled = !shut;
            status.Clear();
        }

        // Only a page on screen shows a settled write, and never from the timer.
        void Show(bool offer = true)
        {
            var writing = _surface.Writing;

            if (offer && TopLevel.GetTopLevel(view) is not null && writing.Take() is { Offer: { } settled } done)
            {
                Ready();
                Present(settled, done.Ask with { Progress = null }, status);
                return;
            }

            if (writing.Current is not { } write)
            {
                if (showing)
                {
                    Ready();
                }

                return;
            }

            showing = true;
            go.IsEnabled = false;
            stop.IsVisible = true;
            progress.IsVisible = true;
            progress.Show(write.Progress);

            if (write.Aside is { } aside)
            {
                status.Say(aside);
            }
            else
            {
                status.Clear();
            }
        }

        void Go()
        {
            if (_surface.Writing.Current is not null)
            {
                return;
            }

            if (!_surface.ModelAvailable() || !_surface.GalaxySearchOn())
            {
                status.Say(AskShutBecause());
                return;
            }

            AdventureChapter? chapter = null;

            if (follows is not null
                && (chapter = _surface.Book.ChapterOf(_surface.Commander(), follows)) is null)
            {
                status.Fail("That story is no longer on file.");
                return;
            }

            var ask = new AdventureAsk(reach, length, thisShipOnly, string.IsNullOrWhiteSpace(brief) ? null : brief, chapter);

            status.Clear();

            if (_surface.Writing.Begin(
                    ask,
                    _surface.Commander(),
                    (asked, aside, token) => _surface.Generator.DraftAsync(asked, _surface.Now(), aside, token),
                    line => Dispatcher.UIThread.Post(() => _surface.Say(line)),
                    write => Dispatcher.UIThread.Post(() => Settle(write), DispatcherPriority.Background)))
            {
                Show();
            }
        }

        go.Click += (_, _) => Go();

        stop.Click += (_, _) =>
        {
            _surface.Writing.Stop();
            Show();
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => Show(offer: false);

        void OnWriting() => Dispatcher.UIThread.Post(() => Show(), DispatcherPriority.Background);

        view.AttachedToVisualTree += (_, _) =>
        {
            _surface.Writing.Changed += OnWriting;
            timer.Start();
            Show();
        };

        view.DetachedFromVisualTree += (_, _) =>
        {
            _surface.Writing.Changed -= OnWriting;
            timer.Stop();
        };

        Label();

        page.Children.Add(reachCombo);
        page.Children.Add(lengthCombo);

        // Left out rather than hidden when there is nothing to choose between (#202).
        if (hasChoice)
        {
            page.Children.Add(thisShip);
        }

        page.Children.Add(briefButton);
        page.Children.Add(buttons);
        page.Children.Add(progress);
        page.Children.Add(status);

        if (shut)
        {
            status.Say(AskShutBecause());
            go.IsEnabled = false;

            var settings = new Button { Content = "Open settings", HorizontalAlignment = HorizontalAlignment.Left };
            settings.Click += (_, _) => _surface.OpenSettings();
            page.Children.Add(settings);
        }

        if (follows is null)
        {
            _askForm = (() => Brief(Go), view);
        }

        return view;
    }

    /// <summary>Shows the Ask page and opens its brief entry, whose commit presses Go; or says why asking is shut.</summary>
    internal string? AskByVoice()
    {
        _prompts.Abandon();

        if (!CanAsk(_surface))
        {
            _nav.Show(RootKey);
            return AskShutBecause();
        }

        _nav.SetVisible(PanelTab.Stories, AskKey, true);
        _nav.Show(AskKey);

        if (!OnAsk())
        {
            return "The Short Story page could not be opened.";
        }

        // At background priority, so the panel has laid out the navigation and the form is on screen.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (OnAsk() && !_nav.Modal && _askForm is { } form && TopLevel.GetTopLevel(form.View) is not null)
                {
                    form.BriefThenGo();
                }
            },
            DispatcherPriority.Background);

        return null;
    }

    /// <summary>What a short story write is doing, beside its bar.</summary>
    internal static string Doing(WriteStage stage) => stage switch
    {
        WriteStage.Places => "Finding places nearby…",
        WriteStage.Outline => "Writing the outline…",
        WriteStage.Objectives => "Writing your objectives…",
        _ => "Checking the objectives…",
    };

    private bool OnAsk() => _nav.Tab == PanelTab.Stories && _nav.Trail is [.., { Key: AskKey }];

    /// <summary>
    /// A write finishes, whether or not a Short Story page is open: the draft stored and the core's reply spoken, or the
    /// failure spoken. Posted at Background, so an aside the write posted before it is said first.
    /// </summary>
    private void Settle(StoryWrite write)
    {
        if (!ReferenceEquals(_surface.Writing.Current, write) || write.Outcome is not { } outcome)
        {
            return;
        }

        _surface.Writing.Settle(write, Stored(outcome, write.Ask with { Progress = null }, write.DrawnFor));
    }

    private StoryOffer Stored(AdventureOutcome outcome, AdventureAsk ask, string? drawnFor)
    {
        if (!string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
        {
            return new StoryOffer(null, null, StoriesView.CommanderChangedRefusal, Failed: true);
        }

        if (outcome.Draft is not { } draft)
        {
            _surface.Say(outcome.Message ?? "I could not write that one.");
            return new StoryOffer(null, null, outcome.Message ?? "Nothing came back.", Failed: true);
        }

        var key = UniqueKey(draft.Key, draft.Previous?.Key);
        var stored = draft with { Key = key };
        var refusal = _surface.Book.Write(_surface.Commander(), stored);

        if (refusal is not null)
        {
            return new StoryOffer(null, null, refusal, Failed: true);
        }

        _asks[key] = ask;
        _surface.Say(outcome.Reply ?? $"{stored.Name}. It is yours to accept or send back.");

        return new StoryOffer(key, stored.Name, string.Join(" ", outcome.Notes), Failed: false);
    }

    /// <summary>A settled write is shown: its failure on the status line, or its draft's reading level opened while the draft is on file.</summary>
    private void Present(StoryOffer offer, AdventureAsk ask, StatusLine status)
    {
        if (offer.Failed)
        {
            status.Fail(offer.Note);
            return;
        }

        status.Say(offer.Note);

        if (offer.Key is not { } key || _surface.Book.Store.Find(_surface.Commander(), key) is null)
        {
            return;
        }

        _asks[key] = ask;

        // Below the root only: the root is the tab's own and GoTo keeps it. The Stories tab rather than the one
        // showing, which the Commander may have changed while the story was written.
        _nav.SelectRoot(PanelTab.Stories, RootKey);
        _nav.GoTo(PanelTab.Stories, new NavCrumb(ReadPrefix + key, offer.Name ?? key));
    }

    private string UniqueKey(string wanted, string? keep)
    {
        var existing = _surface.Book.Store.For(_surface.Commander());
        var key = string.IsNullOrWhiteSpace(wanted) ? "adventure" : wanted;
        var candidate = key;
        var suffix = 2;

        while (existing.Any(other =>
                   string.Equals(other.Key, candidate, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(other.Key, keep, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{key}-{suffix++}";
        }

        return candidate;
    }

    // ---- the acts ----------------------------------------------------------------------------

    private void Begin(string key)
    {
        var refusal = _surface.Book.Begin(_surface.Commander(), key, _surface.Now());

        if (refusal is not null)
        {
            _surface.Say(refusal);
        }

    // The opening itself is said by the callout on the next tick, in the core's voice.
    }

    /// <summary>Runs <paramref name="act"/> if <paramref name="drawnFor"/> is still the shown Commander; otherwise says nothing was done.</summary>
    private void IfShown(string? drawnFor, Action act)
    {
        if (string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
        {
            act();
        }
        else
        {
            _surface.Say(StoriesView.CommanderChangedRefusal);
        }
    }

    private void Abandon(Adventure adventure, string? drawnFor) => _prompts.Choose(
        new ChoiceRequest(
            "adventure.abandon",
            "Abandon",
            $"Abandon {adventure.Name}?",
            "The story stops here. It stays on file under Set aside, and Begin again starts it from the opening.",
            [new ChoiceOption("keep", "Keep going"), new ChoiceOption("abandon", "Abandon it")],
            null,
            ChoiceSurface.Layer),
        option =>
        {
            if (option.Key != "abandon")
            {
                return;
            }

            IfShown(drawnFor, () =>
            {
                var refusal = _surface.Book.Abandon(drawnFor, adventure.Key, _surface.Now());

                if (refusal is not null)
                {
                    _surface.Say(refusal);
                }
            });
        });

    private void Remove(Adventure adventure, bool confirm, string? drawnFor)
    {
        if (!confirm)
        {
            _surface.Book.Remove(drawnFor, adventure.Key);
            _nav.ToRoot();
            return;
        }

        _prompts.Choose(
            new ChoiceRequest(
                "adventure.remove",
                "Remove",
                $"Remove {adventure.Name}?",
                "It comes off the file, with everything it reached. There is no way back from this one.",
                [new ChoiceOption("keep", "Keep it"), new ChoiceOption("remove", "Remove it")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "remove")
                {
                    return;
                }

                IfShown(drawnFor, () =>
                {
                    _surface.Book.Remove(drawnFor, adventure.Key);
                    _nav.ToRoot();
                });
            });
    }

    /// <summary>The draft the voice commands act on, or the reason there is none: the open draft's page, else the only draft.</summary>
    private (Adventure? Draft, string? Refusal) VoiceDraft()
    {
        var commander = _surface.Commander();

        if (_nav.Tab == PanelTab.Stories
            && _nav.Trail[^1].Key is var open
            && open.StartsWith(ReadPrefix, StringComparison.Ordinal)
            && _surface.Book.Standing(commander, open[ReadPrefix.Length..]) is { Adventure.IsDraft: true } standing)
        {
            return (standing.Adventure, null);
        }

        var drafts = _surface.Book.Standings(commander).Where(other => other.Adventure.IsDraft).ToList();

        return drafts.Count switch
        {
            0 => (null, "There is no draft short story."),
            1 => (drafts[0].Adventure, null),
            _ => (null, "There is more than one draft short story. Open the one you mean."),
        };
    }

    private string? OnVoiceDraft(Action<Adventure> act)
    {
        var (draft, refusal) = VoiceDraft();

        if (draft is not null)
        {
            act(draft);
        }

        return refusal;
    }

    internal string? ChangeDraft() => OnVoiceDraft(Revise);

    internal string? AcceptDraft() => OnVoiceDraft(draft => Begin(draft.Key));

    internal string? RejectDraft() => OnVoiceDraft(draft => Remove(draft, confirm: false, _surface.Commander()));

    /// <summary>Reasoning with the AI about a draft (Phase 47, "Revision before acceptance").</summary>
    private void Revise(Adventure draft) => _prompts.Enter(
        new EntryRequest(
            "adventure.revise",
            "Change",
            "What should change?",
            "Say it as you would to the core — \"not Colonia, something closer\", \"the stakes are too low\".",
            string.Empty,
            EntrySurface.Voice,
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("Say what should change.") : EntryVerdict.Ok),
        remark =>
        {
            var ask = _asks.GetValueOrDefault(draft.Key) ?? new AdventureAsk();
            var exchange = _exchanges.GetValueOrDefault(draft.Key) ?? [];

            var drawnFor = _surface.Commander();

            _surface.Say("Let me think about that.");

            _ = Task.Run(async () =>
            {
                var outcome = await _surface.Generator
                    .ReviseAsync(draft, ask, exchange, remark, _surface.Now(), CancellationToken.None)
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
                    {
                        _surface.Say(StoriesView.CommanderChangedRefusal);
                        return;
                    }

                    if (outcome.Draft is not { } revised)
                    {
                        _surface.Say(outcome.Message ?? "I could not change it that way.");
                        return;
                    }

                    // The same key, so the card stays where it was; the old draft rides along.
                    var stored = revised with { Key = draft.Key, Previous = draft with { Previous = null } };
                    var refusal = _surface.Book.Write(_surface.Commander(), stored);

                    if (refusal is not null)
                    {
                        _surface.Say(refusal);
                        return;
                    }

                    _exchanges[draft.Key] = [.. exchange, new AdventureRemark(remark, outcome.Reply)];
                    _asks[draft.Key] = ask;
                    _surface.Say(outcome.Reply ?? "Changed.");
                });
            });
        });

    // ---- drawing -----------------------------------------------------------------------------

    /// <summary>"Follows The Lantern Route." for a chapter whose previous adventure is still on file, or null.</summary>
    private string? FollowsLine(Adventure adventure) =>
        adventure.Follows is { } key && _surface.Book.Store.Find(_surface.Commander(), key) is { } previous
            ? $"Follows {previous.Name}."
            : null;

    private static string By(Adventure adventure) => adventure.Source == AdventureSource.Commander
        ? "yours"
        : adventure.WrittenBy is { } id && D47.Core.Persona.PersonaCatalog.Knows(id)
            ? $"written by {D47.Core.Persona.PersonaCatalog.Resolve(id).Name}"
            : "written by d47";

    /// <summary>A pressable list row holding a stack.</summary>
    private static Border Row() => ListRow.Dress(new Border
    {
        Child = new StackPanel { Spacing = 2 },
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
    });

    /// <summary>A row of buttons that wraps rather than clips.</summary>
    internal static WrapPanel Buttons(params Control[] buttons)
    {
        var panel = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };

        foreach (var button in buttons)
        {
            panel.Children.Add(button);
        }

        return panel;
    }

    /// <summary>A row's name.</summary>
    internal static TextBlock RowName(string text) =>
        ListRow.Name(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

    /// <summary>A row's sub line.</summary>
    internal static TextBlock RowSecondary(string text) =>
        ListRow.Sub(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

    /// <summary>The Commander's current system, from the app's own state.</summary>
    private string? Here() => _surface.State()?.Location?.StarSystem;

    private Func<string, Task<bool>>? Copy() => _copy?.Invoke();

    private static Control Labelled(string label, string? text, Control? content = null) =>
        Labelled(Text(label, TypeScale.Small, ThemeManager.GreyKey), text, content);

    private static Control Labelled(TextBlock label, string? text, Control? content = null)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };
        stack.Children.Add(label);

        if (text is { Length: > 0 })
        {
            stack.Children.Add(Text(text, TypeScale.Body));
        }

        if (content is not null)
        {
            stack.Children.Add(content);
        }

        return stack;
    }

    /// <summary>A trigger, in A, with <paramref name="here"/> in Cyan where the text names it.</summary>
    internal static TextBlock Trigger(string text, string? here)
    {
        var block = Text(text, TypeScale.Secondary, ThemeManager.AKey);

        if (Naming(text, here) is not { } at)
        {
            return block;
        }

        var system = new Run(text.Substring(at, here!.Length));
        Themed(system, TextElement.ForegroundProperty, ThemeManager.CyanKey);

        block.Text = null;
        block.Inlines = [new Run(text[..at]), system, new Run(text[(at + here.Length)..])];

        return block;
    }

    /// <summary>
    /// <see cref="Trigger(string, string?)"/> with a copy glyph straight after <paramref name="system"/> where the
    /// text names it. Without a copy delegate, or when the text does not name it, it is the plain trigger.
    /// </summary>
    internal static Control Trigger(string text, string? here, string? system, Func<string, Task<bool>>? copy)
    {
        if (copy is null || Naming(text, system) is not { } at)
        {
            return Trigger(text, here);
        }

        var end = at + system!.Length;
        var name = new Run(text[at..end]);

        if (string.Equals(system, here, StringComparison.OrdinalIgnoreCase))
        {
            Themed(name, TextElement.ForegroundProperty, ThemeManager.CyanKey);
        }

        var before = Text("", TypeScale.Secondary, ThemeManager.AKey);
        before.Text = null;
        before.Inlines = [new Run(text[..at]), name];
        before.VerticalAlignment = VerticalAlignment.Center;

        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(before);
        row.Children.Add(CopyGlyph.For(system, copy));

        if (end < text.Length)
        {
            var after = Text(text[end..], TypeScale.Secondary, ThemeManager.AKey);
            after.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(after);
        }

        return row;
    }

    /// <summary>Where <paramref name="text"/> names <paramref name="system"/> as a whole name, or null.</summary>
    internal static int? Naming(string text, string? system)
    {
        if (string.IsNullOrWhiteSpace(system))
        {
            return null;
        }

        for (var at = text.IndexOf(system, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = text.IndexOf(system, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + system.Length;

            if ((at == 0 || !char.IsLetterOrDigit(text[at - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end])))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>A trigger phrase as a sentence: "arrive at X" reads badly without a capital and a stop.</summary>
    internal static string Sentence(string phrase) =>
        phrase.Length == 0 ? phrase : char.ToUpperInvariant(phrase[0]) + phrase[1..] + ".";

    /// <summary>The name a story speaker id is shown under: the cast member's, "Narrator", or null for the ship.</summary>
    private string? SpeakerName(string? speaker) => speaker switch
    {
        null or D47.Core.Stories.StorySpeaker.Ship => null,
        D47.Core.Stories.StorySpeaker.Narrator => D47.Core.Audio.VoiceRoles.Called(D47.Core.Audio.VoiceRole.Narrator),
        _ => _surface.Stories?.Speaker(_surface.Commander(), speaker)?.Name ?? speaker,
    };

    /// <summary>What has actually been said about this story (asked for 2026-08-22).</summary>
    private static void Told(StackPanel page, AdventureStanding standing, string? here, Func<string?, string?> named, Func<string, Task<bool>>? copy)
    {
        var adventure = standing.Adventure;

        if (adventure.Told.Count == 0)
        {
            Authored(page, standing, named);
            return;
        }

        foreach (var told in adventure.Told)
        {
            var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };

            if (told.Kind == AdventureToldKind.Beat && told.Line > 0)
            {
                stack.Margin = new Thickness(0, 4, 0, 0);
            }
            else if (told.Kind == AdventureToldKind.Beat)
            {
                var heading = told.Title is { Length: > 0 } title ? title : adventure.Name;

                stack.Children.Add(Stamped($"{heading} — ", told.At));

                if (told.Trigger is { Length: > 0 } trigger)
                {
                    var system = told.Beat >= 0 && told.Beat < adventure.Beats.Count ? adventure.Beats[told.Beat].Trigger.System : null;
                    stack.Children.Add(Trigger(Sentence(trigger), here, system, copy));
                }
            }
            else if (told.Kind == AdventureToldKind.Nudge)
            {
                stack.Children.Add(Stamped("Narrator — ", told.At));
            }
            else
            {
                // The Commander's own words above the answer, so an aside reads as an exchange rather than as
                // a paragraph of story that arrived from nowhere.
                stack.Children.Add(
                    told.Asked is { Length: > 0 } asked
                        ? Stamped("You asked, ", told.At, $" — “{asked}”")
                        : Stamped("Aside — ", told.At));
            }

            if (told.Kind == AdventureToldKind.Beat && told.Speaker is { Length: > 0 } speaker)
            {
                stack.Children.Add(Text(speaker, TypeScale.Small, ThemeManager.GreyKey));
            }

            stack.Children.Add(Text(told.Text, TypeScale.Body));
            page.Children.Add(stack);
        }
    }

    /// <summary>
    /// The story so far from the definition, for a story that was flying before anything was being
    /// recorded.
    /// </summary>
    private static void Authored(StackPanel page, AdventureStanding standing, Func<string?, string?> named)
    {
        var adventure = standing.Adventure;

        if (adventure.OpeningLines.Count == 1 && named(adventure.OpeningSpeaker) is null)
        {
            page.Children.Add(Labelled("Opening", adventure.Opening));
        }
        else if (adventure.OpeningLines.Count > 0)
        {
            page.Children.Add(Labelled("Opening", null));

            foreach (var line in adventure.OpeningLines)
            {
                page.Children.Add(named(line.Speaker) is { } name ? Labelled(name, line.Text) : Text(line.Text, TypeScale.Body));
            }
        }

        var shown = adventure.IsBegun ? standing.Fired.Count : 0;

        for (var index = 0; index < shown && index < adventure.Beats.Count; index++)
        {
            var beat = adventure.Beats[index];
            page.Children.Add(Labelled(Stamped($"{beat.Title} — ", standing.Fired[index]), beat.Lines.Count == 1 && named(beat.Speaker) is null ? beat.Line : null));

            if (beat.Lines.Count > 1 || named(beat.Speaker) is not null)
            {
                foreach (var line in beat.Lines)
                {
                    page.Children.Add(named(line.Speaker) is { } name ? Labelled(name, line.Text) : Text(line.Text, TypeScale.Body));
                }
            }
        }
    }

    private static Button Action(string label, System.Action act, bool destructive = false)
    {
        var button = new Button { Content = label };
        button.Classes.Set("destructive", destructive);
        button.Click += (_, _) => act();
        return button;
    }

    /// <summary>A grey caption with its local time in mono between <paramref name="before"/> and <paramref name="after"/>.</summary>
    private static TextBlock Stamped(string before, DateTimeOffset at, string after = "")
    {
        var block = new TextBlock { FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
        block.Inlines = [new Run(before), Fonts.Mono(at.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.CurrentCulture))];

        if (after.Length > 0)
        {
            block.Inlines.Add(new Run(after));
        }

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    internal static TextBlock Text(string text, double size, string key = ThemeManager.WhiteKey)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    internal static TextBlock Muted(string text) => Text(text, TypeScale.Body, ThemeManager.GreyKey);

    internal static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
