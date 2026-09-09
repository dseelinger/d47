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
public sealed class CoverageWindow : Window
{
    public CoverageWindow(CoverageReport report)
    {
        Title = "Exercised by hand";
        Width = 720;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var summary = new SelectableTextBlock
        {
            Name = "CoverageSummary",
            Text = report.Summary,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(summary, SelectableTextBlock.ForegroundProperty, ThemeManager.TextKey);

        var list = new StackPanel { Spacing = 14 };

        // Same order as the written report, and for the same reason: a list that opens with the work is a
        // list that gets used.
        Section(list, report, "Came back with an error", line => line.Failed);
        Section(list, report, "Never exercised", line => !line.Failed && line.Status == CoverageStatus.Never);
        Section(list, report, "Changed since you last exercised them", line => !line.Failed && line.Status == CoverageStatus.Stale);
        Section(list, report, "Exercised", line => !line.Failed && line.Status == CoverageStatus.Exercised);

        var close = new Button { Name = "CoverageClose", Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        Content = new DockPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Top,
                    Spacing = 4,
                    Margin = new Thickness(0, 0, 0, 16),
                    Children = { Heading("Exercised by hand"), summary },
                },
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0),
                    Children = { close },
                },
                new ScrollViewer
                {
                    Name = "CoverageScroller",
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = list,
                },
            },
        };

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

        var title = new TextBlock
        {
            Text = $"{heading} ({lines.Count})",
            FontSize = TypeScale.Subheading,
            FontWeight = FontWeight.Medium,
        };

        Themed(title, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var rows = new StackPanel { Spacing = 1 };

        foreach (var line in lines)
        {
            rows.Children.Add(Row(line));
        }

        into.Children.Add(new StackPanel { Spacing = 6, Children = { title, rows } });
    }

    /// <summary>One line, colour-carrying its own state.</summary>
    private static Control Row(CoverageLine line)
    {
        var brush = line switch
        {
            { Failed: true } => ThemeManager.DangerKey,
            { Status: CoverageStatus.Exercised } => ThemeManager.AccentKey,
            _ => ThemeManager.TextMutedKey,
        };

        var mark = new TextBlock
        {
            Text = line switch
            {
                { Failed: true } => "failed",
                { Status: CoverageStatus.Exercised } => "done",
                { Status: CoverageStatus.Stale } => "changed",
                _ => "never",
            },
            FontSize = TypeScale.Small,
            Width = 52,
            VerticalAlignment = VerticalAlignment.Center,

            // Weight as well as colour, because colour alone cannot be relied on here.
            FontWeight = line.Failed ? FontWeight.Bold : FontWeight.Normal,
        };

        Themed(mark, TextBlock.ForegroundProperty, brush);

        var name = new TextBlock
        {
            Text = line.Item.Name,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            // The id is the thing to grep for or type at the app; the name is what it reads as.
            [ToolTip.TipProperty] = $"{line.Item.Kind} {line.Item.Id}",
        };

        Themed(name, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var when = new TextBlock
        {
            Text = line.LastSeen is { } seen ? seen.ToString("yyyy-MM-dd") : string.Empty,
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0),
        };

        Themed(when, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // Every line goes somewhere.
        var help = SiteHelpMark.For(DocsSite.Capability(line.Item.CapabilityId), "CoverageHelp");

        var row = new Border
        {
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(3),
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

        Themed(row, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return row;
    }

    private static TextBlock Heading(string text)
    {
        var heading = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.Medium,
        };

        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        return heading;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
