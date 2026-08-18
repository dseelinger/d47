using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// The checklist, as a tab of the panel (list.md Phase 25, "The checklist leaves its window").
/// <para>
/// <b>The headline is not tidiness.</b> A <c>Window</c> cannot appear in the headset at all, so
/// until this moved a Commander in VR could not see their checklist — the one surface that
/// answers "what am I working on", unreachable from the place they spend the session. It also
/// wants the big area for the reason the popup never had it: the list runs to hundreds of lines.
/// </para>
/// <para>
/// <b>One list in the Commander's order</b>, with scope shown on each line rather than the page
/// being carved into separate lists. What gets reordered on a whim is everything being worked
/// on, not one ship's share of it — so <see cref="ChecklistScope"/> is a label and a filter and
/// <b>never a partition</b>.
/// </para>
/// <para>
/// <b>Colour is spent only where something is wrong.</b> A page where six things are coloured is
/// a page where none of them is noticed.
/// </para>
/// </summary>
public sealed class ChecklistPage : UserControl, IFilterablePage
{
    /// <summary>The filter entry that means no filter. The store's own word for it.</summary>
    public const string Everything = "everything";

    /// <summary>The crumb the suggestions page is pushed as.</summary>
    public const string SuggestionsKey = "checklist.suggestions";

    private readonly ChecklistService _checklists;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;

    /// <summary>
    /// The Commander's long arcs (list.md Phase 34, "The checklist points at the arc"). Null under
    /// the designer and in a configuration that tracks none, where the band simply is not drawn.
    /// </summary>
    private readonly D47.Core.Goals.GoalBook? _goals;

    private readonly Action? _backfill;

    private readonly Func<DateTimeOffset> _now;

    /// <summary>The arcs band, rebuilt with the page because an arc's figure moves with the journal.</summary>
    private readonly StackPanel _arcs = new() { Spacing = 4 };

    /// <summary>
    /// The band's window onto the arcs (remediation.md 11, item 4).
    /// <para>
    /// It used to be the <see cref="_arcs"/> stack itself, docked to the top of the page — and a
    /// docked child takes the height it asks for. Nine arcs ask for all of it, so opening the band
    /// left the checklist underneath nought pixels tall and clipped the third arc at the bottom
    /// edge, which is neither a list nor a readable band.
    /// </para>
    /// <para>
    /// Bounded to a share of the page rather than to a fixed number of arcs: what matters is that
    /// the list keeps a working share of the tab, and how many arcs fit in the rest is whatever the
    /// window is tall enough for. Below the cap it takes only what it needs, so a Commander with
    /// two arcs sees two arcs and no scrollbar.
    /// </para>
    /// </summary>
    private readonly ScrollViewer _band = new()
    {
        Name = "GoalsBand",

        // Outside the scroller, so the gap between the band and the list survives being scrolled
        // to the bottom. Inside it, the last arc came to rest against the first checklist line and
        // the two read as one list.
        Margin = new Thickness(0, 0, 0, 10),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        IsVisible = false,
    };

    private readonly Button _arcsButton = new()
    {
        Padding = new Thickness(12, 4),
        MinHeight = 30,
        IsVisible = false,
    };

    private readonly StackPanel _list = new() { Spacing = 4 };
    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    private readonly Button _scopeButton = new()
    {
        Padding = new Thickness(12, 4),
        MinHeight = 30,
    };

    private readonly Button _suggestions = new()
    {
        Padding = new Thickness(12, 4),
        MinHeight = 30,
        IsVisible = false,
    };

    /// <summary>
    /// Import and export, behind one button (remediation.md 10, item 15). One rather than two,
    /// because they are the same rare errand in two directions and two permanent buttons on a
    /// working bar is two things to read past every session.
    /// </summary>
    private readonly Button _transfer = new()
    {
        Content = "Transfer",
        Padding = new Thickness(12, 4),
        MinHeight = 30,
    };

    private string _chosen = Everything;

    /// <summary>What the surface's own search box is narrowing the page to. Empty for nothing.</summary>
    private string _query = string.Empty;

