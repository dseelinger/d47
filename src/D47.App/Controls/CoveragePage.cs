using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Configuration;
using D47.Core.Coverage;

namespace D47.App.Controls;

/// <summary>The Features record, one line per tool and settings row.</summary>
public sealed class CoveragePage : DialogPage
{
    public override string Crumb => "Features";

    private readonly SelectableTextBlock _summary;
    private readonly StackPanel _list;
    private readonly Action<CoverageItem>? _clear;
    private readonly Func<CoverageReport>? _fresh;
    private readonly ViewStateStore? _store;
    private readonly Action<CoverageItem>? _go;
    private readonly Func<CoverageItem, D47.App.Coverage.ToolExercise?>? _exercise;
    private readonly Func<string, bool>? _onPage;
    private readonly CheckBox _showHidden;
    private CoverageReport _report;
    private ViewState _view;

    /// <param name="clear">When given, each failed row gets a Clear button that calls it.</param>
    /// <param name="fresh">Fetches the report to redraw after a clear.</param>
    /// <param name="store">When given, collapsed sections and hidden lines are remembered across visits.</param>
    /// <param name="go">When given, the exercise dialog of a line that has a place gets a GO THERE button that calls it.</param>
    /// <param name="exercise">The steps the exercise dialog shows for a line.</param>
    /// <param name="onPage">Whether a settings row is on its page now; null takes every row as on it.</param>
    public CoveragePage(
        CoverageReport report,
        Action<CoverageItem>? clear = null,
        Func<CoverageReport>? fresh = null,
        ViewStateStore? store = null,
        Action<CoverageItem>? go = null,
        Func<CoverageItem, D47.App.Coverage.ToolExercise?>? exercise = null,
        Func<string, bool>? onPage = null)
    {
        Title = "Features";
        _clear = clear;
        _fresh = fresh;
        _store = store;
        _go = go;
        _exercise = exercise;
        _onPage = onPage;
        _report = report;
        _view = store?.Load() ?? new ViewState();

        _summary = new SelectableTextBlock
        {
            Name = "CoverageSummary",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        ThemeInk.Bind(_summary, SelectableTextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        (_showHidden, _) = LabeledCheckBox.Build("Show hidden items");
        _showHidden.Name = "CoverageShowHidden";
        _showHidden.MinHeight = TypeScale.MinimumTarget;

        _showHidden.IsCheckedChanged += (_, _) => Fill(_report);

        _list = new StackPanel { Spacing = 14 };
        Fill(report);

        var close = new Button { Name = "CoverageClose", Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        Modal.Apply(this, "Diagnostics", Title, _list, [close]);

        Opened += (_, _) => close.Focus();
    }

    private void Fill(CoverageReport report)
    {
        _report = report;

        var hidden = report.Lines.Count(Hidden);

        _summary.Text = hidden > 0 ? $"{report.Summary} ({hidden} hidden)" : report.Summary;
        _list.Children.Clear();
        _list.Children.Add(_summary);
        _list.Children.Add(_showHidden);

        // Same order as the written report, and for the same reason: a list that opens with the work is a
        // list that gets used.
        Section(report, "failed", "Came back with an error", line => line.Failed);
        Section(report, "never", "Never tested", line => !line.Failed && line.Status == CoverageStatus.Never);
        Section(report, "changed", "Changed since you last tested them", line => !line.Failed && line.Status == CoverageStatus.Stale);
        Section(report, "exercised", "Tested", line => !line.Failed && line.Status == CoverageStatus.Exercised);
    }

    private void Section(CoverageReport report, string key, string heading, Func<CoverageLine, bool> include)
    {
        var lines = report.Lines
            .Where(include)
            .Where(line => _showHidden.IsChecked == true || !Hidden(line))
            .OrderBy(line => line.Item.Kind, StringComparer.Ordinal)
            .ThenBy(line => line.Item.Id, StringComparer.Ordinal)
            .ToList();

        if (lines.Count == 0)
        {
            return;
        }

        var id = $"coverage.{key}";
        var text = $"{heading} ({lines.Count})";
        var rows = new StackPanel { Spacing = 2 };

        foreach (var line in lines)
        {
            rows.Children.Add(Row(line));
        }

        var open = _view.IsExpanded(id, startCollapsed: false);
        rows.IsVisible = open;

        var title = new Button
        {
            Name = $"CoverageSection_{key}",
            MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };

        void Show(bool isOpen)
        {
            var named = $"{(isOpen ? "▾" : "▸")} {text}";
            title.Content = TitleText.Build(named, TypeScale.Section, TitleRank.Group);
            AutomationProperties.SetName(title, $"{heading}, {(isOpen ? "expanded" : "collapsed")}");
        }

        Show(open);

        title.Click += (_, _) =>
        {
            var next = !rows.IsVisible;
            rows.IsVisible = next;
            Show(next);
            _view = (_store?.Load() ?? _view).With(id, next);
            _store?.Save(_view);
        };

        _list.Children.Add(new StackPanel { Spacing = 8, Children = { TitleText.GroupRow(title), rows } });
    }

    private static string MarkOf(CoverageLine line) => line switch
    {
        { Failed: true } => "FAILED",
        { Status: CoverageStatus.Exercised } => "DONE",
        { Status: CoverageStatus.Stale } => "CHANGED",
        _ => "NEVER",
    };

    private bool Hidden(CoverageLine line) => _view.IsHidden(line.Item.Key, line.Item.Fingerprint);

    /// <summary>Whether GO THERE has somewhere to land: a settings row on its page now, or a tool with a page.</summary>
    private bool HasPlace(CoverageItem item) => item.Kind == CoverageKind.Setting
        ? _onPage?.Invoke(item.Id) ?? true
        : D47.App.Coverage.ToolPlaces.For(item.Id) is not null;

    private void ShowExercise(CoverageLine line)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var item = line.Item;
        var steps = _exercise?.Invoke(item) ?? new D47.App.Coverage.ToolExercise(item.Name, item.CapabilityId, string.Empty, null);
        Action? goThere = _go is { } go && HasPlace(item) ? () => go(item) : null;

        _ = new ToolExerciseDialog(steps, MarkOf(line), goThere, () => Remember(view => view.Hiding(item.Key, item.Fingerprint))).Over(owner);
    }

    /// <summary>Changes what is stored and redraws, loading fresh first because other writers share the file.</summary>
    private void Remember(Func<ViewState, ViewState> change)
    {
        _view = change(_store?.Load() ?? _view);
        _store?.Save(_view);
        Fill(_report);
    }

    /// <summary>One line, its state on the status ladder: done is met, failed is an error, the rest not met.</summary>
    private Control Row(CoverageLine line)
    {
        var brush = line switch
        {
            { Failed: true } => ThemeManager.RedKey,
            { Status: CoverageStatus.Exercised } => ThemeManager.BlueKey,
            _ => ThemeManager.GreyKey,
        };

        var mark = new TextBlock
        {
            Text = MarkOf(line),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            Width = 64,
            VerticalAlignment = VerticalAlignment.Center,

            // Weight as well as colour, because colour alone cannot be relied on here.
            FontWeight = line.Failed ? FontWeight.Bold : FontWeight.Normal,
        };

        ThemeInk.Bind(mark, TextBlock.ForegroundProperty, brush);

        var name = ListRow.Name(new TextBlock
        {
            Text = line.Item.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        TruncationTip.Watch(name, () => line.Item.Name);

        var when = ListRow.Aside(new TextBlock
        {
            Inlines = [Fonts.Mono(line.LastSeen is { } seen ? seen.ToString("yyyy-MM-dd") : string.Empty)],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0),
        });

        // Every line goes somewhere.
        var help = SiteHelpMark.For(DocsSite.Capability(line.Item.CapabilityId), "CoverageHelp");

        var right = new StackPanel
        {
            [DockPanel.DockProperty] = Dock.Right,
            Orientation = Orientation.Horizontal,
            Children = { when, help },
        };

        if (Hidden(line))
        {
            var unhide = new Button
            {
                Name = "CoverageUnhide",
                Content = "UNHIDE",
                MinHeight = TypeScale.MinimumTarget,
                VerticalAlignment = VerticalAlignment.Center,
            };

            unhide.Click += (_, _) => Remember(view => view.Unhiding(line.Item.Key));

            right.Children.Add(unhide);
        }

        var go = new Button
        {
            Name = "CoverageGo",
            Content = "GO",
            MinHeight = TypeScale.MinimumTarget,
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
        };

        go.Click += (_, _) => ShowExercise(line);

        right.Children.Add(go);

        if (line.Failed && _clear is not null && _fresh is not null)
        {
            var clear = new Button
            {
                Name = "CoverageClear",
                Content = "Clear",
                MinHeight = 44,
                VerticalAlignment = VerticalAlignment.Center,
            };

            clear.Click += (_, _) =>
            {
                _clear(line.Item);
                Fill(_fresh());
            };

            right.Children.Insert(0, clear);
        }

        var row = new Border
        {
            Child = new DockPanel
            {
                Children =
                {
                    mark,
                    right,
                    name,
                },
            },
        };

        return ListRow.Dress(row);
    }

}
