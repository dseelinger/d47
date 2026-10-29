using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The period both Credits pages show; carried from the root to the transactions page and back.</summary>
public sealed class CreditsChoice
{
    public int Period { get; set; } = (int)CombatPeriod.Week;
}

/// <summary>The controls and cells the two Credits pages share.</summary>
internal static class CreditsKit
{
    public const string Dash = "—";

    private static readonly string[] Periods = ["24 hours", "7 days", "30 days", "Everything"];

    public static Segment PeriodSegment() => new Segment
    {
        Name = "CreditsPeriod",
        ItemsSource = Periods,
        SelectedIndex = (int)CombatPeriod.Week,
        Width = 140.0 * Periods.Length,
        Height = TypeScale.MinimumTarget,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Sets <paramref name="current"/> to the trimmed <paramref name="query"/>; false when it is unchanged.</summary>
    public static bool Retarget(string? query, ref string current)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed == current)
        {
            return false;
        }

        current = trimmed;

        return true;
    }

    /// <summary>Whether the ledger, the history walk, the minute or the chosen period differs from what was last drawn.</summary>
    public static bool Moved(CreditLedger? ledger, CreditLedger? seenLedger, HistoryState history, HistoryState seenHistory, DateTimeOffset now, long seenMinute, CreditsChoice choice, Segment period) =>
        !(ReferenceEquals(ledger, seenLedger) && history == seenHistory && Minute(now) == seenMinute && choice.Period == period.SelectedIndex);

    public static long Minute(DateTimeOffset now) => now.ToUnixTimeSeconds() / 60;

    public static CombatPeriod PeriodOf(Segment segment) =>
        (CombatPeriod)Math.Clamp(segment.SelectedIndex, 0, Periods.Length - 1);

    public static Run White(string text)
    {
        var run = Fonts.Mono(text);
        LoadoutPages.Themed(run, TextElement.ForegroundProperty, ThemeManager.WhiteKey);

        return run;
    }

    public static Run Colored(string text, string key)
    {
        var run = Fonts.Mono(text);
        LoadoutPages.Themed(run, TextElement.ForegroundProperty, key);

        return run;
    }

    public static TextBlock Line(string text, string family, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = size,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    public static TextBlock Mono(string text, double size, string key, bool right)
    {
        var block = Line(text, Fonts.MonoFamily, size, key);

        if (right)
        {
            block.TextAlignment = TextAlignment.Right;
            block.HorizontalAlignment = HorizontalAlignment.Right;
        }

        return block;
    }

    public static Border Message(string text)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(14, 12),
            Child = RoutingKit.Ink(text, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true),
        };
        RoutingKit.Themed(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    /// <summary>A grey note under <paramref name="top"/> of margin; <paramref name="full"/> wraps at the page width, not <see cref="RoutingKit.ProseWidth"/>.</summary>
    public static TextBlock Note(string text, double top, bool full = false)
    {
        var note = RoutingKit.Ink(text, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        note.Margin = new Thickness(0, top, 0, 0);

        if (full)
        {
            note.MaxWidth = double.PositiveInfinity;
            note.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        return note;
    }

    public static Notice Warning(DateTimeOffset? since) => new(NoticeLevel.Warning)
    {
        Label = CreditsModel.FailedLabel,
        Text = CreditsModel.FailedText(since),
        Margin = new Thickness(0, 0, 0, 14),
    };

    public static void Place(Grid grid, int column, Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    /// <summary>A grey chrome label.</summary>
    public static TextBlock Label(string text)
    {
        var label = TitleText.Build(text, TypeScale.Meta, TitleRank.Subgroup);
        label.FontFamily = Fonts.ChromeFamily;
        LoadoutPages.Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return label;
    }

    /// <summary>The header row of <paramref name="table"/>: one cell per column of <paramref name="head"/>, an empty cell for any column past them.</summary>
    public static Grid HeaderRow<TRow>(SortHeader<TRow> head, TableLayout table, Func<int, HorizontalAlignment> alignment)
    {
        var cells = new List<Control>();

        for (var i = 0; i < table.Columns.Count; i++)
        {
            cells.Add(i < head.Columns.Count ? head.Cell(i, alignment(i)) : new Border());
        }

        var grid = table.Header(cells);
        grid.MinHeight = TypeScale.MinimumTarget;
        grid.Margin = new Thickness(14, 0);

        return grid;
    }
}
