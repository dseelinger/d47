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
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Engineers tab (Phase 28).</summary>
public static class EngineersPages
{
    /// <summary>The tab's first root: everybody, in the order they can be acted on.</summary>
    public const string DirectoryRoot = "engineers.directory";

    /// <summary>Its second: the solver.</summary>
    public const string RouteRoot = "engineers.route";

    /// <summary>How one engineer's crumb is keyed, so a page rebuilds from the trail alone.</summary>
    public const string WhoPrefix = "engineers.who:";

    /// <summary>Draws whichever level a crumb names.</summary>
    public static Control Build(
        NavCrumb crumb, EngineerSource source, PanelNavigator nav, EngineerDirectoryMemory? memory = null)
    {
        if (crumb.Key.StartsWith(WhoPrefix, StringComparison.Ordinal))
        {
            return new EngineerPage(source, crumb.Key[WhoPrefix.Length..], nav);
        }

        return crumb.Key == RouteRoot
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

        LoadoutPages.Themed(label, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

        var button = new Button
        {
            Content = label,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2),
            HorizontalAlignment = HorizontalAlignment.Left,

            // Tall enough for a ray at a metre, which is the floor every pressable thing on this surface has.
            MinHeight = 30,
        };

        button.Click += (_, _) => nav.Drill(Crumb(engineer));

        return button;
    }

    /// <summary>What the directory's mark means: modules still to engineer this engineer could finish.</summary>
    internal static string Wanted(int many) =>
        many == 0 ? string.Empty : EngineerSay.Count(many, "module", "modules");

    /// <summary>
    /// One prerequisite, a drawn box in front of it rather than a character — the same box wherever a
    /// criterion is shown (#126).
    /// </summary>
    internal static Control CriterionLine(UnlockCriterion criterion)
    {
        var (data, brush, says) = criterion.Met switch
        {
            true => (Glyphs.BoxChecked, ThemeManager.AccentKey, "met"),
            false => (Glyphs.BoxEmpty, ThemeManager.TextMutedKey, "not met"),
            _ => (Glyphs.BoxUndecided, ThemeManager.InfoKey, "not yet known"),
        };

        var box = Glyphs.Draw(data, brush);
        AutomationProperties.SetName(box, says);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                box,
                new SelectableTextBlock
                {
                    Text = criterion.Text,
                    FontSize = TypeScale.Body,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
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

    protected EngineerSource Source { get; }

    protected abstract void Refresh();

    /// <summary>
    /// Attach to detach, for the reason <see cref="LoadoutPage"/> spells out (remediation.md 13, item
    /// 1).
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Source.Changed += OnChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Source.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);
}

/// <summary>The directory (Phase 28, "Who can roll this").</summary>
public sealed class EngineerDirectoryPage : EngineerPageBase, IFilterablePage
{
    private readonly PanelNavigator _nav;
    private readonly TextBlock _summary = new()
    {
        FontSize = TypeScale.Body,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
    };

    private readonly StackPanel _list = new() { Spacing = 3 };
    private readonly EngineerDirectoryMemory? _memory;

    private readonly CheckBox _colonia = new()
    {
        Content = "Hide the Colonia eight",
        MinHeight = 30,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly CheckBox _onFoot = new()
    {
        Content = "Hide on-foot engineers",
        MinHeight = 30,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private string? _query;

    public EngineerDirectoryPage(EngineerSource source, PanelNavigator nav, EngineerDirectoryMemory? memory = null)
        : base(source)
    {
        _nav = nav;
        _memory = memory;

        _colonia.IsChecked = memory?.HideColonia ?? false;
        _onFoot.IsChecked = memory?.HideOnFoot ?? false;

        // The switch owns the flag rather than mirroring it back — the same reason the goals band's
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

        var checks = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { _colonia, _onFoot },
        };

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("who should I unlock next");

        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(checks, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_summary);
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

        _summary.Text = report.Summary();
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
                _list.Children.Add(LoadoutPages.Heading(Caption(entry.Reach)));
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
                showing: showing));
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
    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly TextBlock _said = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Margin = new Thickness(0, 8, 0, 0),
    };