    /// <summary>
    /// Which line is selected, by id. <b>The movers belong to the selection</b> and to nothing
    /// else: a pair of arrows on every one of several hundred rows is several hundred controls a
    /// ray can hit by accident, and a row with permanent arrows reads as a row whose position is
    /// the interesting thing about it.
    /// </summary>
    private string? _selected;

    /// <summary>
    /// Whether the arcs are showing. <b>Closed by default</b>, because nine arcs above a working
    /// list is the list pushed off the page — the band is a header a Commander opens when they want
    /// to know where the year is going, not a permanent third of the tab.
    /// </summary>
    private bool _showArcs;

    /// <summary>
    /// The most of the page the arcs may take, as a share of it (remediation.md 11, item 4). Under
    /// half, because the list is what the tab is for and the band is a header opened over it.
    /// </summary>
    private const double BandShare = 0.45;

    /// <summary>
    /// What the list keeps whatever the band would like, in pixels: the filter row above it plus
    /// enough rows underneath to still be a list.
    /// <para>
    /// A share alone is not enough, because the row of buttons above the list costs the same fifty
    /// pixels on a tall window and a short one. On a short one a proportional band left the list
    /// with fifty-six pixels, which is a scrollbar and half a line. The band gives instead: it is
    /// the thing a Commander opened and can close again.
    /// </para>
    /// </summary>
    private const double ListKeeps = 150;

    /// <summary>Which arc is open, by key. Its next step and its two buttons belong to it.</summary>
    private string? _openArc;

