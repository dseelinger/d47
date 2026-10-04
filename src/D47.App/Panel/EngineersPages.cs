using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Engineers root of the Asset Mgmt tab (Phase 28).</summary>
public static class EngineersPages
{
    /// <summary>The root: everybody, in the order they can be acted on.</summary>
    public const string DirectoryRoot = "engineers.directory";

    /// <summary>The solver, a level drilled from the directory.</summary>
    public const string RouteKey = "engineers.route";

    /// <summary>The crumb the directory's Route tile pushes.</summary>
    public static NavCrumb RouteCrumb { get; } =
        new(RouteKey, "Route") { Help = D47.Core.Capabilities.Builtin.EngineerCapability.Id };

    /// <summary>Whether a crumb is one of these pages rather than another root's.</summary>
    public static bool Owns(NavCrumb crumb) => crumb.Key.StartsWith("engineers.", StringComparison.Ordinal);

    /// <summary>How one engineer's crumb is keyed, so a page rebuilds from the trail alone.</summary>
    public const string WhoPrefix = "engineers.who:";

    /// <summary>Draws whichever level a crumb names.</summary>
    public static Control Build(
        NavCrumb crumb,
        EngineerSource source,
        PanelNavigator nav,
        EngineerDirectoryMemory? memory = null,
        Func<string, Task<bool>>? copy = null)
    {
        if (crumb.Key.StartsWith(WhoPrefix, StringComparison.Ordinal))
        {
            return new EngineerPage(source, crumb.Key[WhoPrefix.Length..], nav, copy);
        }

        return crumb.Key == RouteKey
            ? new EngineerRoutePage(source, nav, memory)
            : new EngineerDirectoryPage(source, nav, memory);
    }

    /// <summary>Whether the two checkbox filters leave this engineer on the list (#132).</summary>
    internal static bool Shown(EngineerDirectoryMemory? memory, Engineer engineer) =>
        memory is null || !memory.Hides(engineer);

    /// <summary>The crumb for one engineer, keyed on the id the journal writes rather than a name.</summary>
    public static NavCrumb Crumb(Engineer engineer) =>
        new(WhoPrefix + engineer.Id.ToString(CultureInfo.InvariantCulture), engineer.Name)
        {
            Level = WhoPrefix,
        };

