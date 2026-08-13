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

/// <summary>
/// The whole coverage record, one line per tool and settings row.
/// <para>
/// A window rather than more of the Diagnostics card. Ninety-odd rows inline would be ninety-odd
/// rows to scroll past to reach anything below them, and the settings surface navigates by
/// scroll position mapped to cards — one card twenty times taller than every other leaves the
/// indicator parked on Diagnostics for most of the scroll and stops being a navigation aid. The
/// card keeps the one-line summary; this is the long form, which is the same split the written
/// report already makes.
/// </para>
/// <para>
/// Off unless <c>D47_COVERAGE</c> is set, like everything else about the record: the row that
/// opens this is not registered at all otherwise.
/// </para>
/// </summary>
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
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(summary, SelectableTextBlock.ForegroundProperty, ThemeManager.TextKey);

        var list = new StackPanel { Spacing = 14 };

        // Same order as the written report, and for the same reason: a list that opens with the
        // work is a list that gets used. Something that ran and errored is a stronger call to
        // action than something never tried, so it leads.
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
            FontSize = 12,
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

    /// <summary>
    /// One line, colour-carrying its own state.
    /// <para>
    /// Three brushes, all of them already in every theme: Accent for exercised, Danger for
    /// something that came back with an error, TextMuted for everything still to do. There is
    /// no green in the palette, and inventing one would mean choosing a value in all five
    /// themes — a lot of colour decisions to make on behalf of a workbench aid.
    /// </para>
    /// <para>
    /// Stale reads muted alongside never-exercised on purpose. It is the state where the last
    /// run proved nothing about what is there now, so as work remaining it is the same as never
    /// having run it. The word carries the difference and the date says how long ago.
    /// </para>
    /// </summary>
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
            FontSize = 10,
            Width = 52,
            VerticalAlignment = VerticalAlignment.Center,

            // Weight as well as colour, because colour alone cannot be relied on here. In the
            // Elite theme Accent is #FF7100 and Danger #FF5555 — both warm, and sixty rows of
            // scrolling apart, so they are never seen together to be told apart. The HUD-matrix
            // theme is worse: its palette comes from the Commander's own matrix file, so no
            // pair of brushes can be guaranteed distinct. The word says which state it is, the
            // section it sits in says it again, and the weight carries it in any palette.
            FontWeight = line.Failed ? FontWeight.Bold : FontWeight.Normal,
        };

        Themed(mark, TextBlock.ForegroundProperty, brush);

        var name = new TextBlock
        {
            Text = line.Item.Name,
            FontSize = 11,
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
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0),
        };

        Themed(when, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // Every line goes somewhere. Knowing a tool has never been tried is only half an answer;
        // the other half is what it was supposed to do, which is its help page.
        var help = new Button
        {
            Name = "CoverageHelp",
            Content = "?",
            FontSize = 10,
            Padding = new Thickness(7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = DocsSite.Capability(line.Item.CapabilityId),
        };

        help.Click += (_, _) => Process.Start(new ProcessStartInfo(
            DocsSite.Capability(line.Item.CapabilityId))
        {
            UseShellExecute = true,
        });

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
            FontSize = 17,
            FontWeight = FontWeight.Medium,
        };

        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        return heading;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
