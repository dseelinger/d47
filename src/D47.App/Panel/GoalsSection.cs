using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>The Goals section of Commander › Progress: every running goal as an arc row that opens in place.</summary>
internal sealed class GoalsSection : UserControl
{
    private readonly D47.Core.Goals.GoalBook _goals;
    private readonly Action? _backfill;
    private readonly Action<string>? _ask;
    private readonly Func<DateTimeOffset> _now;
    private readonly StackPanel _arcs = new() { Spacing = 2, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Notice _problems = new() { IsVisible = false, Margin = new Thickness(0, 0, 0, 10) };
    private string? _openArc;
    private bool _allRanksShown;
    private string? _seen;

    public GoalsSection(
        D47.Core.Goals.GoalBook goals,
        Action? backfill = null,
        Action<string>? ask = null,
        Func<DateTimeOffset>? now = null)
    {
        _goals = goals;
        _backfill = backfill;
        _ask = ask;
        _now = now ?? (() => DateTimeOffset.Now);

        Content = new StackPanel { Children = { _problems, _arcs } };
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _goals.Store.Changed += OnChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _goals.Store.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(() => Refresh());

    /// <summary>Redraws when a goal's standing has moved since the last draw.</summary>
    public bool Tick() => Signature() != _seen && Refresh();

    private string Signature() =>
        string.Join(
            '|',
            _goals.Standings.Select(standing => string.Join(
                ',', standing.Arc.Key, standing.IsDone, standing.Have, standing.Need, standing.Note, standing.Track?.Current, standing.Track?.Fill, standing.Merits)));

    private void Say(string message)
    {
        Refresh();
        _problems.IsVisible = true;
        _problems.Text = message;
    }

    /// <summary>Draws the goals; true.</summary>
    private bool Refresh()
    {
        _arcs.Children.Clear();
        _seen = Signature();

        var standings = _goals.Standings;
        var running = standings.Count(standing => !standing.IsDone);

        _arcs.Children.Add(TitleText.Group($"Goals ({running} running)", "Each goal, how far along it is and what to do next."));

        if (standings.Count == 0)
        {
            _arcs.Children.Add(ChecklistPage.Muted("Every goal is removed."));
        }

        foreach (var standing in standings)
        {
            _arcs.Children.Add(Arc(standing));
        }

        // The button that gives every arc its age.
        if (_goals.Mine is null && _backfill is not null)
        {
            var read = new Button
            {
                Content = "Read my journals",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0),
            };

            read.Click += (_, _) => _backfill();

            _arcs.Children.Add(ChecklistPage.Muted(
                "Nothing here has an age yet, and the milestones have no figures. Reading back "
                + "through the journals already on this disk is what gives them one."));

            _arcs.Children.Add(read);
        }

        AddRemoved();

        return true;
    }

    /// <summary>The Removed list: each removed goal with a Recover button. Nothing is drawn when none is removed.</summary>
    private void AddRemoved()
    {
        var removed = _goals.Removed;

        if (removed.Count == 0)
        {
            return;
        }

        var names = _goals.Everything()
            .Where(standing => removed.Contains(standing.Arc.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (names.Count == 0)
        {
            return;
        }

        _arcs.Children.Add(ChecklistPage.Muted("Removed"));

        foreach (var standing in names)
        {
            var recover = new Button
            {
                Content = "Recover",
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            AutomationProperties.SetName(recover, $"Recover {standing.Arc.Name}");

            recover.Click += (_, _) =>
            {
                Say(_goals.Recover(standing.Arc.Key));
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var name = ChecklistPage.Named(standing.Arc.Name, null, sentence: false);

            Grid.SetColumn(recover, 1);
            row.Children.Add(name);
            row.Children.Add(recover);

            _arcs.Children.Add(ListRow.Dress(new Border { Child = row }, selected: false));
        }
    }

    /// <summary>
    /// One arc: what it is, how far along, how long it has run. Open, its head is the only part filled with A,
    /// and below it, on Slab, sit the figures, the track, what the ranks give and what to do about it.
    /// </summary>
    private Control Arc(D47.Core.Goals.GoalStanding standing)
    {
        var open = standing.Arc.Key == _openArc;
        var track = standing.Track;

        var head = new StackPanel { Spacing = 2 };
        head.Children.Add(NameLine(standing));
        head.Children.Add(ListRow.Sub(new TextBlock { Text = Caption(standing), TextWrapping = TextWrapping.Wrap }));

        var press = ListRow.Dress(new Border { Child = head }, selected: open);

        ListRow.Opens(press, () =>
        {
            _openArc = open ? null : standing.Arc.Key;
            _allRanksShown = false;
            Refresh();
        });

        if (!open)
        {
            if (track is not null)
            {
                head.Children.Add(Spaced(GoalTrackBar.Build(track, GoalTrackBar.RowHeight)));
            }
            else if (CountTrack(standing) is { } count)
            {
                head.Children.Add(Spaced(count));
            }

            AutomationProperties.SetName(press, standing.Arc.Name);

            return press;
        }

        var body = new StackPanel { Spacing = 8 };

        if (track is not null)
        {
            if (standing.Arc.Kind != D47.Core.Goals.GoalKind.Story)
            {
                body.Children.Add(Figures(standing, track));
            }

            body.Children.Add(GoalTrackBar.Build(track, GoalTrackBar.OpenHeight, standing.Benefits));

            if (standing.Benefits is { } benefits)
            {
                if (benefits.Closeup is { } closeup)
                {
                    body.Children.Add(PowerplayCloseupView.Build(
                        closeup,
                        benefits.Rank,
                        standing.Merits is null ? null : track.Fill,
                        standing.Merits));
                }

                body.Children.Add(PowerplayBenefitsView.Build(
                    benefits,
                    _allRanksShown,
                    () =>
                    {
                        _allRanksShown = true;
                        Refresh();
                    }));
            }
        }
        else if (CountTrack(standing) is { } count)
        {
            body.Children.Add(count);
        }

        body.Children.Add(Step(standing));

        var ground = ListRow.Dress(new Border { Child = body, Padding = new Thickness(12, 10) }, selected: false);
        ground.MinHeight = 0;
        ThemeInk.Bind(ground, Border.BackgroundProperty, ThemeManager.SlabKey);

        var card = new StackPanel { Spacing = 0, Children = { press, ground } };
        AutomationProperties.SetName(card, standing.Arc.Name);

        return card;
    }

    /// <summary>A count goal's single unsegmented track; null where the figure is unknown.</summary>
    private static Control? CountTrack(D47.Core.Goals.GoalStanding standing)
    {
        var fill = standing.IsDone ? 1 : standing.Fraction;

        return fill is { } known && standing.Arc.Top is null
            ? D47.App.Controls.Gauge.Track(known, ThemeManager.AKey)
            : null;
    }

    private static Control Spaced(Control control)
    {
        control.Margin = new Thickness(0, 4, 0, 0);
        return control;
    }

    /// <summary>The goal's name, led by a tick once done, with its whole percent in mono at the right.</summary>
    private static Control NameLine(D47.Core.Goals.GoalStanding standing)
    {
        var name = ChecklistPage.Named(standing.Arc.Name, standing.IsDone ? ChecklistPage.Met : null, sentence: false);

        if (standing.Track is not { } track)
        {
            return name;
        }

        var percent = ListRow.NameInk(new TextBlock
        {
            Text = GoalTrackBar.Whole(track),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = ListRow.NameSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });

        DockPanel.SetDock(percent, Dock.Right);

        return new DockPanel { Children = { percent, name } };
    }

    /// <summary>Rank, the percent into it, the next rank and the ranks to go, as four figures with no tile behind them.</summary>
    private static Control Figures(D47.Core.Goals.GoalStanding standing, D47.Core.Goals.GoalTrack track)
    {
        var powerplay = standing.Benefits is not null;
        var next = standing.Have is { } rank ? (int)rank + 1 : 0;

        var rankName = powerplay
            ? standing.Have?.ToString(CultureInfo.CurrentCulture) ?? "—"
            : track.IsDone ? standing.Arc.Top ?? "—" : track.Segments[track.Current].Name;

        var known = powerplay ? standing.Merits is not null : track.Fill is not null;

        var into = track.IsDone
            ? "—"
            : known && track.Fill is { } fill ? ((int)Math.Floor(fill * 100)).ToString(CultureInfo.InvariantCulture) + "%" : "not known yet";

        var toGo = standing.Have is { } have && standing.Need is { } need
            ? Math.Max(0, need - have).ToString("N0", CultureInfo.CurrentCulture)
            : "not known yet";

        (string Label, string Value, bool Chrome, string Key, string Sub)[] figures;

        if (powerplay)
        {
            var top = D47.Core.Goals.GoalCatalogue.PowerplayTop;
            var floor = D47.Core.Knowledge.PowerplayRanks.MeritsNeeded((int)(standing.Have ?? 0)) ?? 0;
            var span = (D47.Core.Knowledge.PowerplayRanks.MeritsNeeded(next) ?? floor) - floor;

            var done = standing.Merits is { } merits && !track.IsDone
                ? $"{Math.Clamp(merits - floor, 0, span).ToString("N0", CultureInfo.CurrentCulture)} / {span.ToString("N0", CultureInfo.CurrentCulture)} merits"
                : string.Empty;

            figures =
            [
                ("Rank", rankName, true, ThemeManager.WhiteKey, $"of {top}"),
                (track.IsDone ? "To rank" : $"To rank {next}", into, false, ThemeManager.CyanKey, done),
                ("Merits", standing.Merits?.ToString("N0", CultureInfo.CurrentCulture) ?? "not known yet", false, ThemeManager.WhiteKey, $"of {(D47.Core.Knowledge.PowerplayRanks.MeritsNeeded(top) ?? 0).ToString("N0", CultureInfo.CurrentCulture)} for rank {top}"),
                ("Ranks to go", toGo, false, ThemeManager.WhiteKey, $"to rank {top}"),
            ];
        }
        else
        {
            figures =
            [
                ("Rank", rankName, true, ThemeManager.WhiteKey, standing.Arc.Top is { } topRank ? $"of {topRank}" : string.Empty),
                ("Into this rank", into, false, ThemeManager.CyanKey, string.Empty),
                ("Next rank", standing.NextRank ?? "—", true, ThemeManager.WhiteKey, string.Empty),
                ("Ranks to go", toGo, false, ThemeManager.WhiteKey, standing.Arc.Top is { } toRank ? $"to {toRank}" : string.Empty),
            ];
        }

        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };

        for (var index = 0; index < figures.Length; index++)
        {
            var figure = Figure(figures[index].Label, figures[index].Value, figures[index].Chrome, figures[index].Key, figures[index].Sub);
            Grid.SetColumn(figure, index);
            strip.Children.Add(figure);
        }

        return strip;
    }

    private static Control Figure(string label, string value, bool chrome, string key, string sub)
    {
        var name = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.MetaSmall,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };

        ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var reading = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily(chrome ? Fonts.ChromeFamily : Fonts.MonoFamily),
            FontSize = chrome ? TypeScale.Title : TypeScale.Readout,
            TextWrapping = TextWrapping.Wrap,
        };

        ThemeInk.Bind(reading, TextBlock.ForegroundProperty, key);

        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 8, 0), Children = { name, reading } };

        if (sub.Length > 0)
        {
            var line = new TextBlock { Text = sub, FontSize = TypeScale.Meta, TextWrapping = TextWrapping.Wrap };
            ThemeInk.Bind(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            panel.Children.Add(line);
        }

        return panel;
    }

    /// <summary>The caption under an arc: where it stands, where the figure came from, and its age.</summary>
    private string Caption(D47.Core.Goals.GoalStanding standing)
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
            // Never "0".
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
    /// </summary>
    private Control Step(D47.Core.Goals.GoalStanding standing)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };

        var step = _goals.Next(standing.Arc.Key);

        panel.Children.Add(ListRow.NameInk(new TextBlock
        {
            Text = step?.Say ?? standing.Arc.Done,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        }));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

        if (step is { CanPropose: true })
        {
            var promote = new Button { Content = "Suggest a line" };

            promote.Click += (_, _) =>
            {
                Say(_goals.Promote(standing.Arc.Key));
            };

            buttons.Children.Add(promote);
        }

        if (!standing.IsDone && standing.Arc.Ask is { } question && _ask is not null)
        {
            var ask = new Button { Content = "Ask D47" };

            ask.Click += (_, _) => _ask(question);

            buttons.Children.Add(ask);
        }

        var remove = new Button { Content = "Remove" };

        remove.Click += (_, _) =>
        {
            _openArc = null;
            Say(_goals.Remove(standing.Arc.Key));
        };

        buttons.Children.Add(remove);
        panel.Children.Add(buttons);

        return panel;
    }
}
