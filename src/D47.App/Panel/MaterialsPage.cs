using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>Fleet › Materials: a sidebar of views over the gap, opening on Needed by plans (#557).</summary>
public sealed class MaterialsPage : UserControl
{
    public const string RootKey = LoadoutPages.GapRoot;

    /// <summary>The footer's phrase; it carries a <c>get_build_gap</c> keyword.</summary>
    public const string Phrase = "what am I short of";

    public const string NeededView = "needed";

    public const string NeededHead =
        "What every live plan still needs, for ships, suits and weapons, against what you hold.";

    public const string NoPlans = "No live plan needs a material.";

    public const string Met = "✓ MET";

    /// <summary>Names the click-outside layer, for a test to press without aiming at the dialog itself.</summary>
    internal const string DetailBackdropName = "MaterialsDetailBackdrop";

    private const string Columns = "*,150,110,90,200";

    private readonly GapSource _gap;
    private readonly EngineerSource? _engineers;
    private readonly ContentControl _sidebar = new();
    private readonly StackPanel _body = new();
    private readonly Avalonia.Controls.Panel _detailLayer = new() { IsVisible = false };

    public MaterialsPage(GapSource gap, JournalClock clock, EngineerSource? engineers = null)
    {
        _gap = gap;
        _engineers = engineers;

        gap.Changed += OnChanged;

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase, clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var columns = new DockPanel();
        DockPanel.SetDock(_sidebar, Dock.Left);
        columns.Children.Add(_sidebar);
        columns.Children.Add(LoadoutPages.Scrolling(_body));
        root.Children.Add(columns);

        _detailLayer.HorizontalAlignment = HorizontalAlignment.Stretch;
        _detailLayer.VerticalAlignment = VerticalAlignment.Stretch;

        Content = new Avalonia.Controls.Panel { Children = { root, _detailLayer } };

        Refresh();
    }

    /// <summary>Redraws against the live plans and the live inventory.</summary>
    public void Refresh()
    {
        CloseDetail();

        var report = _gap.Report();
        var needed = report.Builds
            .SelectMany(build => build.Lines)
            .Select(line => line.Material.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        _sidebar.Content = new Sidebar(
            [new SidebarGroup("Plans", [new SidebarItem(NeededView, "Needed by plans", Count(needed))])],
            NeededView);

        _body.Children.Clear();
        _body.Children.Add(TitleText.Block(
            TitleText.Build("Needed by plans", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Materials ›")));
        _body.Children.Add(Hint(NeededHead, new Thickness(0, 10, 0, 0)));

        var notes = Notes(report);

        if (notes.Count > 0)
        {
            var stack = new StackPanel { Spacing = IndexPage.ListGap, Margin = new Thickness(0, 18, 0, 0) };

            foreach (var note in notes)
            {
                stack.Children.Add(note);
            }

            _body.Children.Add(stack);
        }

        if (report.Builds.Count == 0)
        {
            _body.Children.Add(Hint(NoPlans, new Thickness(0, 18, 0, 0)));
            return;
        }

        foreach (var build in report.Builds)
        {
            _body.Children.Add(Group(build));
        }
    }

    /// <summary>The head's kind line: <c>Anaconda · 2 plans · 4 SHORT</c>, <c>Suit · grade 5 · 1 SHORT</c>.</summary>
    public static string KindLine(GapBuild build)
    {
        var kind = build.Key.Kind switch
        {
            GapBuildKind.Ship =>
                $"{build.Hull} · {Count(build.Plans)} plan{(build.Plans == 1 ? string.Empty : "s")}",
            _ when build.Grade is { } grade =>
                $"{KindWord(build.Key.Kind)} · grade {Count(grade)}",
            _ => KindWord(build.Key.Kind),
        };

        return $"{kind} · {Count(build.Short)} SHORT";
    }

    /// <summary>RAW, MANUFACTURED or ENCODED for a ship material, ON FOOT for a locker item.</summary>
    public static string LedgerOf(MaterialEntry material) => material.Ledger switch
    {
        MaterialLedger.Material => (material.Category ?? "Material").ToUpperInvariant(),
        MaterialLedger.ShipLocker => "ON FOOT",
        MaterialLedger.Cargo or MaterialLedger.RareCargo => "CARGO",
        _ => "UNKNOWN",
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _gap.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    private static string KindWord(GapBuildKind kind) => kind switch
    {
        GapBuildKind.Suit => "Suit",
        GapBuildKind.Weapon => "Weapon",
        _ => "Ship",
    };

    private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static Control Group(GapBuild build)
    {
        var name = TitleText.Build(build.Name, TypeScale.Section, TitleRank.Group);
        name.VerticalAlignment = VerticalAlignment.Center;

        var kind = new TextBlock
        {
            Text = KindLine(build),
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };
        LoadoutPages.Themed(kind, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var head = new WrapPanel { ItemSpacing = 12, LineSpacing = 4, Children = { name, kind } };

        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var line in build.Lines)
        {
            rows.Children.Add(Row(line));
        }

        return new StackPanel
        {
            Margin = new Thickness(0, 28, 0, 0),
            Children = { TitleText.GroupRow(head), ColumnHeads(), rows },
        };
    }

    private static Control ColumnHeads()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), Height = 32 };

        foreach (var (text, column, right) in new[]
                 {
                     ("Material", 0, false),
                     ("Ledger", 1, false),
                     ("Held / need", 2, true),
                     ("Short", 3, true),
                 })
        {
            var head = Chrome(text, ThemeManager.GreyKey);
            head.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(head, column);
            grid.Children.Add(head);
        }

        return new Border { Padding = new Thickness(14, 0), Child = grid };
    }

    private static Border Row(GapBuildLine line)
    {
        var name = new TextBlock
        {
            Text = line.Material.Name,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var ledger = Chrome(LedgerOf(line.Material), ThemeManager.GreyKey);
        Grid.SetColumn(ledger, 1);

        var amounts = Mono($"{Count(line.Held)} / {Count(line.Needed)}", ThemeManager.WhiteKey);
        Grid.SetColumn(amounts, 2);

        var shortfall = line.Short > 0
            ? Mono(Count(line.Short), ThemeManager.AKey)
            : Mono(Met, ThemeManager.BlueKey);
        Grid.SetColumn(shortfall, 3);

        var gauge = Gauge.Track(line.Needed > 0 ? (double)line.Held / line.Needed : 1, ThemeManager.AKey);
        gauge.VerticalAlignment = VerticalAlignment.Center;
        gauge.Margin = new Thickness(16, 0, 0, 0);
        Grid.SetColumn(gauge, 4);

        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(Columns),
                Children = { name, ledger, amounts, shortfall, gauge },
            },
        };
        var fill = Themed(row, Border.BackgroundProperty, ThemeManager.TileKey);
        row.PointerEntered += (_, _) =>
        {
            fill.Dispose();
            fill = Themed(row, Border.BackgroundProperty, ThemeManager.Tile2Key);
        };
        row.PointerExited += (_, _) =>
        {
            fill.Dispose();
            fill = Themed(row, Border.BackgroundProperty, ThemeManager.TileKey);
        };

        return row;
    }

    private static IDisposable Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));

    private static TextBlock Chrome(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.MetaSmall,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Mono(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Control,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Hint(string text, Thickness margin)
    {
        var hint = new TextBlock { Text = text, FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap, Margin = margin };
        LoadoutPages.Themed(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return hint;
    }

    private List<Control> Notes(GapReport report)
    {
        var notes = new List<Control>();

        if (report.Gates.Count > 0)
        {
            notes.Add(GatesNote(report));
        }

        if (report.Uncovered.Count > 0)
        {
            var n = report.Uncovered.Count;

            notes.Add(NoteLine(
                $"{Count(n)} planned slot{(n == 1 ? string.Empty : "s")} "
                + (n == 1 ? "has" : "have") + " no material total",
                () => OpenList("No material total — the reason is on each line", report.Uncovered)));
        }

        if (report.Assumed.Count > 0)
        {
            var n = report.Assumed.Count;

            notes.Add(NoteLine(
                $"{Count(n)} planned slot{(n == 1 ? string.Empty : "s")} "
                + (n == 1 ? "is" : "are") + " costed at the most expensive module",
                () => OpenList("Costed at the most expensive module", report.Assumed)));
        }

        return notes;
    }

    /// <summary>A note as a warning, its SHOW tile opening what <paramref name="open"/> shows.</summary>
    private static Control NoteLine(string text, Action open)
    {
        var show = LoadoutPages.Press("Show", open);

        AutomationProperties.SetName(show, text);

        return new Notice(NoticeLevel.Warning) { Text = text, Actions = { show } };
    }

    /// <summary>
    /// The Gates note: counted and named by job where an <see cref="EngineerSource"/> can say who unblocks
    /// them, the same set the Engineers page ranks (#477); the raw per-slot lines otherwise.
    /// </summary>
    private Control GatesNote(GapReport report)
    {
        if (_engineers is { } source)
        {
            var n = BlockedJobs(source.Read()).Count;

            return NoteLine(
                $"{Count(n)} planned grade{(n == 1 ? string.Empty : "s")} "
                + (n == 1 ? "is" : "are") + " beyond your engineers' ranks",
                () => OpenEngineerGates(source));
        }

        var slots = report.Gates.Count;

        return NoteLine(
            $"{Count(slots)} planned slot{(slots == 1 ? string.Empty : "s")} "
            + (slots == 1 ? "needs" : "need") + " a higher engineer rank",
            () => OpenList("No unlocked engineer offers these grades yet — costed at five rolls", report.Gates));
    }

    /// <summary>Every outstanding job the ranked route covers, across every candidate, grouped by job.</summary>
    private static IReadOnlyList<IReadOnlyList<PlannedWork>> BlockedJobs(EngineerReport report) =>
        [.. report.Route
            .SelectMany(candidate => candidate.Covers)
            .GroupBy(work => work.Job, StringComparer.Ordinal)
            .Select(job => (IReadOnlyList<PlannedWork>)[.. job])];

    /// <summary>One blocked job, readable and counted — "Grade 5 Long Range Weapon · Multi-cannon ×10".</summary>
    private static string DescribeBlockedJob(IReadOnlyList<PlannedWork> job)
    {
        var described = job[0].DescribeJob();
        var capitalised = char.ToUpperInvariant(described[0]) + described[1..];
        var named = job[0].Module is { Length: > 0 } module ? $"{capitalised} · {module}" : capitalised;

        return job.Count > 1 ? $"{named} ×{Count(job.Count)}" : named;
    }

    /// <summary>Where the Commander stands with one engineer — not started, invited, or unlocked at grade N.</summary>
    private static string Standing(EngineerStanding? standing, int needed)
    {
        if (standing is { IsUnlocked: true })
        {
            return $"unlocked at grade {Count(standing.Rank ?? 1)} of {Count(needed)} needed";
        }

        return standing is { IsInvited: true } ? "invited" : "not started";
    }

    /// <summary>Who is needed, which one first, how to get them, and what is still blocked (#477).</summary>
    private void OpenEngineerGates(EngineerSource source)
    {
        var report = source.Read();
        var route = report.Route.Where(candidate => candidate.Covers.Count > 0).ToList();
        var body = new List<Control>();

        if (route.Count == 0)
        {
            body.Add(LoadoutPages.Muted("Nobody left to unlock covers this."));
        }
        else
        {
            body.Add(LoadoutPages.Section("Who you need"));

            foreach (var candidate in route)
            {
                var needed = candidate.Covers.Max(work => work.Rank);
                var entry = report.Directory.FirstOrDefault(e => e.Engineer.Id == candidate.Engineer.Id);

                body.Add(LoadoutPages.Muted($"{candidate.Engineer.Name} — {Standing(entry?.Standing, needed)}"));
            }

            var first = route[0];

            body.Add(LoadoutPages.Section("Which one first"));
            body.Add(LoadoutPages.Muted($"{first.Engineer.Name}: {first.Summary()}"));

            body.Add(LoadoutPages.Section("How to get them"));

            foreach (var line in first.Working())
            {
                body.Add(LoadoutPages.Muted(line));
            }

            if (EngineersPages.AddPrerequisitesControl(
                    first.Engineer, first.Criteria, source, () => OpenEngineerGates(source)) is { } button)
            {
                body.Add(button);
            }
        }

        body.Add(LoadoutPages.Section("What is blocked"));

        foreach (var job in BlockedJobs(report))
        {
            body.Add(LoadoutPages.Muted(DescribeBlockedJob(job)));
        }

        body.Add(LoadoutPages.Muted("Material totals count these at the most rolls their grade can take."));

        OpenDetail("Beyond your engineers' ranks", body);
    }

    private void OpenList(string title, IReadOnlyList<string> lines) =>
        OpenDetail(title, [.. lines.Select(line => (Control)LoadoutPages.Muted(line))]);

    /// <summary>Opens the detail dialog in the page's own visual tree, replacing whatever was open.</summary>
    private void OpenDetail(string title, IReadOnlyList<Control> body)
    {
        var content = new StackPanel { Spacing = 4 };

        foreach (var line in body)
        {
            content.Children.Add(line);
        }

        var panel = new Border
        {
            BorderThickness = new Thickness(1),
            MaxWidth = 480,
            MaxHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = Modal.Build("Materials", title, content, [LoadoutPages.Press("Close", CloseDetail)]),
        };

        LoadoutPages.Themed(panel, Border.BorderBrushProperty, ThemeManager.AKey);

        var backdrop = new Border { Name = DetailBackdropName };

        LoadoutPages.Themed(backdrop, Border.BackgroundProperty, ThemeManager.ScrimKey);

        backdrop.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            CloseDetail();
        };

        _detailLayer.Children.Clear();
        _detailLayer.Children.Add(backdrop);
        _detailLayer.Children.Add(panel);
        _detailLayer.IsVisible = true;
    }

    private void CloseDetail()
    {
        _detailLayer.IsVisible = false;
        _detailLayer.Children.Clear();
    }
}
