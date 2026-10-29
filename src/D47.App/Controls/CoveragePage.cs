using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
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
    private ViewState _view;

    /// <param name="clear">When given, each failed row gets a Clear button that calls it.</param>
    /// <param name="fresh">Fetches the report to redraw after a clear.</param>
    /// <param name="store">When given, which sections are collapsed is remembered across visits.</param>
    public CoveragePage(
        CoverageReport report,
        Action<CoverageItem>? clear = null,
        Func<CoverageReport>? fresh = null,
        ViewStateStore? store = null)
    {
        Title = "Features";
        _clear = clear;
        _fresh = fresh;
        _store = store;
        _view = store?.Load() ?? new ViewState();

        _summary = new SelectableTextBlock
        {
            Name = "CoverageSummary",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        ThemeInk.Bind(_summary, SelectableTextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        _list = new StackPanel { Spacing = 14 };
        Fill(report);

        var close = new Button { Name = "CoverageClose", Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        Modal.Apply(this, "Diagnostics", Title, _list, [close]);

        Opened += (_, _) => close.Focus();
    }

    private void Fill(CoverageReport report)
    {
        _summary.Text = report.Summary;
        _list.Children.Clear();
        _list.Children.Add(_summary);

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
            Text = line switch
            {
                { Failed: true } => "FAILED",
                { Status: CoverageStatus.Exercised } => "DONE",
                { Status: CoverageStatus.Stale } => "CHANGED",
                _ => "NEVER",
            },
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
