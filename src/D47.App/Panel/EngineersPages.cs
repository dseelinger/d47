using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The Engineers tab (list.md Phase 28).
/// <para>
/// <b>Two roots and one level.</b> The <em>Directory</em> answers "who can I go and get", ordered
/// by what the Commander can act on today; the <em>Route</em> answers "how do I get everything my
/// plans need", ranked and showing its work. Drilling a directory row opens the one engineer
/// behind it. That is the whole shape — the drill stack, the reflow and the breadcrumb Phase 25
/// built cover the rest without any of these pages knowing.
/// </para>
/// <para>
/// <b>One pane rather than two.</b> A row already holds the name, the specialities, the location
/// and the standing, so a second column beside it would only repeat them.
/// </para>
/// </summary>
public static class EngineersPages
{
    /// <summary>The tab's first root: everybody, in the order they can be acted on.</summary>
    public const string DirectoryRoot = "engineers.directory";

    /// <summary>Its second: the solver.</summary>
    public const string RouteRoot = "engineers.route";

    /// <summary>How one engineer's crumb is keyed, so a page rebuilds from the trail alone.</summary>
    public const string WhoPrefix = "engineers.who:";

    /// <summary>Draws whichever level a crumb names. Handed to <see cref="PanelView.Furnish"/>.</summary>
    public static Control Build(NavCrumb crumb, EngineerSource source, PanelNavigator nav)
    {
        if (crumb.Key.StartsWith(WhoPrefix, StringComparison.Ordinal))
        {
            return new EngineerPage(source, crumb.Key[WhoPrefix.Length..]);
        }

        return crumb.Key == RouteRoot
            ? new EngineerRoutePage(source)
            : new EngineerDirectoryPage(source, nav);
    }

    /// <summary>The crumb for one engineer, keyed on the id the journal writes rather than a name.</summary>
    public static NavCrumb Crumb(Engineer engineer) =>
        new(WhoPrefix + engineer.Id.ToString(CultureInfo.InvariantCulture), engineer.Name);

    /// <summary>What the directory's mark means: somebody one of the Commander's plans wants.</summary>
    internal static string Wanted(int many) =>
        many == 0
            ? string.Empty
            : $"{EngineerSay.Count(many, "planned thing", "planned things")} wants them";
}

/// <summary>
/// A page that redraws when the plans or the Commander's position move underneath it, and lets go
/// of the source when it is detached — the same arrangement <see cref="LoadoutPage"/> makes, and
/// forgetting it once is a page that keeps redrawing after its tab has gone.
/// </summary>
public abstract class EngineerPageBase : UserControl
{
    protected EngineerPageBase(EngineerSource source)
    {
        Source = source;
        source.Changed += OnChanged;
    }

    protected EngineerSource Source { get; }

    protected abstract void Refresh();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Source.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);
}