    public ChecklistPage(
        ChecklistService checklists,
        PanelNavigator nav,
        PanelPrompts prompts,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfill = null,
        Func<DateTimeOffset>? now = null)
    {
        _checklists = checklists;
        _nav = nav;
        _prompts = prompts;
        _goals = goals;
        _backfill = backfill;
        _now = now ?? (() => DateTimeOffset.Now);

        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        // A chooser rather than a combo box, and declared as a layer rather than as a page: the
        // scopes on a working list are a handful, and taking the whole panel to answer a question
        // the Commander did not think of as one is a level of navigation for nothing (list.md
        // Phase 25, "Page or layer is declared per call site").
        _scopeButton.Click += (_, _) => ChooseScope();

        _suggestions.Click += (_, _) =>
            _nav.Drill(new NavCrumb(SuggestionsKey, "Suggestions"));

        _arcsButton.Click += (_, _) =>
        {
            _showArcs = !_showArcs;
            Rebuild();
        };

        var add = new Button { Content = "Add a line", Padding = new Thickness(12, 4), MinHeight = 30 };
        add.Click += (_, _) => AddLine();

        // Import and export (remediation.md 10, item 15). Beside the filter rather than beside
        // "Add a line": adding is what a Commander does to this list every session, and moving the
        // whole thing to another machine is what they do once.
        _transfer.Click += (_, _) => ChooseTransfer();

        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _suggestions, add },
        };

        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);

        // The arcs live beside the scope filter rather than above the whole page: they are another
        // way of reading the same list, which is what the bar is for.
        bar.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _scopeButton, _arcsButton, _transfer },
        });

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);

        _problems.Margin = new Thickness(0, 0, 0, 10);

        _band.Content = _arcs;

        DockPanel.SetDock(_band, Dock.Top);

        // A share of the page, so the list keeps a working share of the tab whatever the window is
        // doing. Recomputed on resize rather than set once: this panel is instantiated for the
        // desktop window and again for the headset, and those are very different heights.
        SizeChanged += (_, e) => _band.MaxHeight = Math.Max(
            0,
            Math.Min(e.NewSize.Height * BandShare, e.NewSize.Height - ListKeeps));

        root.Children.Add(bar);
        root.Children.Add(_band);
        root.Children.Add(_problems);
        root.Children.Add(new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        Content = root;

        Rebuild();
    }

    /// <summary>
    /// Starts listening, and catches up on anything missed while this page was not on screen
    /// (remediation.md 11, item 3).
    /// <para>
    /// <b>Paired with the detach below, and it has to be.</b> The subscription used to be made in
    /// the constructor and dropped on detach, which is not a pair: drilling into Suggestions
    /// reflows the tab into two panes and reparents this page, so it detached, unsubscribed, and
    /// was deaf for the rest of the session. A "Yes" heard out loud then changed the list and the
    /// page went on showing what it had.
    /// </para>
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _checklists.List.Changed += OnChanged;
        _checklists.Proposals.Changed += OnChanged;

        if (_goals is not null)
        {
            _goals.Store.Changed += OnChanged;
        }

        // The world moved while this was off screen, which is the ordinary case for a page that is
        // reparented by a reflow.
        Rebuild();
    }

    /// <summary>
    /// The surface's one search box, narrowing this page (list.md Phase 12). A filter rather than
    /// a highlight, because a checklist is a list of things rather than a body of text — which is
    /// exactly the per-page difference the one search affordance exists to allow.
    /// </summary>
    /// <summary>A list of lines is always narrowable.</summary>
    public bool Filters => true;

    public void Filter(string? query)
    {
        _query = (query ?? string.Empty).Trim();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _checklists.List.Changed -= OnChanged;
        _checklists.Proposals.Changed -= OnChanged;

        if (_goals is not null)
        {
            _goals.Store.Changed -= OnChanged;
        }
    }

    /// <summary>
    /// The suggestions page, built for the crumb the button above pushes (list.md Phase 25).
    /// <para>
    /// <b>Suggestions are a page rather than an interruption.</b> Everything
    /// <see cref="ChecklistProposals"/> is holding waits in one place, and accepting stays the
    /// Commander's own act — reachable from here and from a spoken yes, and from nothing else,
    /// because accepting is <c>Protected</c>.
    /// </para>
    /// </summary>
    public Control BuildSuggestions()
    {
        var page = new StackPanel { Spacing = 8, Margin = new Thickness(14) };

        void Fill()
        {
            page.Children.Clear();

            var pending = _checklists.Proposals.PendingFor(_checklists.Document.CommanderFid);

            if (pending.Count == 0)
            {
                page.Children.Add(Muted(
                    "Nothing waiting. D47 puts proposals here rather than on your list, and they stay "
                    + "here until you accept them."));

                return;
            }

            foreach (var proposal in pending)
            {
                page.Children.Add(Proposal(proposal, Fill));
            }
        }

        Fill();

        var scroller = new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        // The store raises this from whichever thread wrote, so the hop is not optional.
        void Follow() => Dispatcher.UIThread.Post(Fill);

        // <b>Accepting from this page refreshed it and nothing else did</b>, which is why the gap
        // was invisible until a spoken yes accepted the same proposal from somewhere else: the
        // card stayed on screen after the line was already on the list (remediation.md 11, item 3).
        //
        // On attach rather than here, and dropped on detach, so the page is not holding a handler
        // after it has been navigated away from — and so it catches up on whatever happened while
        // it was gone.
        scroller.AttachedToVisualTree += (_, _) =>
        {
            _checklists.Proposals.Changed += Follow;
            Fill();
        };

        scroller.DetachedFromVisualTree += (_, _) => _checklists.Proposals.Changed -= Follow;

        return scroller;
    }

    /// <summary>
    /// The store raises this from whichever thread wrote — the tick loop, most often — and every
    /// control here belongs to the UI thread, so the hop is not optional.
    /// </summary>
    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        _list.Children.Clear();

        var document = _checklists.Document;
        var waiting = _checklists.Proposals.PendingFor(document.CommanderFid).Count;

        _suggestions.IsVisible = waiting > 0;
        _suggestions.Content = $"Suggestions ({waiting})";

        _scopeButton.Content = _chosen == Everything ? "Showing everything" : $"Showing {_chosen}";

        RebuildArcs();

        var live = document.Items.Where(item => item.IsLive && Matches(item)).ToList();

        var open = live.Where(item => !item.IsComplete).ToList();
        var done = live.Where(item => item.IsComplete).ToList();

        if (live.Count == 0)
        {
            _list.Children.Add(Muted(_query.Length > 0 || _chosen != Everything
                ? "Nothing on your list matches that."
                : "Nothing here yet. Say \"add buy limpets to my checklist\", or ask for a build plan, "
                  + "and D47 will propose it."));
        }

        // One list, in the Commander's order, and no headings between scopes. Scope rides each
        // line instead: carving the page into per-ship lists would make the order several orders,
        // and the thing being ordered is everything being worked on.
        foreach (var item in open)
        {
            _list.Children.Add(Line(item));
        }

        if (done.Count > 0)
        {
            // Below the line and counted, never removed. Seeing how far you have come is most of
            // the point of a list that runs for weeks.
            _list.Children.Add(Heading($"Done ({done.Count})"));

            foreach (var item in done)
            {
                _list.Children.Add(Line(item));
            }
        }

        var tombstoned = document.Items.Count(item => !item.IsLive);

        if (tombstoned > 0)
        {
            _list.Children.Add(Muted(
                $"{tombstoned} item{(tombstoned == 1 ? string.Empty : "s")} dropped by a later version of a "
                + "plan, kept so you can see what changed."));
        }

        ShowProblems();
    }

    /// <summary>
    /// The arcs band (list.md Phase 34, "The checklist points at the arc").
    /// <para>
    /// <b>Here rather than on a tab of its own</b>, because the join is the feature: a goal that
    /// takes months and the lines it produced this week belong on one page, and putting them on two
    /// would make the connection something a Commander has to remember rather than something they
    /// can see.
    /// </para>
    /// </summary>
    private void RebuildArcs()
    {
        _arcs.Children.Clear();

        if (_goals is null)
        {
            _arcsButton.IsVisible = false;
            _band.IsVisible = false;
            return;
        }

        var standings = _goals.Standings;
        var running = standings.Count(standing => !standing.IsDone);

        _arcsButton.IsVisible = true;
        _arcsButton.Content = _showArcs ? "Hide goals" : $"Goals ({running} running)";
        _band.IsVisible = _showArcs;

        if (!_showArcs)
        {
            return;
        }

        if (standings.Count == 0)
        {
            _arcs.Children.Add(Muted("Every goal is set aside."));
            return;
        }

        foreach (var standing in standings)
        {
            _arcs.Children.Add(Arc(standing));
        }

        // The button that gives every arc its age. A press rather than anything automatic: it reads
        // every journal on the disk, and that is a thing a person asks for.
        if (_goals.Mine is null && _backfill is not null)
        {
            var read = new Button
            {
                Content = "Read my journals",
                Padding = new Thickness(12, 4),
                MinHeight = 30,
                Margin = new Thickness(0, 4, 0, 0),
            };

            read.Click += (_, _) => _backfill();

            _arcs.Children.Add(Muted(
                "Nothing here has an age yet, and the milestones have no figures. Reading back "
                + "through the journals already on this disk is what gives them one."));

            _arcs.Children.Add(read);
        }
    }

    /// <summary>One arc: what it is, how far along, how long it has run — and, when open, what to do about it.</summary>
    private Control Arc(D47.Core.Goals.GoalStanding standing)
    {
        var open = standing.Arc.Key == _openArc;
        var body = new StackPanel { Spacing = 2 };

        body.Children.Add(new TextBlock
        {
            Text = standing.IsDone ? "✓  " + standing.Arc.Name : standing.Arc.Name,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        // The figure, then the bar — and no bar at all where the fraction is unknown. A bar drawn
        // at nothing is the one thing item 2 forbids: it reads as no progress rather than as no
        // answer.
        body.Children.Add(Muted(Aside(standing)));

        if (standing.Fraction is { } fraction)
        {
            body.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Value = fraction,
                Height = 4,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        if (open)
        {
            body.Children.Add(Step(standing));
        }

        var card = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = body,
            MinHeight = 34,
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(
            card,
            Border.BorderBrushProperty,
            open ? ThemeManager.AccentKey : ThemeManager.SurfaceAltKey);

        AutomationProperties.SetName(card, standing.Arc.Name);

        card.PointerPressed += (_, _) =>
        {
            _openArc = open ? null : standing.Arc.Key;
            Rebuild();
        };

        return card;
    }

    /// <summary>The caption under an arc: where it stands, where the figure came from, and its age.</summary>
    private string Aside(D47.Core.Goals.GoalStanding standing)
    {
        var parts = new List<string>();

        if (standing.IsDone)
        {
            parts.Add("done");
        }
        else if (standing.Note is { Length: > 0 } note)
        {
            parts.Add(note);
        }
        else if (standing.Have is { } have && standing.Need is { } need)
        {
            parts.Add($"{have:N0} of {need:N0}");
        }
        else
        {
            // Never "0". An arc nothing can currently evaluate has to read as one.
            parts.Add("not known yet");
        }

        if (standing.Source == D47.Core.Goals.GoalSource.Mined && standing.AsOf is { } stamp)
        {
            parts.Add($"as of {stamp:d MMM yyyy}");
        }

        if (!standing.IsDone && standing.Age(_now()) is { TotalDays: >= 1 } age)
        {
            parts.Add($"running {(int)age.TotalDays} days");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// What to do about this arc today, and the two things a Commander can do about the arc itself.
    /// <para>
    /// <b>Adding is a proposal.</b> It goes to the suggestions page like everything else, so
    /// accepting stays their act rather than a button that writes their list.
    /// </para>
    /// </summary>
    private Control Step(D47.Core.Goals.GoalStanding standing)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };

        var step = _goals?.Next(standing.Arc.Key);

        panel.Children.Add(new TextBlock
        {
            Text = step?.Say ?? standing.Arc.Done,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (step is { CanPropose: true })
        {
            var promote = new Button
            {
                Content = "Suggest a line",
                Padding = new Thickness(12, 4),
                MinHeight = 30,
            };

            promote.Click += (_, _) =>
            {
                Say(_goals!.Promote(standing.Arc.Key));
                Rebuild();
            };

            buttons.Children.Add(promote);
        }

        var aside = new Button
        {
            Content = "Set aside",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
        };

        aside.Click += (_, _) =>
        {
            Say(_goals!.SetAside(standing.Arc.Key, aside: true));
            _openArc = null;
            Rebuild();
        };

        buttons.Children.Add(aside);
        panel.Children.Add(buttons);

        return panel;
    }

    private bool Matches(ChecklistItem item)
    {
        if (_query.Length > 0
            && !item.Text.Contains(_query, StringComparison.OrdinalIgnoreCase)
            && !item.Scope.ToString().Contains(_query, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_chosen == Everything)
        {
            return true;
        }

        return _chosen.Equals(item.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
               || _chosen.Equals(item.Source.ToString(), StringComparison.OrdinalIgnoreCase)
               || _chosen.Equals(ChecklistScope.Word(item.Scope.Group), StringComparison.OrdinalIgnoreCase)
               || _chosen.Equals(item.IsComplete ? "complete" : "open", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One line: what it is, which scope it belongs to, and — when it is the selected one — the
    /// pair of movers.
    /// </summary>
    private Control Line(ChecklistItem item)
    {
        var id = item.Id.ToString();
        var selected = id == _selected;

        var body = new StackPanel { Spacing = 2 };

        if (item.TicksByHand)
        {
            var tick = new CheckBox { Content = item.Text, IsChecked = item.IsComplete };

            tick.Click += (_, _) =>
            {
                var change = tick.IsChecked == true
                    ? _checklists.Complete(item.Id)
                    : _checklists.Uncomplete(item.Id);

                if (!change.Changed)
                {
                    Say(change.Report);
                }
            };

            body.Children.Add(tick);
        }
        else
        {
            // No checkbox at all, rather than a disabled one. A greyed-out tick still asserts
            // that ticking is the mechanism here, and it is not: the next journal read would
            // either undo it or leave it standing and lying.
            body.Children.Add(new TextBlock
            {
                Text = (item.IsComplete ? "✓  " : "•  ") + item.Text,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        // Scope on the line rather than as a heading over a group of them.
        var aside = new List<string> { item.Scope.ToString() };

        // And the arc it came from, where one proposed it (list.md Phase 34). Without this,
        // finishing the line visibly moves nothing bigger than itself, which is the phase's own
        // statement of why the join is worth having.
        if (item.Goal is { Length: > 0 } goal)
        {
            // Through the book rather than the catalogue, so a line from a goal the Commander
            // invented names it as they wrote it rather than as a key.
            aside.Add("towards " + (_goals?.Find(goal)?.Arc.Name ?? goal));
        }

        if (ChecklistNextAction.For(item.State) is { } next)
        {
            aside.Add(next);
        }
        else if (!item.TicksByHand && _checklists.Verdict(item)?.Reason is { } reason)
        {
            aside.Add(reason);
        }

        var caption = Muted(string.Join(" · ", aside));

        // Colour only where something is wrong. Everything else — open, done, worked out from
        // the journal — is the ordinary condition of a list that runs for weeks.
        if (ChecklistNextAction.IsWrong(item.State))
        {
            Themed(caption, TextBlock.ForegroundProperty, ThemeManager.DangerKey);
        }

        body.Children.Add(caption);

        var row = new DockPanel();

        if (selected)
        {
            row.Children.Add(Movers(item));
        }

        row.Children.Add(body);

        var card = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = row,

            // Tall enough for a ray at a metre. A line a Commander has to aim at is a line they
            // press twice, and the second press is on the one below it.
            MinHeight = 34,
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(
            card,
            Border.BorderBrushProperty,
            selected ? ThemeManager.AccentKey : ThemeManager.SurfaceAltKey);

        // Selecting is what grows the movers, so the whole card takes the press rather than a
        // handle somewhere on it.
        card.PointerPressed += (_, _) =>
        {
            _selected = selected ? null : id;
            Rebuild();
        };

        return card;
    }

    /// <summary>
    /// Up and down, on the selected line.
    /// <para>
    /// <b>Reordering is not a drag</b> — a drag is the worst gesture available to a ray at a
    /// metre, needing a press, an aim held through motion and a release, any one of which a hand
    /// at arm's length gets wrong. And the phrase does it without aiming at all, which a drag has
    /// no spoken form of.
    /// </para>
    /// </summary>
    private Control Movers(ChecklistItem item)
    {
        var up = new Button
        {
            Content = "▲",
            Padding = new Thickness(10, 2),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var down = new Button
        {
            Content = "▼",
            Padding = new Thickness(10, 2),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        up.Click += (_, _) => Moved(item, -1);
        down.Click += (_, _) => Moved(item, 1);

        AutomationProperties.SetName(up, "Move up");
        AutomationProperties.SetName(down, "Move down");

        var movers = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { up, down },
        };

        // Rewording and removing belong to the selection for the same reason the movers do: four
        // controls on every one of several hundred rows is four hundred things a ray can hit by
        // accident (remediation.md 10, items 13 and 14).
        //
        // Only on a line the Commander wrote. A derived line's words are the plan's words and
        // dropping it is a revision of the plan, so both controls would be offers d47 then has to
        // refuse -- and a control that exists to say no is worse than one that is not there.
        if (item.Kind == ChecklistItemKind.Authored)
        {
            var edit = new Button
            {
                Content = "Edit",
                Padding = new Thickness(10, 2),
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var drop = new Button
            {
                Content = "Delete",
                Padding = new Thickness(10, 2),
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };

            edit.Click += (_, _) => EditLine(item);
            drop.Click += (_, _) => DeleteLine(item);

            AutomationProperties.SetName(edit, "Edit this line");
            AutomationProperties.SetName(drop, "Delete this line");

            movers.Children.Add(edit);
            movers.Children.Add(drop);
        }

        DockPanel.SetDock(movers, Dock.Right);
        return movers;
    }

    /// <summary>
    /// Rewords a line the Commander wrote (remediation.md 10, item 13).
    /// <para>
    /// The same prompt <see cref="AddLine"/> uses, opened on the keyboard rather than on the
    /// microphone and carrying what the line already says. Correcting a word is not the same act
    /// as writing a sentence: the value already exists and most of it is right, which is the case
    /// the keyboard is better at and speech is worst at.
    /// </para>
    /// </summary>
    private void EditLine(ChecklistItem item)
    {
        _prompts.Enter(
            new EntryRequest(
                "checklist.edit",
                "Edit",
                "Edit this line",
                "Your own words. The line keeps its place, its tick and whatever it came from.",
                item.Text,
                EntrySurface.Keyboard,
                value => string.IsNullOrWhiteSpace(value)
                    ? EntryVerdict.No("A line with nothing written on it is not a line.")
                    : EntryVerdict.Ok),
            value =>
            {
                var change = _checklists.Reword(item.Id, value);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                Rebuild();
            });
    }

    /// <summary>
    /// Takes a line off the list (remediation.md 10, item 14).
    /// <para>
    /// <b>Asked first, and asked as a chooser rather than a dialog</b> — a popup cannot exist in
    /// the VR path at all. Deleting is the one control here with no way back: a tick can be
    /// un-ticked and a move can be moved again, and this cannot, which is exactly the gap a ray at
    /// a metre falls into.
    /// </para>
    /// </summary>
    private void DeleteLine(ChecklistItem item)
    {
        _prompts.Choose(
            new ChoiceRequest(
                "checklist.delete",
                "Delete",
                "Delete this line",
                $"\"{item.Text}\" would come off the list. There is no way back from this one.",
                [new ChoiceOption("keep", "Keep it"), new ChoiceOption("delete", "Delete it")],
                "keep",
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "delete")
                {
                    return;
                }

                var change = _checklists.Delete(item.Id);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                // The line that was selected has gone, so the selection goes with it rather than
                // pointing at an id nothing answers to.
                _selected = null;
                Rebuild();
            });
    }

    private void Moved(ChecklistItem item, int by)
    {
        var change = _checklists.Move(item.Id, by);

        if (!change.Changed)
        {
            Say(change.Report);
            return;
        }

        Rebuild();
    }

    /// <summary>
    /// The scope filter, as a chooser drawn into the panel's layer rather than as a dropdown.
    /// A dropdown is a popup, and a popup cannot exist in the VR path at all.
    /// </summary>
    private void ChooseScope()
    {
        var options = new List<ChoiceOption>
        {
            new(Everything, "Everything"),
        };

        options.AddRange(_checklists.Filters().Select(filter => new ChoiceOption(filter, filter)));

        _prompts.Choose(
            new ChoiceRequest(
                "checklist.scope",
                "Show",
                "Show",
                "Scope is a label and a filter, never a partition — your order is one order across "
                + "all of them.",
                options,
                _chosen,
                ChoiceSurface.Layer)
            {
                CurrentWord = "showing now",
            },
            option =>
            {
                _chosen = option.Key;
                Rebuild();
            });
    }

    /// <summary>
    /// Import or export, asked as a chooser (remediation.md 10, item 15).
    /// </summary>
    private void ChooseTransfer()
    {
        _prompts.Choose(
            new ChoiceRequest(
                "checklist.transfer",
                "Transfer",
                "Import or export",
                "The whole list, as JSON — every line, what it came from, and the ones you have "
                + "finished with. For moving to another machine.",
                [new ChoiceOption("export", "Export to a file"), new ChoiceOption("import", "Import from a file")],
                "export",
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key == "export")
                {
                    _ = ExportAsync();
                }
                else
                {
                    _ = ImportAsync();
                }
            });
    }

    /// <summary>
    /// Writes the list out.
    /// <para>
    /// Through the Commander's own file picker where there is one, and beside the executable where
    /// there is not. <b>The headset is the second case</b>: its host window is never shown, so it
    /// has no storage provider to open a picker from — and an export that simply does not work in
    /// VR would be an export that works on the surface that already has a text editor and fails on
    /// the one that does not.
    /// </para>
    /// </summary>
    private async Task ExportAsync()
    {
        var json = _checklists.Export();
        var suggested = $"d47-checklist-{_now():yyyy-MM-dd}.json";

        try
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is { CanSave: true } storage)
            {
                var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Export the checklist",
                    SuggestedFileName = suggested,
                    DefaultExtension = "json",
                    FileTypeChoices = [ChecklistFiles],
                });

                if (file is null)
                {
                    return;
                }

                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);

                Say($"Exported to {file.Name}.");
                return;
            }

            var path = Path.Combine(
                Path.GetDirectoryName(_checklists.List.Path) ?? ".",
                suggested);

            await File.WriteAllTextAsync(path, json);
            Say($"Exported to {path}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Said rather than thrown. A file that would not write is the Commander's disk telling
            // them something, and it is not a reason for the panel to fall over.
            Say($"Could not write the file: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads one back, replacing this Commander's list.
    /// <para>
    /// Confirmed first, because it is a replacement rather than a merge and there is no way back
    /// from it — the same reason deleting one line asks.
    /// </para>
    /// </summary>
    private async Task ImportAsync()
    {
        string json;
        string what;

        try
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is { CanOpen: true } storage)
            {
                var picked = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Import a checklist",
                    AllowMultiple = false,
                    FileTypeFilter = [ChecklistFiles],
                });

                if (picked is not [{ } file])
                {
                    return;
                }

                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);

                json = await reader.ReadToEndAsync();
                what = file.Name;
            }
            else
            {
                var path = Path.Combine(
                    Path.GetDirectoryName(_checklists.List.Path) ?? ".",
                    "checklist-import.json");

                if (!File.Exists(path))
                {
                    Say($"Put the file at {path} and ask again. There is no file picker on this surface.");
                    return;
                }

                json = await File.ReadAllTextAsync(path);
                what = "checklist-import.json";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Say($"Could not read the file: {ex.Message}");
            return;
        }

        _prompts.Choose(
            new ChoiceRequest(
                "checklist.import.confirm",
                "Import",
                "Replace your checklist?",
                $"Everything on your list would be replaced by what is in {what}. There is no way "
                + "back from this one.",
                [new ChoiceOption("keep", "Keep what I have"), new ChoiceOption("replace", "Replace it")],
                "keep",
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "replace")
                {
                    return;
                }

                var change = _checklists.Import(json);

                Say(change.Report);

                if (change.Changed)
                {
                    _selected = null;
                    Rebuild();
                }
            });
    }

    /// <summary>What both pickers filter on. One declaration, so they cannot disagree.</summary>
    private static readonly Avalonia.Platform.Storage.FilePickerFileType ChecklistFiles = new("Checklist")
    {
        Patterns = ["*.json"],
    };

    /// <summary>
    /// The Commander's own line, said or typed. Voice first, because a checklist line is a
    /// sentence and a sentence is far easier said than hunted for one key at a time.
    /// </summary>
    private void AddLine()
    {
        _prompts.Enter(
            new EntryRequest(
                "checklist.add",
                "Add",
                "Add a line",
                "Your own note. It gets a checkbox, because it is yours to tick.",
                string.Empty,
                EntrySurface.Voice,
                value => string.IsNullOrWhiteSpace(value)
                    ? EntryVerdict.No("There was nothing to add.")
                    : EntryVerdict.Ok),
            value =>
            {
                var change = _checklists.AddNote(_checklists.ScopeFor(null, null), value);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                Rebuild();
            });
    }

    /// <summary>
    /// One proposal, with what it would do to the list.
    /// <para>
    /// <b>A revision arrives as a diff</b>, so an ordering the Commander spent time on survives a
    /// plan change: the summary says what is added and what is dropped rather than presenting a
    /// new list to replace the old one.
    /// </para>
    /// </summary>
    private Control Proposal(ChecklistProposal proposal, Action refresh)
    {
        var accept = new Button { Content = "Accept", Padding = new Thickness(14, 4), MinHeight = 30 };

        accept.Click += (_, _) =>
        {
            Say(_checklists.Accept(proposal.Id));
            refresh();
        };

        var decline = new Button { Content = "Decline", Padding = new Thickness(14, 4), MinHeight = 30 };

        decline.Click += (_, _) =>
        {
            Say(_checklists.Decline(proposal.Id));
            refresh();
        };

        var body = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = proposal.Summary, TextWrapping = TextWrapping.Wrap },
                Muted("D47 proposed this and cannot accept it itself."),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { accept, decline },
                },
            },
        };

        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = body,
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(card, Border.BorderBrushProperty, ThemeManager.AccentKey);

        return card;
    }

    private void ShowProblems()
    {
        var problems = _checklists.List.Problems.Concat(_checklists.Proposals.Problems).ToList();

        _problems.IsVisible = problems.Count > 0;
        _problems.Text = string.Join("\n", problems.Select(problem => $"{problem.Where}: {problem.Reason}"));
    }

    /// <summary>
    /// A refusal is shown where the problems are, not swallowed. Trying to tick a computed item
    /// has to say why rather than quietly doing nothing.
    /// </summary>
    private void Say(string message)
    {
        Rebuild();
        _problems.IsVisible = true;
        _problems.Text = message;
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 12, 0, 2),
    };

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
