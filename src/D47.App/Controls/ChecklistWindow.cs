using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Checklists;

namespace D47.App.Controls;

/// <summary>
/// The checklist panel (list.md Phase 17, "TheApp keeps 'The Ultimate' checklist").
/// <para>
/// <b>One surface.</b> The Commander's own lines, their ship builds and their construction sites
/// are all here, because "what am I working on" must have exactly one answer. What is kept apart
/// is how "done" is decided: an authored line has a checkbox, and a derived one has the journal's
/// verdict beside it and no checkbox at all. <b>A derived item cannot be ticked by hand</b>, and
/// this is where that is most visible — there is nothing to press, and the line says why.
/// </para>
/// <para>
/// <b>Completed items are kept.</b> They sit below the line with their count showing, so forty
/// finished ones never bury the six still open, and a fortnight of progress is visible rather
/// than merely recorded.
/// </para>
/// <para>
/// <b>It reflects changes live.</b> The store raises <see cref="ChecklistStore.Changed"/> whoever
/// wrote — the panel, a voice command, or a text editor — and this rebuilds from it. That is what
/// makes the panel a view of the file rather than a second copy of it, exactly as
/// <see cref="MacroWindow"/> is a convenience over <c>macros.json</c>.
/// </para>
/// <para>
/// <b>It is also where committing lives.</b> Accepting a proposal is the Commander's own act and
/// is unreachable from the model (<see cref="D47.Core.Capabilities.ToolDefinition.Protected"/>),
/// so a button here and a spoken phrase are the two ways in.
/// </para>
/// </summary>
public sealed class ChecklistWindow : Window
{
    private const string All = "everything";

    private readonly ChecklistService _checklists;
    private readonly StackPanel _list = new() { Spacing = 6 };
    private readonly ComboBox _filter = new() { Width = 200 };
    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    private string _chosen = All;

    public ChecklistWindow(ChecklistService checklists)
    {
        _checklists = checklists;

        Title = "Checklist";
        Width = 720;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var header = new TextBlock
        {
            Text = $"Saved to {checklists.List.Path}. Proposals wait in {checklists.Proposals.Path} "
                   + "until you accept them — D47 writes that file and never this one.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(header, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        _filter.SelectionChanged += (_, _) =>
        {
            _chosen = _filter.SelectedItem as string ?? All;
            Rebuild();
        };

        var close = new Button { Content = "Close", Padding = new Thickness(14, 4) };
        close.Click += (_, _) => Close();

        var root = new DockPanel { Margin = new Thickness(16) };

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { new TextBlock { Text = "Show", VerticalAlignment = VerticalAlignment.Center }, _filter },
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { close },
        };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);

        root.Children.Add(header);
        root.Children.Add(_problems);
        root.Children.Add(top);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = _list, Margin = new Thickness(0, 12, 0, 0) });

        Content = root;
        Themed(this, BackgroundProperty, ThemeManager.SurfaceKey);

        checklists.List.Changed += OnChanged;
        checklists.Proposals.Changed += OnChanged;

        Closed += (_, _) =>
        {
            checklists.List.Changed -= OnChanged;
            checklists.Proposals.Changed -= OnChanged;
        };

        Rebuild();
    }

    /// <summary>
    /// The store raises this from whichever thread wrote — the tick loop, most often — and every
    /// control here belongs to the UI thread, so the hop is not optional.
    /// </summary>
    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        RebuildFilters();

        _list.Children.Clear();

        var document = _checklists.Document;
        var pending = _checklists.Proposals.PendingFor(document.CommanderFid);

        foreach (var proposal in pending)
        {
            _list.Children.Add(BuildProposal(proposal));
        }

        var live = document.Items
            .Where(item => item.IsLive && Matches(item))
            .ToList();

        var open = live.Where(item => !item.IsComplete).ToList();
        var done = live.Where(item => item.IsComplete).ToList();

        if (live.Count == 0 && pending.Count == 0)
        {
            _list.Children.Add(Muted(
                "Nothing here yet. Say \"add buy limpets to my checklist\", or ask for a build plan, "
                + "and D47 will propose it."));
        }

        foreach (var scope in open.Select(item => item.Scope).Distinct())
        {
            _list.Children.Add(Heading(scope.ToString()));

            foreach (var item in open.Where(item => item.Scope.Same(scope)))
            {
                _list.Children.Add(BuildItem(item));
            }
        }

        if (done.Count > 0)
        {
            // Below the line and counted, never removed. Seeing how far you have come is most of
            // the point of a list that runs for weeks.
            _list.Children.Add(Heading($"Done ({done.Count})"));

            foreach (var item in done)
            {
                _list.Children.Add(BuildItem(item));
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
    /// The filter row is a projection of what is actually on the list — kind, source, group and
    /// state — rather than a hand-written set. A fourth kind of plan appears here without anybody
    /// remembering to add it, the same way the settings surface projects the capability registry.
    /// </summary>
    private void RebuildFilters()
    {
        var options = new List<string> { All };
        options.AddRange(_checklists.Filters());

        if (_filter.ItemsSource is IReadOnlyList<string> existing && existing.SequenceEqual(options))
        {
            return;
        }

        _filter.ItemsSource = options;

        if (!options.Contains(_chosen, StringComparer.Ordinal))
        {
            _chosen = All;
        }

        _filter.SelectedItem = _chosen;
    }

    private bool Matches(ChecklistItem item)
    {
        if (_chosen == All)
        {
            return true;
        }

        return _chosen.Equals(item.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
               || _chosen.Equals(item.Source.ToString(), StringComparison.OrdinalIgnoreCase)
               || _chosen.Equals(item.Scope.Group.ToString(), StringComparison.OrdinalIgnoreCase)
               || _chosen.Equals(item.IsComplete ? "complete" : "open", StringComparison.OrdinalIgnoreCase);
    }

    private Control BuildItem(ChecklistItem item)
    {
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

            var remove = new Button
            {
                Content = "Remove",
                Padding = new Thickness(10, 2),
                FontSize = TypeScale.Body,
            };

            remove.Click += (_, _) => Say(_checklists.Delete(item.Id).Report);

            body.Children.Add(new DockPanel
            {
                Children = { Right(remove), tick },
            });
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

            var verdict = _checklists.Verdict(item);

            body.Children.Add(Muted(
                verdict?.Reason
                ?? $"Worked out from your journal — {item.State.ToString().ToLowerInvariant()}. Not ticked by hand."));
        }

        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            Child = body,
        };
        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return card;
    }

    private Control BuildProposal(ChecklistProposal proposal)
    {
        var accept = new Button { Content = "Accept", Padding = new Thickness(12, 2), FontSize = TypeScale.Body };
        accept.Click += (_, _) => Say(_checklists.Accept(proposal.Id));

        var decline = new Button { Content = "Decline", Padding = new Thickness(12, 2), FontSize = TypeScale.Body };
        decline.Click += (_, _) => Say(_checklists.Decline(proposal.Id));

        var body = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = proposal.Summary, TextWrapping = TextWrapping.Wrap },
                Muted("D47 proposed this and cannot make it itself."),
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

    private static Control Right(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(control, Dock.Right);
        return control;
    }

    private static TextBlock Heading(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 10, 0, 2),
        };

        return block;
    }

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
