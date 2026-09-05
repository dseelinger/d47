using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;

namespace D47.Core.Knowledge;

/// <summary>
/// The Community Goal supply search, saved once and run again and again
/// (<a href="https://github.com/dseelinger/d47/issues/296">#296</a>).
/// <para>
/// <b>The INARA query the Commander was typing by hand every time</b>: buy Palladium, near
/// wherever the ship is, nearest first, within 250 ly, prices under eight hours old, a large pad,
/// a station within 50,000 Ls of the star, at least 10,000 in stock, no surface stations, no
/// carriers. Every one of those is a knob on <c>find_nearest_station</c> now, and this is the one
/// place they are set together — so "community goal search" runs the same question every time
/// rather than a fresh sentence for the model to reinterpret.
/// </para>
/// <para>
/// <b>Not a tool.</b> The phrases are <see cref="DynamicCommand"/>s, matched whole and first by
/// the router and pointed at the galaxy search with the arguments baked, which costs no
/// tool-surface bytes and never reaches a model. Dynamic rather than declared on the tool, because
/// the commodity is editable and the argument is not knowable when the descriptor is registered —
/// the same reason the macro names and the carrier course are dynamic.
/// </para>
/// <para>
/// <b>Only the commodity moves.</b> The rest is the goal's shape, and the issue that built this
/// says nothing here is meant to outlive the full INARA-equivalent search that replaces it.
/// </para>
/// </summary>
public sealed class CommunityGoalSearch
{
    public const string DefaultCommodity = "Palladium";

    /// <summary>Light years out from the Commander's system.</summary>
    public const double MaxDistance = 250;

    /// <summary>How stale a quote may be. Eight hours, because supply is what moves fastest.</summary>
    public const int MaxPriceAgeHours = 8;

    /// <summary>Light seconds from the star.</summary>
    public const double MaxStationDistance = 50_000;

    /// <summary>The least a station must hold to be worth the trip.</summary>
    public const int MinSupply = 10_000;

    /// <summary>
    /// How many the page gets. The ear gets one — the nearest — and the rest are counted; see
    /// the distance ordering in the galaxy capability.
    /// </summary>
    public const int Limit = 10;

    /// <summary>
    /// What the Commander says to run it. Whole utterances, so none can swallow a longer sentence.
    /// </summary>
    public static readonly IReadOnlyList<string> Spellings =
    [
        "community goal search",
        "cg search",
        "c g search",
        "run the community goal search",
        "run the cg search",
    ];

    /// <summary>
    /// What reruns it while the page is up. The first refresh command anywhere in d47, and kept
    /// to the moment the page is showing so the bare word cannot be claimed by this forever.
    /// </summary>
    public static readonly IReadOnlyList<string> RefreshSpellings =
    [
        "refresh",
        "refresh that",
        "refresh the search",
    ];

    private readonly Lock _gate = new();

    private string _commodity = DefaultCommodity;

    /// <summary>The commodity, as the market spells it. Blank falls back to the default.</summary>
    public string Commodity
    {
        get
        {
            lock (_gate)
            {
                return _commodity;
            }
        }

        set
        {
            lock (_gate)
            {
                _commodity = string.IsNullOrWhiteSpace(value) ? DefaultCommodity : value.Trim();
            }
        }
    }

    /// <summary>
    /// Whether the Community Goal page is on screen right now, which is when "refresh" means
    /// this search. Set by the window that draws the page; false with no window.
    /// </summary>
    public Func<bool> Showing { get; set; } = () => false;

    /// <summary>The saved question, as the galaxy search takes it. Read fresh: the commodity moves.</summary>
    public ToolArguments Arguments() => new(Values());

    /// <summary>
    /// Whether a journal's commodity spelling names this one. The journal writes the symbol
    /// (<c>palladium</c>) and, where the two differ, a localised name beside it
    /// (<c>Low Temperature Diamonds</c> for <c>lowtemperaturediamonds</c>); the market's own
    /// spelling matches either once case and spaces are set aside.
    /// </summary>
    public bool IsCommodity(string? named)
    {
        if (string.IsNullOrWhiteSpace(named))
        {
            return false;
        }

        var mine = Commodity.Replace(" ", string.Empty);
        var theirs = named.Replace(" ", string.Empty);

        return string.Equals(mine, theirs, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The router's vocabulary for this search: the run phrases always, the refresh phrases only
    /// while the page is showing. Enumerated at match time, so both the commodity and the page
    /// are read as they are now.
    /// </summary>
    public IEnumerable<DynamicCommand> Phrases()
    {
        var values = Values();

        foreach (var phrase in Spellings)
        {
            yield return new DynamicCommand(phrase, GalaxyCapability.Id, MaterialSeam.MarketTool, values);
        }

        if (!Showing())
        {
            yield break;
        }

        foreach (var phrase in RefreshSpellings)
        {
            yield return new DynamicCommand(phrase, GalaxyCapability.Id, MaterialSeam.MarketTool, values);
        }
    }

    private Dictionary<string, string> Values() =>
        new(StringComparer.Ordinal)
        {
            ["commodity"] = Commodity,
            ["max_distance"] = MaxDistance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["max_price_age_hours"] = MaxPriceAgeHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["large_pad"] = "true",
            ["max_station_distance"] = MaxStationDistance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["min_supply"] = MinSupply.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["order_by"] = "distance",
            ["limit"] = Limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
}