    public EngineerPage(EngineerSource source, string id, PanelNavigator nav)
        : base(source)
    {
        _id = id;
        _nav = nav;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("where is Felicity Farseer");

        DockPanel.SetDock(say, Dock.Bottom);
        DockPanel.SetDock(_said, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(_said);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        LoadoutPages.Themed(_said, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

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

        _body.Children.Add(new SelectableTextBlock
        {
            Text = engineer.Where,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        _body.Children.Add(LoadoutPages.Muted(entry.Aside));

        if (entry.GateLine is { Length: > 0 } gate)
        {
            _body.Children.Add(LoadoutPages.Muted(gate));
        }

        // One per line rather than one running clause (remediation.md 16, item 6).
        _body.Children.Add(LoadoutPages.Heading("Grades"));

        if (entry.SpecialityLines.Count == 0)
        {
            _body.Children.Add(LoadoutPages.Muted(entry.Specialities));
        }
        else
        {
            foreach (var speciality in entry.SpecialityLines)
            {
                _body.Children.Add(LoadoutPages.Muted("•  " + speciality));
            }
        }

        _body.Children.Add(LoadoutPages.Heading("Where you stand"));
        _body.Children.Add(LoadoutPages.Muted(entry.Status));

        // The reason to care about this engineer, before what reaching them costs (#109).
        if (entry.Planned.Count > 0)
        {
            _body.Children.Add(LoadoutPages.Heading(
                entry.Reach == EngineerReach.Unlocked ? "Planned work" : "What unlocking them buys"));

            foreach (var work in entry.Planned.Take(PlannedShown))
            {
                _body.Children.Add(LoadoutPages.Muted("•  " + work.Describe()));
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
            _body.Children.Add(LoadoutPages.Heading("Unlock Prerequisites"));

            foreach (var criterion in entry.Criteria)
            {
                _body.Children.Add(EngineersPages.CriterionLine(criterion));
            }
        }

        // The way in, stop by stop.
        if (entry.Chain.IsDone)
        {
            _body.Children.Add(LoadoutPages.Muted("Nothing stands between you and them."));
        }
        else
        {
            _body.Children.Add(LoadoutPages.Heading("The way in"));

            foreach (var step in entry.Chain.Steps)
            {
                // The name opens that engineer, and the rest of the stop stays beside it (remediation.md 12,
                // item 7).
                _body.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        EngineersPages.Name(step.Engineer, _nav, TypeScale.Body),
                        new SelectableTextBlock
                        {
                            Text = step.Rest(),
                            FontSize = TypeScale.Body,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                });

                if (step.Meeting is { Length: > 0 } meeting)
                {
                    _body.Children.Add(LoadoutPages.Muted($"    first: {meeting}"));
                }

                if (step.Tribute is { Length: > 0 } tribute)
                {
                    _body.Children.Add(LoadoutPages.Muted($"    hand over: {tribute}"));
                }
            }

            var promote = LoadoutPages.Press(
                "Put the route on my checklist",
                () => Say(Source.Promote(engineer.Name)));

            promote.Margin = new Thickness(0, 10, 0, 0);

            _body.Children.Add(promote);
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
                _body.Children.Add(LoadoutPages.Heading(caption));
                _body.Children.Add(LoadoutPages.Muted(text));
            }
        }
    }

    private void Say(string message)
    {
        _said.IsVisible = true;
        _said.Text = message;
    }
}

/// <summary>The solver (Phase 28, "The fastest way in").</summary>
public sealed class EngineerRoutePage : EngineerPageBase
{
    private const int Shown = 5;

    private readonly PanelNavigator _nav;
    private readonly EngineerDirectoryMemory? _memory;

    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly TextBlock _said = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Margin = new Thickness(0, 8, 0, 0),
    };

    public EngineerRoutePage(EngineerSource source, PanelNavigator nav, EngineerDirectoryMemory? memory = null)
        : base(source)
    {
        _nav = nav;
        _memory = memory;
        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("who should I unlock next");

        DockPanel.SetDock(say, Dock.Bottom);
        DockPanel.SetDock(_said, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(_said);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        LoadoutPages.Themed(_said, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

        Refresh();
    }

    protected override void Refresh()
    {
        _body.Children.Clear();

        var report = Source.Read();

        _body.Children.Add(new SelectableTextBlock
        {
            Text = report.Summary(),
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        });

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
            _body.Children.Add(EngineersPages.Name(
                candidate.Engineer, _nav, TypeScale.Body, FontWeight.SemiBold));

            _body.Children.Add(new SelectableTextBlock
            {
                Text = candidate.Summary(),
                FontSize = TypeScale.Body,
                TextWrapping = TextWrapping.Wrap,
            });

            foreach (var criterion in candidate.Criteria)
            {
                _body.Children.Add(EngineersPages.CriterionLine(criterion));
            }

            foreach (var line in candidate.Working())
            {
                _body.Children.Add(LoadoutPages.Muted(line));
            }

            var promote = LoadoutPages.Press(
                "Put this route on my checklist",
                () => Say(Source.Promote(candidate.Engineer.Name)));

            promote.Margin = new Thickness(0, 6, 0, 0);

            _body.Children.Add(promote);
        }

        if (route.Count > Shown)
        {
            // Said rather than silently cut.
            _body.Children.Add(LoadoutPages.Muted(
                $"{(route.Count - Shown).ToString(CultureInfo.InvariantCulture)} more ranked "
                + "below these, on the Directory."));
        }
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

    private void Say(string message)
    {
        _said.IsVisible = true;
        _said.Text = message;
    }
}