/// <summary>
/// The directory (list.md Phase 28, "Who can roll this").
/// <para>
/// <b>Sorted by what the Commander can act on today</b> — within reach, then unlocked, then
/// locked — rather than alphabetically or by speciality, because the question is nearly always
/// <em>who can I go and get</em>. The summary carries the count that belongs to the other tab:
/// how many plans are waiting on somebody they have not met.
/// </para>
/// </summary>
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
    private string? _query;

    public EngineerDirectoryPage(EngineerSource source, PanelNavigator nav)
        : base(source)
    {
        _nav = nav;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("who should I unlock next");

        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_summary);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        Content = root;

        Refresh();
    }

    /// <summary>
    /// Thirty-eight people with ten specialities each is a scroll hunt, so the query takes rows
    /// away rather than only colouring them. It matches on the name, the system and what they
    /// grade, which are the three things anybody types looking for one.
    /// </summary>
    /// <summary>The directory is a long list of names, which is exactly what a query is for.</summary>
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

        var shown = report.Directory.Where(Matches).ToList();

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

            if (EngineersPages.Wanted(entry.Wanted) is { Length: > 0 } wanted)
            {
                line += $" — {wanted}";
            }

            _list.Children.Add(LoadoutPages.Row(
                line,
                entry.Aside,
                entry.Wanted > 0,
                () => _nav.Drill(EngineersPages.Crumb(entry.Engineer))));
        }
    }

    private bool Matches(EngineerEntry entry) =>
        _query is not { } query
        || entry.Engineer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (entry.Engineer.System?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || entry.Engineer.Specialities.Any(speciality =>
            speciality.Kind.Contains(query, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The heading over each group, and it says what the group <em>means</em> rather than naming
    /// the state — "within reach" is a term this page invented and nobody has to learn a term to
    /// read a list.
    /// </summary>
    private static string Caption(EngineerReach reach) => reach switch
    {
        EngineerReach.WithinReach => "You can go and get these now",
        EngineerReach.Unlocked => "Already yours",
        _ => "Behind somebody else",
    };
}

/// <summary>
/// One engineer (list.md Phase 28, "Who can roll this").
/// <para>
/// Everything a row could not hold: what they grade and to what grade, where the Commander stands,
/// and the way in — spelled out stop by stop, with the button that puts it on the checklist.
/// </para>
/// </summary>
public sealed class EngineerPage : EngineerPageBase
{
    private readonly string _id;
    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly TextBlock _said = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Margin = new Thickness(0, 8, 0, 0),
    };

    public EngineerPage(EngineerSource source, string id)
        : base(source)
    {
        _id = id;

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

        _body.Children.Add(new TextBlock
        {
            Text = engineer.Where,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        _body.Children.Add(LoadoutPages.Muted(entry.Aside));

        _body.Children.Add(LoadoutPages.Heading("Grades"));
        _body.Children.Add(LoadoutPages.Muted(entry.Specialities));

        _body.Children.Add(LoadoutPages.Heading("Where you stand"));
        _body.Children.Add(LoadoutPages.Muted(entry.Status));

        // The way in, stop by stop. Nothing here is a summary of the chain — a summary is what a
        // Commander cannot act on.
        if (entry.Chain.IsDone)
        {
            _body.Children.Add(LoadoutPages.Muted("Nothing stands between you and them."));
        }
        else
        {
            _body.Children.Add(LoadoutPages.Heading("The way in"));

            foreach (var step in entry.Chain.Steps)
            {
                _body.Children.Add(new TextBlock
                {
                    Text = step.Describe(),
                    FontSize = TypeScale.Body,
                    TextWrapping = TextWrapping.Wrap,
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

        // The prose the table carries, last, because it is the part a Commander reads once. It is
        // also the part that argues with d47's own count of the chain where the two disagree —
        // Yi Shen's meeting text names three referrals where the directory reads any one of three
        // — so it is printed rather than summarised away.
        foreach (var (caption, text) in new[]
                 {
                     ("Earning the invitation", engineer.Meeting),
                     ("The invitation asks for", engineer.Unlock),
                     ("Reputation rises fastest by", engineer.Reputation),
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

/// <summary>
/// The solver (list.md Phase 28, "The fastest way in").
/// <para>
/// <b>It shows its work.</b> Every candidate carries the sentence that produced its place in the
/// order — the stops, the distance, the jumps at the range of the ship being flown, and what it
/// covers — because a ranking nobody can inspect is an oracle, and when it is wrong, or when the
/// Commander would rather go elsewhere anyway, they cannot tell a bad answer from a bug.
/// </para>
/// </summary>
public sealed class EngineerRoutePage : EngineerPageBase
{
    private const int Shown = 5;

    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly TextBlock _said = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Margin = new Thickness(0, 8, 0, 0),
    };

    public EngineerRoutePage(EngineerSource source)
        : base(source)
    {
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

        _body.Children.Add(new TextBlock
        {
            Text = report.Summary(),
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        });

        // What the ranking was measured from, said out loud. A ranking whose origin is unstated is
        // one a Commander cannot check, and with no position at all every candidate scores the
        // same — which is alphabetical order wearing a ranking's coat.
        _body.Children.Add(LoadoutPages.Muted(Measured(report)));

        if (report.Route.Count == 0)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "There is nobody left to unlock at the grade your plans ask for."));

            return;
        }

        foreach (var candidate in report.Route.Take(Shown))
        {
            _body.Children.Add(LoadoutPages.Heading(candidate.Engineer.Name));

            _body.Children.Add(new TextBlock
            {
                Text = candidate.Summary(),
                FontSize = TypeScale.Body,
                TextWrapping = TextWrapping.Wrap,
            });

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

        if (report.Route.Count > Shown)
        {
            // Said rather than silently cut. A list that stops at five without saying so reads as
            // a list of everybody who could help.
            _body.Children.Add(LoadoutPages.Muted(
                $"{(report.Route.Count - Shown).ToString(CultureInfo.InvariantCulture)} more ranked "
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