    /// <summary>An engineer's name, pressable, wherever it is shown (remediation.md 12, item 7).</summary>
    internal static Control Name(
        Engineer engineer, PanelNavigator nav, double size, FontWeight weight = FontWeight.Normal)
    {
        var label = new TextBlock
        {
            Text = engineer.Name,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
        };

        LoadoutPages.Themed(label, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var button = new Button
        {
            Content = label,
            Background = Brushes.Transparent,
            Padding = new Thickness(0, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinHeight = 30,
        };

        button.Click += (_, _) => nav.Drill(Crumb(engineer));

        return button;
    }

    /// <summary>What the directory's mark means: modules still to engineer this engineer could finish.</summary>
    internal static string Wanted(int many) =>
        many == 0 ? string.Empty : EngineerSay.Count(many, "module", "modules");

    /// <summary>
    /// The control that adds every unmet line of an engineer's prerequisites to the checklist in one
    /// press, or null where every line is already met — there is no per-line control (#257).
    /// </summary>
    internal static Button? AddPrerequisitesControl(
        Engineer engineer, IReadOnlyList<UnlockCriterion> criteria, EngineerSource source, Action refresh)
    {
        if (criteria.Count == 0 || criteria.All(criterion => criterion.Met == true))
        {
            return null;
        }

        return LoadoutPages.Press("Add to checklist", () =>
        {
            source.AddPrerequisites(engineer);
            refresh();
        });
    }

    /// <summary>
    /// The "Unlock Prerequisites" section heading, with the add control on the same row where there is
    /// one to add (#257).
    /// </summary>
    internal static Control PrerequisitesHeader(
        Engineer engineer,
        IReadOnlyList<UnlockCriterion> criteria,
        EngineerSource source,
        Action refresh,
        bool compact = false)
    {
        if (AddPrerequisitesControl(engineer, criteria, source, refresh) is not { } button)
        {
            return LoadoutPages.Section("Unlock Prerequisites", compact);
        }

        var heading = new SelectableTextBlock { VerticalAlignment = VerticalAlignment.Bottom };

        TitleText.Style(heading, TypeScale.Section, TitleRank.Group);
        TitleText.Show(heading, "Unlock Prerequisites");

        var row = new DockPanel();
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(button);
        row.Children.Add(heading);

        var section = TitleText.GroupRow(row);
        section.Margin = compact ? new Thickness(0, 4, 0, 2) : new Thickness(0, 14, 0, 6);

        return section;
    }

    /// <summary>
    /// A criterion's word on the status ladder and its ink — the same words wherever a criterion is shown
    /// (#126).
    /// </summary>
    internal static (string Word, string Key) Ladder(UnlockCriterion criterion) => criterion switch
    {
        { Met: true } => ("✓ MET", ThemeManager.BlueKey),
        { Met: null } => ("? UNKNOWN", ThemeManager.WhiteKey),
        { Measure.Fill: > 0 } => ("IN PROGRESS", ThemeManager.AKey),
        _ => ("NOT MET", ThemeManager.GreyKey),
    };

    /// <summary>One prerequisite as a Slab tile: its ladder word, the criterion, and its reading and gauge where it has them.</summary>
    internal static Control CriterionLine(UnlockCriterion criterion)
    {
        var (word, key) = Ladder(criterion);

        var box = new TextBlock
        {
            Text = word,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        LoadoutPages.Themed(box, TextBlock.ForegroundProperty, key);

        var said = new SelectableTextBlock
        {
            Text = criterion.Text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        LoadoutPages.Themed(said, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(96, GridUnitType.Pixel), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Auto)],
            ColumnSpacing = 12,
            RowSpacing = 4,
        };

        grid.Children.Add(box);
        Grid.SetColumn(said, 1);
        grid.Children.Add(said);

        var below = new List<Control>();

        if (criterion.Reading is { Length: > 0 } reading)
        {
            below.Add(LoadoutPages.Toned(reading, ThemeManager.AKey));
        }

        // A met line draws full even where the reading behind it fell short of the target — the journal
        // may have settled it some other way (#17).
        if (criterion.Measure is { } measure)
        {
            below.Add(LoadoutPages.MeasureBar(criterion.Met == true ? 1 : measure.Fill));
        }

        foreach (var child in below)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(child, grid.RowDefinitions.Count - 1);
            Grid.SetColumn(child, 1);
            grid.Children.Add(child);
        }

        var tile = new Border { Padding = new Thickness(14, 10), Child = grid };
        LoadoutPages.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    /// <summary>Criteria as ladder tiles, 2px apart.</summary>
    internal static Control Criteria(IEnumerable<UnlockCriterion> criteria)
    {
        var stack = new StackPanel { Spacing = StatTile.Gap };

        foreach (var criterion in criteria)
        {
            stack.Children.Add(CriterionLine(criterion));
        }

        return stack;
    }
}

/// <summary>
/// A page that redraws when the plans or the Commander's position move underneath it, and lets go of
/// the source when it is detached — the same arrangement <see cref="LoadoutPage"/> makes, and
/// forgetting it once is a page that keeps redrawing after its tab has gone.
/// </summary>
public abstract class EngineerPageBase : UserControl
{
    protected EngineerPageBase(EngineerSource source) => Source = source;

    private IDisposable? _sized;

    protected EngineerSource Source { get; }

    /// <summary>Whether the panel is drawing mini, where headings are compact.</summary>
    protected bool Mini { get; private set; }

    protected abstract void Refresh();

    /// <summary>The page's line for the title line; a root page declares <see cref="IPageSummary"/>.</summary>
    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    protected void Summarise(string summary)
    {
        if (summary == Summary)
        {
            return;
        }

        Summary = summary;
        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Attach to detach, for the reason <see cref="LoadoutPage"/> spells out (remediation.md 13, item
    /// 1).
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Source.Changed += OnChanged;

        var panel = this.GetSelfAndVisualAncestors().OfType<PanelView>().FirstOrDefault();
        Mini = panel?.Mode == PanelMode.Mini;

        Refresh();

        _sized = panel?.GetObservable(PanelView.ModeProperty).Subscribe(new AnonymousObserver<PanelMode>(OnSurface));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Source.Changed -= OnChanged;
        _sized?.Dispose();
        _sized = null;
    }

    private void OnSurface(PanelMode mode)
    {
        var mini = mode == PanelMode.Mini;

        if (mini != Mini)
        {
            Mini = mini;
            Refresh();
        }
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);
}

/// <summary>The directory (Phase 28, "Who can roll this").</summary>
public sealed class EngineerDirectoryPage : EngineerPageBase, IFilterablePage, IPageSummary
{
    private readonly PanelNavigator _nav;

    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly EngineerDirectoryMemory? _memory;

    private readonly CheckBox _colonia;
    private readonly CheckBox _onFoot;

    private string? _query;

    public EngineerDirectoryPage(EngineerSource source, PanelNavigator nav, EngineerDirectoryMemory? memory = null)
        : base(source)
    {
        _nav = nav;
        _memory = memory;

        _colonia = LabeledCheckBox.Caps("Hide the Colonia eight");
        _onFoot = LabeledCheckBox.Caps("Hide on-foot engineers");

        _colonia.IsChecked = memory?.HideColonia ?? false;
        _onFoot.IsChecked = memory?.HideOnFoot ?? false;

        // The checkbox owns the flag rather than mirroring it back — the same reason the goals band's
        // toggle does (ChecklistPage._arcsToggle).
        _colonia.IsCheckedChanged += (_, _) =>
        {
            _memory?.RememberColonia(_colonia.IsChecked == true);
            Refresh();
        };

        _onFoot.IsCheckedChanged += (_, _) =>
        {
            _memory?.RememberOnFoot(_onFoot.IsChecked == true);
            Refresh();
        };

        var checks = new WrapPanel
        {
            ItemSpacing = 2,
            LineSpacing = 2,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { _colonia, _onFoot, LoadoutPages.Press("Route", () => nav.Drill(EngineersPages.RouteCrumb)) },
        };

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("who should I unlock next");

        DockPanel.SetDock(checks, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(checks);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        Content = root;

        Refresh();
    }

    /// <summary>
    /// Thirty-eight people with ten specialities each is a scroll hunt, so the query takes rows away
    /// rather than only colouring them.
    /// </summary>
    public bool Filters => true;

    public void Filter(string? query)
    {
        _query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        Refresh();
    }

    protected override void Refresh()
    {
        var report = Source.Read();

        Summarise(report.Summary());
        _list.Children.Clear();

        var shown = report.Directory.Where(Matches).Where(NotHidden).ToList();

        if (shown.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(_query is null
                ? "No engineers on record, which should not be possible."
                : $"Nothing matches “{_query}”."));

            return;
        }

        var group = (EngineerReach?)null;

        foreach (var entry in shown)
        {
            if (group != entry.Reach)
            {
                group = entry.Reach;
                _list.Children.Add(ListRow.Head(Caption(entry.Reach)));
            }

            var line = entry.Engineer.Name;
            var notes = new List<string>();

            if (EngineersPages.Wanted(entry.Wanted) is { Length: > 0 } wanted)
            {
                notes.Add(wanted);
            }

            if (entry.GateLine is { Length: > 0 } gate)
            {
                notes.Add(gate);
            }

            if (notes.Count > 0)
            {
                line += $" — {string.Join(" · ", notes)}";
            }

            // Which row the right pane is drawing, and it is one comparison rather than a second piece of
            // state to keep in step (#110).
            var crumb = EngineersPages.Crumb(entry.Engineer);
            var showing = _nav.Trail.Count > 0
                          && string.Equals(_nav.Trail[^1].Key, crumb.Key, StringComparison.Ordinal);

            _list.Children.Add(LoadoutPages.Row(
                line,
                entry.Aside,
                entry.Wanted > 0 || entry.GateLine is not null,
                () => _nav.Drill(crumb),
                showing: showing,
                markKey: entry.Reach == EngineerReach.Unlocked ? null : ThemeManager.RedKey));
        }
    }

    /// <summary>Whether this engineer survives the two checkbox filters (#132).</summary>
    private bool NotHidden(EngineerEntry entry) => EngineersPages.Shown(_memory, entry.Engineer);

    private bool Matches(EngineerEntry entry) =>
        _query is not { } query
        || entry.Engineer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (entry.Engineer.System?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || entry.Engineer.Specialities.Any(speciality =>
            speciality.Kind.Contains(query, StringComparison.OrdinalIgnoreCase));

    /// <summary>The heading over each group (remediation.md 12, items 8, 9 and 10).</summary>
    private static string Caption(EngineerReach reach) => reach switch
    {
        EngineerReach.WithinReach => "Ready for Unlock",
        EngineerReach.Unlocked => "Unlocked",
        _ => "Needs a Referral",
    };
}

/// <summary>One engineer (Phase 28, "Who can roll this").</summary>
public sealed class EngineerPage : EngineerPageBase
{
    private const int PlannedShown = 8;

    private readonly string _id;
    private readonly PanelNavigator _nav;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly StackPanel _body = new() { Spacing = 2 };

    public EngineerPage(EngineerSource source, string id, PanelNavigator nav, Func<string, Task<bool>>? copy = null)
        : base(source)
    {
        _id = id;
        _nav = nav;
        _copy = copy;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("where is Felicity Farseer");

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

    protected override void Refresh()
    {
        _body.Children.Clear();

        var report = Source.Read();

        var entry = int.TryParse(_id, CultureInfo.InvariantCulture, out var id)
            ? report.Directory.FirstOrDefault(row => row.Engineer.Id == id)
            : null;

        if (entry is null)
        {
            _body.Children.Add(LoadoutPages.Muted("I have no record of that engineer."));
            return;
        }

        var engineer = entry.Engineer;

        _body.Children.Add(Title(engineer.Name));

        // The workshop has a row of its own, so its copy glyph does not narrow the name to a third of the pane.
        var tiles = new List<Control>();

        if (entry.LightYears is not null)
        {
            tiles.Add(StatTile.Build("Distance", entry.Aside, StatInk.Number));
        }

        tiles.Add(StatTile.Build("Where you stand", entry.Status));

        _body.Children.Add(Workshop(engineer, report.From));
        _body.Children.Add(StatTile.Grid(tiles, maxColumns: 2));

        // Where the pin lives — d47 has no journal event for one, so the Commander says so here (#113).
        var (pinned, _) = LabeledCheckBox.Build("A blueprint is pinned with them");
        pinned.IsChecked = Source.IsPinned(engineer.Id);
        pinned.Margin = new Thickness(0, 8, 0, 0);

        pinned.IsCheckedChanged += (_, _) => Source.Pin(engineer.Id, pinned.IsChecked == true);

        _body.Children.Add(pinned);

        if (entry.GateLine is { Length: > 0 } gate)
        {
            _body.Children.Add(LoadoutPages.Muted(gate));
        }

        // One per line rather than one running clause (remediation.md 16, item 6).
        _body.Children.Add(LoadoutPages.Section("Grades", Mini));

        if (entry.SpecialityLines.Count == 0)
        {
            _body.Children.Add(LoadoutPages.Toned(entry.Specialities, ThemeManager.AKey));
        }
        else
        {
            foreach (var speciality in entry.SpecialityLines)
            {
                _body.Children.Add(LoadoutPages.Toned("•  " + speciality, ThemeManager.AKey));
            }
        }

        // The reason to care about this engineer, before what reaching them costs (#109).
        if (entry.Planned.Count > 0)
        {
            _body.Children.Add(LoadoutPages.Section(
                entry.Reach == EngineerReach.Unlocked ? "Planned work" : "What unlocking them buys",
                Mini));

            foreach (var work in entry.Planned.Take(PlannedShown))
            {
                _body.Children.Add(Listed(ListRow.NameInk(new SelectableTextBlock
                {
                    Text = work.Describe(),
                    FontSize = TypeScale.Body,
                    TextWrapping = TextWrapping.Wrap,
                })));
            }

            if (entry.Planned.Count > PlannedShown)
            {
                _body.Children.Add(LoadoutPages.Muted(
                    $"and {(entry.Planned.Count - PlannedShown).ToString(CultureInfo.InvariantCulture)} more."));
            }
        }

        // Unlock Prerequisites, with what is already done marked (remediation.md 13, item 12).
        if (entry.Criteria.Count > 0)
        {
            _body.Children.Add(EngineersPages.PrerequisitesHeader(engineer, entry.Criteria, Source, Refresh, Mini));
            _body.Children.Add(EngineersPages.Criteria(entry.Criteria));
        }

        // The way in, stop by stop.
        if (entry.Chain.IsDone)
        {
            var done = LoadoutPages.Muted("Nothing stands between you and them.");
            done.Margin = new Thickness(0, 10, 0, 0);

            _body.Children.Add(done);
        }
        else
        {
            _body.Children.Add(LoadoutPages.Section("The way in", Mini));

            foreach (var step in entry.Chain.Steps)
            {
                _body.Children.Add(Listed(Stop(step, report.From)));
            }
        }

        // The prose the table carries, last, because it is the part a Commander reads once.
        foreach (var (caption, text) in new[]
                 {
                     ("Earning the invitation", engineer.Meeting),
                     ("The invitation asks for", engineer.Unlock),
                 })
        {
            if (text is { Length: > 0 })
            {
                _body.Children.Add(LoadoutPages.Section(caption, Mini));
                _body.Children.Add(LoadoutPages.Muted(text));
            }
        }
    }

    /// <summary>The engineer's name as the screen title block.</summary>
    private Control Title(string name)
    {
        var block = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Heading,
            TitleRank.Screen);

        TitleText.Show(block, name);

        var title = TitleText.Block(block);
        title.Margin = new Thickness(0, 0, 0, Mini ? 4 : 10);

        return title;
    }

    /// <summary>Where the engineer works, Cyan where it is the Commander's system, with the copy glyph inside the tile.</summary>
    private Control Workshop(Engineer engineer, string? here)
    {
        var system = engineer.System;
        var ink = Here(system, here) ? StatInk.Here : StatInk.Value;
        var tile = StatTile.Build("Workshop", engineer.Where, ink);

        if (system is not { Length: > 0 } target || _copy is not { } copy || tile.Child is not { } figures)
        {
            return tile;
        }

        tile.Child = null;

        var glyph = CopyGlyph.For(target, copy);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(glyph, 1);

        tile.Child = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 6,
            Children = { figures, glyph },
        };

        return tile;
    }

    private static bool Here(string? system, string? here) =>
        system is { Length: > 0 } && string.Equals(system, here, StringComparison.OrdinalIgnoreCase);

    /// <summary>One stop on the way in: the engineer's name opens them, and the rest of the stop stays beside it (remediation.md 12, item 7).</summary>
    private Control Stop(UnlockStep step, string? here)
    {
        var stop = new StackPanel { Spacing = 2 };
        var row = new WrapPanel { Orientation = Orientation.Horizontal };

        row.Children.Add(EngineersPages.Name(step.Engineer, _nav, TypeScale.Body));

        if (step.SystemSplit() is { } split && _copy is { } stopCopy)
        {
            var named = Said(split.Before + split.System);

            if (Here(split.System, here))
            {
                var system = new Run(split.System);
                LoadoutPages.Themed(system, Run.ForegroundProperty, ThemeManager.CyanKey);

                named.Inlines = [new Run(split.Before), system];
            }

            row.Children.Add(named);
            row.Children.Add(CopyGlyph.For(split.System, stopCopy));
            row.Children.Add(Said(split.After));
        }
        else
        {
            row.Children.Add(Said(step.Rest()));
        }

        stop.Children.Add(row);

        if (step.Meeting is { Length: > 0 } meeting)
        {
            stop.Children.Add(LoadoutPages.Muted($"first: {meeting}"));
        }

        if (step.Tribute is { Length: > 0 } tribute)
        {
            stop.Children.Add(LoadoutPages.Muted($"hand over: {tribute}"));
        }

        return stop;
    }

    private static SelectableTextBlock Said(string text)
    {
        var block = new SelectableTextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return block;
    }

    /// <summary>A read-only list row holding <paramref name="content"/>.</summary>
    private static Border Listed(Control content) =>
        ListRow.Dress(new Border { Child = content });
}

/// <summary>The solver (Phase 28, "The fastest way in").</summary>
public sealed class EngineerRoutePage : EngineerPageBase, IPageSummary
{
    private const int Shown = 5;

    private readonly PanelNavigator _nav;
    private readonly EngineerDirectoryMemory? _memory;

    private readonly StackPanel _body = new() { Spacing = 2 };

    public EngineerRoutePage(EngineerSource source, PanelNavigator nav, EngineerDirectoryMemory? memory = null)
        : base(source)
    {
        _nav = nav;
        _memory = memory;
        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("who should I unlock next");

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

    protected override void Refresh()
    {
        _body.Children.Clear();

        var report = Source.Read();

        Summarise(report.Summary());

        // What the ranking was measured from, said out loud.
        _body.Children.Add(LoadoutPages.Muted(Measured(report)));

        // The Directory's two ticks take rows off this ranking too, without touching the ranking itself
        // (#132) — EngineerPlanService.Describe() reads the same UnlockPlanner.Rank output for the
        // spoken answer, which stays unfiltered.
        var route = report.Route.Where(candidate => EngineersPages.Shown(_memory, candidate.Engineer)).ToList();

        if (route.Count == 0)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "There is nobody left to unlock at the grade your plans ask for."));

            return;
        }

        foreach (var candidate in route.Take(Shown))
        {
            // The ranked name opens that engineer rather than merely heading a block (remediation.md 12, item
            // 7).
            var row = Ranked(candidate);
            row.Margin = new Thickness(0, 12, 0, 0);
            _body.Children.Add(row);

            if (EngineersPages.AddPrerequisitesControl(candidate.Engineer, candidate.Criteria, Source, Refresh)
                is { } button)
            {
                button.Margin = new Thickness(0, 2, 0, 0);
                _body.Children.Add(button);
            }

            if (candidate.Criteria.Count > 0)
            {
                _body.Children.Add(EngineersPages.Criteria(candidate.Criteria));
            }

            foreach (var line in candidate.Working())
            {
                _body.Children.Add(LoadoutPages.Muted(line));
            }
        }

        if (route.Count > Shown)
        {
            // Said rather than silently cut.
            var more = LoadoutPages.Muted(
                $"{(route.Count - Shown).ToString(CultureInfo.InvariantCulture)} more ranked "
                + "below these, on the Directory.");
            more.Margin = new Thickness(0, 10, 0, 0);

            _body.Children.Add(more);
        }
    }

    /// <summary>A candidate as a pressable list row: the name, and the ranking's summary under it.</summary>
    private Button Ranked(UnlockCandidate candidate)
    {
        var crumb = EngineersPages.Crumb(candidate.Engineer);
        var showing = _nav.Trail.Count > 0
                      && string.Equals(_nav.Trail[^1].Key, crumb.Key, StringComparison.Ordinal);

        var name = ListRow.Name(new TextBlock { Text = candidate.Engineer.Name, TextWrapping = TextWrapping.Wrap });

        var summary = ListRow.Sub(new TextBlock { Text = candidate.Summary(), TextWrapping = TextWrapping.Wrap });

        var button = ListRow.Dress(
            new Button
            {
                Content = new StackPanel { Spacing = 1, Children = { name, summary } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
            },
            showing);

        button.Click += (_, _) => _nav.Drill(crumb);

        return button;
    }

    private static string Measured(EngineerReport report)
    {
        if (report.From is not { Length: > 0 } here)
        {
            return "I do not know where you are yet, so nothing here is ranked by distance.";
        }

        return report.JumpRange is { } range
            ? $"Measured from {here}, at {range.ToString("N1", CultureInfo.InvariantCulture)} ly a jump."
            : $"Measured from {here}. No jump range reported, so distances are in light years only.";
    }
}
