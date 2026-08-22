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
            return new EngineerPage(source, crumb.Key[WhoPrefix.Length..], nav);
        }

        return crumb.Key == RouteRoot
            ? new EngineerRoutePage(source, nav)
            : new EngineerDirectoryPage(source, nav);
    }

    /// <summary>
    /// The crumb for one engineer, keyed on the id the journal writes rather than a name.
    /// <para>
    /// <b>Levelled</b>, so choosing another engineer replaces the one that is open rather than
    /// nesting under it (remediation.md 13, items 6 and 7). Without it a wide panel showed two and
    /// three engineers side by side, and the trail read <c>Directory › Farseer › Tani › Ryder</c>
    /// — a route through three people at once. It is the same fault the Loadout tab had, fixed
    /// the same way (remediation.md 11, item 5); this tab pushes its crumb from a different place
    /// and never got the same treatment.
    /// </para>
    /// <para>
    /// It is also why backing out to the Directory first did not help: the trail was
    /// <c>Directory › Farseer</c>, Back made it <c>Directory</c>, and pressing another name
    /// pushed onto <em>that</em> — which is the correct behaviour of a stack and the wrong
    /// behaviour for a chooser. Levelling makes both routes do the same thing.
    /// </para>
    /// </summary>
    public static NavCrumb Crumb(Engineer engineer) =>
        new(WhoPrefix + engineer.Id.ToString(CultureInfo.InvariantCulture), engineer.Name)
        {
            Level = WhoPrefix,
        };

    /// <summary>
    /// An engineer's name, pressable, wherever it is shown (remediation.md 12, item 7).
    /// <para>
    /// The directory's rows already opened the engineer behind them. Everywhere else a name
    /// appeared it was ordinary text — the ranked candidates on the Route, and every stop of a
    /// chain, which is exactly where a Commander is reading about somebody they have not met and
    /// wants to know where they are.
    /// </para>
    /// <para>
    /// A button styled as the text it replaces rather than a link: the surface has no hover on
    /// the headset, so a name that only announced itself by changing colour under a pointer would
    /// announce itself to nobody there.
    /// </para>
    /// </summary>
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

            // Tall enough for a ray at a metre, which is the floor every pressable thing on this
            // surface has.
            MinHeight = 30,
        };

        button.Click += (_, _) => nav.Drill(Crumb(engineer));

        return button;
    }

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
    protected EngineerPageBase(EngineerSource source) => Source = source;

    protected EngineerSource Source { get; }

    protected abstract void Refresh();

    /// <summary>
    /// Attach to detach, for the reason <see cref="LoadoutPage"/> spells out
    /// (remediation.md 13, item 1). These levels are cached by the same strip and were the same
    /// mismatched pair; nobody has reported it here, and it is the same bug waiting.
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
    private readonly Button _colonia;
    private string? _query;

    /// <summary>
    /// Whether the eight engineers out at Colonia are on the list (remediation.md 13, item 11).
    /// <para>
    /// <b>On by default</b>, because hiding a third of the directory from a Commander who has
    /// never said they want it hidden is the panel deciding what they own. One press takes them
    /// off, and the button says which question it is answering rather than merely which state it
    /// is in — the same shape the gap page's filter has.
    /// </para>
    /// </summary>
    private bool _far = true;

    public EngineerDirectoryPage(EngineerSource source, PanelNavigator nav)
        : base(source)
    {
        _nav = nav;

        _colonia = LoadoutPages.Press(string.Empty, () =>
        {
            _far = !_far;
            Refresh();
        });

        _colonia.Margin = new Thickness(0, 0, 0, 10);

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("who should I unlock next");

        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(_colonia, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_summary);
        root.Children.Add(_colonia);
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

        // Short enough to survive a narrow pane: a button does not wrap, and the first draft of
        // this label was cut off mid-word at the default panel width.
        _colonia.Content = _far ? "Hide the Colonia eight" : "Show Colonia again";

        var shown = report.Directory.Where(Matches).Where(Near).ToList();

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

    /// <summary>
    /// Whether this engineer survives the Colonia filter (remediation.md 13, item 11).
    /// <para>
    /// An engineer d47 has no position for is <b>kept</b> whichever way the filter is set: the
    /// question the button asks is "are they out at Colonia", and "I do not know where they are"
    /// is not a yes.
    /// </para>
    /// </summary>
    private bool Near(EngineerEntry entry) =>
        _far || entry.Engineer.IsFarFromTheBubble != true;

    private bool Matches(EngineerEntry entry) =>
        _query is not { } query
        || entry.Engineer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (entry.Engineer.System?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || entry.Engineer.Specialities.Any(speciality =>
            speciality.Kind.Contains(query, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The heading over each group (remediation.md 12, items 8, 9 and 10).
    /// <para>
    /// <b>The game's vocabulary rather than this page's.</b> These were sentences — "You can go
    /// and get these now", "Already yours", "Behind somebody else" — written on the argument that
    /// nobody should have to learn a term to read a list. That argument is right about inventing
    /// a term and wrong about this list: a Commander reading it has already learnt <em>unlock</em>
    /// and <em>invitation</em> from the game, so a sentence that avoids both words is a
    /// translation away from the vocabulary they came in with.
    /// </para>
    /// <para>
    /// The third one was <b>"Requires Engineer Intro First"</b> until 2026-08-22, and it is the
    /// rule above applied badly rather than a different rule: <em>Intro</em> is an abbreviation
    /// of a word the game does not use, and <em>First</em> says again what <em>Requires</em>
    /// already said. <b>Referral</b> is the word for this — it is what every chain on the
    /// documentation page is already called — so the label now uses it and stays the length of
    /// the two beside it. Found by writing the tab's in-app help: it was the one heading that had
    /// to be explained rather than read.
    /// </para>
    /// </summary>
    private static string Caption(EngineerReach reach) => reach switch
    {
        EngineerReach.WithinReach => "Ready for Unlock",
        EngineerReach.Unlocked => "Unlocked",
        _ => "Needs a Referral",
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

        _body.Children.Add(new TextBlock
        {
            Text = engineer.Where,
            FontSize = TypeScale.Body,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        _body.Children.Add(LoadoutPages.Muted(entry.Aside));

        // One per line rather than one running clause (remediation.md 16, item 6). Nine of them
        // set as prose wrap into a paragraph that has to be read to find out whether the one you
        // came for is in it, and the commas between entries look like the commas inside them.
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

        // What it takes, with what is already done marked (remediation.md 13, item 12). All of
        // it rather than only what is outstanding: the chain below says what to go and do, and a
        // referral earned a month ago vanishing from the list reads as a requirement that was
        // never there.
        if (entry.Criteria.Count > 0)
        {
            _body.Children.Add(LoadoutPages.Heading("What it takes"));

            foreach (var criterion in entry.Criteria)
            {
                _body.Children.Add(new TextBlock
                {
                    Text = criterion.Describe(),
                    FontSize = TypeScale.Body,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = App.Current!
                        .GetResourceObservable(criterion.Met == true
                            ? ThemeManager.AccentKey
                            : ThemeManager.TextMutedKey)
                        .ToBinding(),
                });
            }
        }

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
                // The name opens that engineer, and the rest of the stop stays beside it
                // (remediation.md 12, item 7). A chain is where somebody the Commander has never
                // heard of is first mentioned, so it is the place the question is asked.
                _body.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        EngineersPages.Name(step.Engineer, _nav, TypeScale.Body),
                        new TextBlock
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

    private readonly PanelNavigator _nav;

    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly TextBlock _said = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Margin = new Thickness(0, 8, 0, 0),
    };

    public EngineerRoutePage(EngineerSource source, PanelNavigator nav)
        : base(source)
    {
        _nav = nav;
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
            // The ranked name opens that engineer rather than merely heading a block
            // (remediation.md 12, item 7).
            _body.Children.Add(EngineersPages.Name(
                candidate.Engineer, _nav, TypeScale.Body, FontWeight.SemiBold));

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
