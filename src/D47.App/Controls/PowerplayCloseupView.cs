using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using static D47.App.Theming.ThemedText;
using D47.Core.Knowledge;

namespace D47.App.Controls;

/// <summary>The chapter the Commander is climbing: one column per rank with its bar segment and what it gives.</summary>
public static class PowerplayCloseupView
{
    private const double SegmentHeight = 22;

    /// <summary>
    /// <paramref name="fill"/> is the share of the next rank done, or null where the merit total is not known;
    /// <paramref name="merits"/> is that total.
    /// </summary>
    public static Control Build(Closeup closeup, int rank, double? fill, long? merits)
    {
        ArgumentNullException.ThrowIfNull(closeup);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
        head.Children.Add(Text($"This chapter · ranks {closeup.Label}".ToUpperInvariant(), TypeScale.Meta, ThemeManager.CyanKey, chrome: true));

        var left = merits is { } have ? (PowerplayRanks.MeritsNeeded(closeup.Last) ?? 0) - have : 0;

        if (left > 0)
        {
            var toGo = Text(
                $"{left.ToString("N0", CultureInfo.CurrentCulture)} merits to rank {closeup.Last}",
                TypeScale.Small,
                ThemeManager.GreyKey,
                mono: true);

            Grid.SetColumn(toGo, 1);
            head.Children.Add(toGo);
        }

        var columns = new Grid();

        for (var index = 0; index < closeup.Ranks.Count; index++)
        {
            if (index > 0)
            {
                columns.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Gaps.Tile)));
            }

            columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var column = Column(closeup.Ranks[index], rank, fill);
            Grid.SetColumn(column, index * 2);
            columns.Children.Add(column);
        }

        return new StackPanel { Children = { head, columns } };
    }

    private static Control Column(CloseupRank entry, int rank, double? fill)
    {
        var held = entry.Rank <= rank;
        var next = entry.Rank == rank + 1;
        var percent = next && fill is { } known
            ? " · " + ((int)Math.Floor(known * 100)).ToString(CultureInfo.InvariantCulture) + "%"
            : string.Empty;

        var number = Text(
            entry.Rank.ToString(CultureInfo.InvariantCulture) + percent,
            TypeScale.Small,
            held ? ThemeManager.WhiteKey : next ? ThemeManager.CyanKey : ThemeManager.GreyKey,
            mono: true);

        var segment = new Border { Height = SegmentHeight, Margin = new Thickness(0, 2, 0, 4) };
        ThemeInk.Bind(segment, Border.BackgroundProperty, held ? ThemeManager.AKey : ThemeManager.TileKey);

        if (next && fill is { } share)
        {
            var part = Math.Clamp(share, 0, 1);

            var filled = new Border();
            ThemeInk.Bind(filled, Border.BackgroundProperty, ThemeManager.CyanKey);

            segment.Child = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(part, GridUnitType.Star), new ColumnDefinition(1 - part, GridUnitType.Star) },
                Children = { filled },
            };
        }

        var words = new StackPanel { Spacing = 2 };

        if (entry.Rewards.Count == 0)
        {
            words.Children.Add(Text("—", TypeScale.Meta, ThemeManager.GreyKey));
        }

        foreach (var reward in entry.Rewards)
        {
            words.Children.Add(Text(reward, TypeScale.Meta, held ? ThemeManager.WhiteKey : ThemeManager.GreyKey));
        }

        return new StackPanel { Children = { number, segment, words } };
    }

}
