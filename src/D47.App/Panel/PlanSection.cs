using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The PLAN section of a ship's page: what its live plans and construction deliveries still need.</summary>
public static class PlanSection
{
    public static Control Build(PlanShortfall plan)
    {
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(0, 6, 0, 6) };

        stack.Children.Add(ListRow.Head("Plan"));
        stack.Children.Add(Aside(plan));

        if (plan.Rows.Count > 0)
        {
            stack.Children.Add(Table(plan.Rows));
        }

        foreach (var trip in plan.Trips)
        {
            stack.Children.Add(TripNotice(trip));
        }

        foreach (var block in plan.Blocks)
        {
            stack.Children.Add(new Notice(NoticeLevel.Error)
            {
                Label = "Engineer rank",
                Text = block.Sentence,
                Detail = block.Reach,
            });
        }

        if (plan.OneTrip.Count > 0)
        {
            stack.Children.Add(ListRow.Head("Gather in one trip"));
            stack.Children.Add(LoadoutPages.Muted("Missing materials that come from the same kind of place."));

            foreach (var site in plan.OneTrip)
            {
                stack.Children.Add(Site(site));
            }
        }

        stack.Children.Add(LoadoutPages.Muted(
            "Adds up every live plan for this ship, and construction deliveries. Redraws as you collect."));

        return stack;
    }

    /// <summary>"2 plans · 1 construction delivery", for the line under the head.</summary>
    public static string AsideText(PlanShortfall plan) =>
        $"What the live plans for this ship still need · {Count(plan.PlanCount, "plan", "plans")}"
        + $" · {Count(plan.DeliveryCount, "construction delivery", "construction deliveries")}";

    /// <summary>The short column: the amount still missing, or "✓ MET".</summary>
    public static string ShortText(ShortfallRow row) =>
        row.Short == 0 ? "✓ MET" : row.Short.ToString(CultureInfo.InvariantCulture);

    /// <summary>The detail line of a trip notice.</summary>
    public static string TripDetail(ShortfallTrip trip) =>
        $"Need {trip.Need.ToString(CultureInfo.InvariantCulture)}"
        + $" · cap {trip.Cap.ToString(CultureInfo.InvariantCulture)}"
        + $" · {Count(trip.Trips, "trip", "trips")}";

    private static string Count(int count, string one, string many) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many)}";

    private static Control Aside(PlanShortfall plan)
    {
        var text = LoadoutPages.Muted(AsideText(plan).ToUpperInvariant());
        text.FontSize = TypeScale.Meta;
        return text;
    }

    private static Notice TripNotice(ShortfallTrip trip) => new(NoticeLevel.Warning)
    {
        Label = "More than one trip",
        Text = trip.Sentence,
        Detail = TripDetail(trip),
    };

    private static Control Table(IReadOnlyList<ShortfallRow> rows)
    {
        var table = new StackPanel { Spacing = Gaps.Tile };

        foreach (var row in rows)
        {
            table.Children.Add(Row(row));
        }

        return table;
    }

    private static Control Row(ShortfallRow row)
    {
        var grid = new Grid
        {
            MinHeight = TypeScale.MinimumTarget,
            Margin = new Thickness(12, 0),
            ColumnSpacing = 12,
            ColumnDefinitions = new ColumnDefinitions("*,130,110,90,220"),
        };

        var name = LoadoutPages.Toned(
            row.Grade is { } grade ? $"{row.Name} (grade {grade.ToString(CultureInfo.InvariantCulture)})" : row.Name,
            ThemeManager.WhiteKey,
            TypeScale.Secondary);

        var where = LoadoutPages.Toned(LedgerText(row), ThemeManager.GreyKey, TypeScale.Small);

        var held = Mono(
            $"{row.Held.ToString(CultureInfo.InvariantCulture)}/{row.Needed.ToString(CultureInfo.InvariantCulture)}",
            ThemeManager.WhiteKey);

        var met = row.Short == 0;
        var left = Mono(ShortText(row), met ? ThemeManager.BlueKey : ThemeManager.AKey);

        var fill = row.Needed <= 0 ? 1 : (double)row.Held / row.Needed;
        var gauge = Gauge.Track(fill, met ? ThemeManager.BlueKey : Gauge.FillKey(GaugeFill.Capacity));
        gauge.VerticalAlignment = VerticalAlignment.Center;

        Place(grid, name, 0);
        Place(grid, where, 1);
        Place(grid, held, 2);
        Place(grid, left, 3);
        Place(grid, gauge, 4);

        var slab = new Border { Child = grid };
        LoadoutPages.Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private static string LedgerText(ShortfallRow row)
    {
        if (row.Site is { Length: > 0 } site)
        {
            return site;
        }

        return row.Ledger switch
        {
            MaterialLedger.Material => "Materials",
            MaterialLedger.ShipLocker => "Ship locker",
            MaterialLedger.Cargo => "Cargo",
            MaterialLedger.RareCargo => "Rare cargo",
            _ => string.Empty,
        };
    }

    private static Control Site(OneTripSite site)
    {
        var origin = new StackPanel
        {
            Children =
            {
                LoadoutPages.Toned(site.Origin.ToUpperInvariant(), ThemeManager.WhiteKey, TypeScale.Secondary),
                LoadoutPages.Toned(site.Kind.ToUpperInvariant(), ThemeManager.AKey, TypeScale.Meta),
            },
        };

        var gives = LoadoutPages.Toned(string.Join(", ", site.Materials), ThemeManager.GreyKey, TypeScale.Small);
        gives.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            MinHeight = TypeScale.MinimumTarget,
            Margin = new Thickness(12, 4),
            ColumnSpacing = 12,
            ColumnDefinitions = new ColumnDefinitions("260,*"),
        };

        origin.VerticalAlignment = VerticalAlignment.Center;
        Place(grid, origin, 0);
        Place(grid, gives, 1);

        var slab = new Border { Child = grid };
        LoadoutPages.Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private static TextBlock Mono(string text, string key)
    {
        var block = LoadoutPages.Toned(text, key, TypeScale.Control);
        block.FontFamily = new FontFamily(Fonts.MonoFamily);
        return block;
    }

    private static void Place(Grid grid, Control child, int column)
    {
        child.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }
}
