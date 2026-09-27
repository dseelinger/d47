using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Coverage;

namespace D47.App.Controls;

/// <summary>The whole coverage record, one line per tool and settings row.</summary>
public sealed class CoveragePage : DialogPage
{
    public override string Crumb => "Coverage";

    public CoveragePage(CoverageReport report)
    {
        Title = "Exercised by hand";

        var summary = new SelectableTextBlock
        {
            Name = "CoverageSummary",
            Text = report.Summary,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(summary, SelectableTextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var list = new StackPanel { Spacing = 14, Children = { summary } };

        // Same order as the written report, and for the same reason: a list that opens with the work is a
        // list that gets used.
        Section(list, report, "Came back with an error", line => line.Failed);
        Section(list, report, "Never exercised", line => !line.Failed && line.Status == CoverageStatus.Never);
        Section(list, report, "Changed since you last exercised them", line => !line.Failed && line.Status == CoverageStatus.Stale);
        Section(list, report, "Exercised", line => !line.Failed && line.Status == CoverageStatus.Exercised);

        var close = new Button { Name = "CoverageClose", Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        Modal.Apply(this, "Coverage", Title, list, [close]);

        Opened += (_, _) => close.Focus();
    }

    private static void Section(
        StackPanel into,
        CoverageReport report,
        string heading,
        Func<CoverageLine, bool> include)
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

        var title = Modal.Section($"{heading} ({lines.Count})");

        var rows = new StackPanel { Spacing = 2 };

        foreach (var line in lines)
        {
            rows.Children.Add(Row(line));
        }

        into.Children.Add(new StackPanel { Spacing = 8, Children = { title, rows } });
    }

    /// <summary>One line, its state on the status ladder: done is met, failed is an error, the rest not met.</summary>
    private static Control Row(CoverageLine line)
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

        Themed(mark, TextBlock.ForegroundProperty, brush);

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

        var row = new Border
        {
            Child = new DockPanel
            {
                Children =
                {
                    mark,
                    new StackPanel
                    {
                        [DockPanel.DockProperty] = Dock.Right,
                        Orientation = Orientation.Horizontal,
                        Children = { when, help },
                    },
                    name,
                },
            },
        };

        return ListRow.Dress(row);
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
