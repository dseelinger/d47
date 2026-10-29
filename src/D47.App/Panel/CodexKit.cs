using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The period both Codex pages show; carried from the root to a region's page and back.</summary>
public sealed class CodexChoice
{
    public int Period { get; set; } = (int)CodexPeriod.Trip;
}

/// <summary>One cell of a Codex strip: the label, the figure, the figure's ink and the line under it.</summary>
internal readonly record struct StripCell(string Label, int Value, string Key, string Line);

/// <summary>The controls the Codex pages share.</summary>
internal static class CodexKit
{
    public static Segment PeriodSegment() => new Segment
    {
        Name = "CodexPeriod",
        ItemsSource = CodexModel.Periods,
        SelectedIndex = (int)CodexPeriod.Trip,
        Width = 140.0 * CodexModel.Periods.Count,
        Height = TypeScale.MinimumTarget,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static CodexPeriod PeriodOf(Segment segment) =>
        (CodexPeriod)Math.Clamp(segment.SelectedIndex, 0, CodexModel.Periods.Count - 1);

    /// <summary>The row the period segment sits in; <see cref="Since"/> goes at its right.</summary>
    public static Grid PeriodRow(Segment period)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(period);

        return row;
    }

    /// <summary>"On record since 2025-03-13", at the right of the period row.</summary>
    public static TextBlock Since(DateTimeOffset at, bool failed)
    {
        var note = RoutingKit.Ink(string.Empty, TypeScale.ControlLarge, ThemeManager.GreyKey);
        note.Inlines!.Add(new Run("On record since "));
        note.Inlines.Add(CreditsKit.White(CodexModel.OnRecord(at, failed)));
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.VerticalAlignment = VerticalAlignment.Center;
        note.TextAlignment = Avalonia.Media.TextAlignment.Right;
        note.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        Grid.SetColumn(note, 1);

        return note;
    }

    /// <summary>Replaces the note in <paramref name="row"/>, if there is one, with the one for <paramref name="since"/>.</summary>
    public static void ShowSince(Grid row, DateTimeOffset? since, bool failed)
    {
        foreach (var old in row.Children.Where(child => Grid.GetColumn(child) == 1).ToList())
        {
            row.Children.Remove(old);
        }

        if (since is { } at)
        {
            row.Children.Add(Since(at, failed));
        }
    }

    public static TextBlock Line(string text) => RoutingKit.Ink(text, TypeScale.ControlLarge, ThemeManager.GreyKey);

    public static Notice Warning(DateTimeOffset? since) => new(NoticeLevel.Warning)
    {
        Label = CodexModel.FailedLabel,
        Text = CodexModel.FailedText(since),
        Margin = new Thickness(0, 0, 0, 14),
    };

    /// <summary>Four read-only cells on Slab: Entries, New, Logged and <paramref name="last"/>.</summary>
    public static Grid Strip(int entries, int added, int logged, string lastLabel, int last) => Strip(
        new StripCell("Entries", entries, ThemeManager.AKey, "codex scans"),
        new StripCell("New", added, ThemeManager.BlueKey, "new to your codex"),
        new StripCell("Logged", logged, ThemeManager.AKey, "distinct entries"),
        new StripCell(lastLabel, last, ThemeManager.AKey, "with entries"));

    /// <summary>Read-only cells on Slab, side by side.</summary>
    public static Grid Strip(params StripCell[] cells)
    {
        var strip = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(',', cells.Select(_ => "*"))),
            ColumnSpacing = Gaps.Tile,
            Margin = new Thickness(0, 10, 0, 10),
        };

        for (var i = 0; i < cells.Length; i++)
        {
            Cell(strip, i, cells[i]);
        }

        return strip;
    }

    private static void Cell(Grid strip, int column, StripCell cell)
    {
        var (label, value, key, line) = cell;
        var border = new Border
        {
            Name = "CodexStrip." + label,
            Padding = new Thickness(12, 8),
            Child = new StackPanel
            {
                Children =
                {
                    CreditsKit.Label(label),
                    CreditsKit.Mono(CodexModel.Number(value), TypeScale.Small, key, right: false),
                    CreditsKit.Line(line, Fonts.ProseFamily, TypeScale.Meta, ThemeManager.GreyKey),
                },
            },
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);
        Grid.SetColumn(border, column);
        strip.Children.Add(border);
    }
}
