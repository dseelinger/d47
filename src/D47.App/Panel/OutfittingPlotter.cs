using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>What a ship page needs to plot an outfitting route and say why it cannot.</summary>
/// <param name="Registry">How the plot is made: the same tool the voice calls.</param>
/// <param name="Plans">Read before and after a plot, to tell whether it stored a new plan.</param>
/// <param name="LookupsEnabled">Whether "Look things up in the galaxy" is on.</param>
/// <param name="Here">The Commander's system, for the hint.</param>
public sealed record OutfittingPlotter(
    CapabilityRegistry Registry,
    RoutePlanBook Plans,
    Func<bool> LookupsEnabled,
    Func<string?> Here)
{
    /// <summary>The reason shown, and set as the tooltip, while the galaxy setting is off.</summary>
    public const string SwitchedOff =
        "Route planning is switched off. It shares the galaxy search setting, so turning on "
        + "“Look things up in the galaxy” switches both on.";

    /// <summary>The tool's search radius when the call names none.</summary>
    private const int Radius = 50;

    /// <summary>The to-buy block for a build, or null when it needs nothing bought.</summary>
    public LoadoutToBuy? For(ShipBuild build, ShipPurchases purchases)
    {
        if (purchases.Items.Count == 0)
        {
            return null;
        }

        var names = purchases.Items.Select(purchase => purchase.Item.Display).ToList();
        var kinds = names.Distinct(StringComparer.Ordinal).ToList();
        var subhead = !build.IsOwned
            ? purchases.StorageChecked ? "The hull, and planned modules not in storage" : "The hull, and planned modules"
            : purchases.StorageChecked
                ? "Planned modules not fitted in their slot and not in storage"
                : "Planned modules not fitted in their slot";

        var from = Here() is { Length: > 0 } system ? system : "where you are";
        var enabled = LookupsEnabled();

        return new LoadoutToBuy(
            subhead,
            Summary(names),
            $"Plots from {from}, within {Radius} ly, and opens the route on Navigation › Plan.",
            purchases.StorageChecked,
            enabled ? null : SwitchedOff,
            Plot: async cancellationToken =>
            {
                var before = Plans.Last(RoutePlanKind.Outfitting);

                var result = await Registry
                    .InvokeAsync(
                        "plot_outfitting_route",
                        new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["items"] = string.Join(", ", kinds),
                            ["max_distance"] = Radius.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["storage_checked"] = purchases.StorageChecked ? "true" : "false",
                        }),
                        cancellationToken)
                    .ConfigureAwait(true);

                var after = Plans.Last(RoutePlanKind.Outfitting);

                return new LoadoutToBuyOutcome(
                    result.Content,
                    result.IsError,
                    after is not null && !ReferenceEquals(before, after)
                        ? RoutingPages.ResultCrumb(RoutePlanKind.Outfitting, after.Headline)
                        : null);
            });
    }

    /// <summary>"6 items to buy: A, 2 × B, C and 2 more."</summary>
    private static string Summary(IReadOnlyList<string> names)
    {
        var groups = names.GroupBy(name => name, StringComparer.Ordinal).Select(group => (Name: group.Key, Count: group.Count())).ToList();
        var count = names.Count;

        string Said((string Name, int Count) group) => group.Count == 1 ? group.Name : $"{group.Count} × {group.Name}";

        if (count == 1)
        {
            return $"1 item to buy: {names[0]}.";
        }

        var shown = groups.Take(3).ToList();
        var rest = count - shown.Sum(group => group.Count);

        var listed = rest == 0
            ? shown.Count == 1
                ? Said(shown[0])
                : $"{string.Join(", ", shown.Take(shown.Count - 1).Select(Said))} and {Said(shown[^1])}"
            : $"{string.Join(", ", shown.Select(Said))} and {rest} more";

        return $"{count} items to buy: {listed}.";
    }
}

/// <summary>The to-buy block on a ship's page.</summary>
/// <param name="Subhead">What the list counts, which depends on storage and on owning the hull.</param>
/// <param name="Summary">The items, three named and the rest counted.</param>
/// <param name="Hint">Where the route starts and what pressing the button does.</param>
/// <param name="StorageChecked">False until module storage has been seen.</param>
/// <param name="Unavailable">Why the button is disabled, or null when it is not.</param>
/// <param name="Plot">Plots the route and says what happened.</param>
public sealed record LoadoutToBuy(
    string Subhead,
    string Summary,
    string Hint,
    bool StorageChecked,
    string? Unavailable,
    Func<CancellationToken, Task<LoadoutToBuyOutcome>> Plot);

/// <summary>What a plot said, and the result page to open when it stored a new plan.</summary>
public sealed record LoadoutToBuyOutcome(string Said, bool IsError, D47.Core.Interface.NavCrumb? Open);
