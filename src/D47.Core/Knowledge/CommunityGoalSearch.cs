using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>The Community Goal supply search, saved once and run again and again (#296).</summary>
public sealed class CommunityGoalSearch
{
    public const string DefaultCommodity = "Palladium";

    /// <summary>Light years out from the Commander's system.</summary>
    public const double MaxDistance = 250;

    /// <summary>How stale a quote may be.</summary>
    public const int MaxPriceAgeHours = 8;

    /// <summary>Light seconds from the star.</summary>
    public const double MaxStationDistance = 50_000;

    /// <summary>The least a station must hold to be worth the trip.</summary>
    public const int MinSupply = 10_000;

    /// <summary>How many the page gets.</summary>
    public const int Limit = 10;

    /// <summary>What the Commander says to run it.</summary>
    public static readonly IReadOnlyList<string> Spellings =
    [
        "community goal search",
        "cg search",
        "c g search",
        "run the community goal search",
        "run the cg search",
    ];

    /// <summary>The same search asked from the ship instead of from the goal (#331).</summary>
    public static readonly IReadOnlyList<string> FromHereSpellings =
    [
        "community goal search from here",
        "cg search from here",
        "c g search from here",
        "run the community goal search from here",
        "run the cg search from here",
    ];

    /// <summary>What reruns it while the page is up.</summary>
    public static readonly IReadOnlyList<string> RefreshSpellings =
    [
        "refresh",
        "refresh that",
        "refresh the search",
    ];

    /// <summary>The refresh phrases that rerun it from the ship instead of from the goal (#331).</summary>
    public static readonly IReadOnlyList<string> RefreshFromHereSpellings =
    [
        "refresh from here",
        "refresh that from here",
        "refresh the search from here",
    ];

    private readonly Lock _gate = new();

    private string _commodity = DefaultCommodity;

    /// <summary>The commodity, as the market spells it.</summary>
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
    /// Whether the Community Goal page is on screen right now, which is when "refresh" means this
    /// search.
    /// </summary>
    public Func<bool> Showing { get; set; } = () => false;

    /// <summary>The Commander's community goal board, for the system to measure from (#331).</summary>
    public Func<CommunityGoalBoard?> Board { get; set; } = () => null;

    /// <summary>The clock the board's expiries are read against.</summary>
    public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// The system the search measures from, or null when nothing on the board is live and the ship has
    /// to answer for it.
    /// </summary>
    public string? GoalSystem => OriginOf(Board(), Now(), Commodity);

    /// <summary>The saved question, as the galaxy search takes it.</summary>
    /// <param name="fromShip">
    /// Ask it from wherever the ship is, rather than from the goal's system.
    /// </param>
    public ToolArguments Arguments(bool fromShip = false) => new(Values(fromShip));

    /// <summary>Which live goal's system the search measures from.</summary>
    public static string? OriginOf(CommunityGoalBoard? board, DateTimeOffset now, string commodity)
    {
        if (board is null)
        {
            return null;
        }

        var goal = board.Live(now)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SystemName))
            .OrderByDescending(candidate => candidate.IsParticipating)
            .ThenByDescending(candidate => Names(candidate.Title, commodity))
            .FirstOrDefault();

        return goal?.SystemName?.Trim();
    }

    /// <summary>Whether a goal's title names the commodity being searched for.</summary>
    private static bool Names(string? title, string commodity) =>
        !string.IsNullOrWhiteSpace(title)
        && !string.IsNullOrWhiteSpace(commodity)
        && title.Replace(" ", string.Empty)
            .Contains(commodity.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a journal's commodity spelling names this one.</summary>
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
    /// The router's vocabulary for this search: the run phrases always, the refresh phrases only while
    /// the page is showing.
    /// </summary>
    public IEnumerable<DynamicCommand> Phrases()
    {
        var fromGoal = Values(fromShip: false);
        var fromShip = Values(fromShip: true);

        foreach (var phrase in Spellings)
        {
            yield return new DynamicCommand(phrase, GalaxyCapability.Id, MaterialSeam.MarketTool, fromGoal);
        }

        foreach (var phrase in FromHereSpellings)
        {
            yield return new DynamicCommand(phrase, GalaxyCapability.Id, MaterialSeam.MarketTool, fromShip);
        }

        if (!Showing())
        {
            yield break;
        }

        foreach (var phrase in RefreshSpellings)
        {
            yield return new DynamicCommand(phrase, GalaxyCapability.Id, MaterialSeam.MarketTool, fromGoal);
        }

        foreach (var phrase in RefreshFromHereSpellings)
        {
            yield return new DynamicCommand(phrase, GalaxyCapability.Id, MaterialSeam.MarketTool, fromShip);
        }
    }

    /// <summary>
    /// What this search tags every posting with, so the page that draws it (#303) can tell its own
    /// answer apart from the Market page's Find or a spoken market question — both post to the same
    /// shared <see cref="CommodityBoard"/>.
    /// </summary>
    public const string Tag = "community-goal";

    /// <summary>The same posting, measured from the ship rather than from the goal (#331).</summary>
    public const string ShipTag = "community-goal-ship";

    /// <summary>Whether a posting's tag is one of this search's own.</summary>
    public static bool Owns(string? tag) =>
        string.Equals(tag, Tag, StringComparison.Ordinal)
        || string.Equals(tag, ShipTag, StringComparison.Ordinal);

    /// <summary>
    /// Whose system a tagged posting was measured from, in the words a Commander reads and hears.
    /// </summary>
    public static string? Whose(string? tag) => tag switch
    {
        Tag => "the goal's system",
        ShipTag => "your ship's system",
        _ => null,
    };

    private Dictionary<string, string> Values(bool fromShip)
    {
        // Null when asked from the ship, and null when there is no live goal to ask from.
        var origin = fromShip ? null : GoalSystem;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = Commodity,
            ["max_distance"] = MaxDistance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["max_price_age_hours"] = MaxPriceAgeHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["large_pad"] = "true",
            ["max_station_distance"] = MaxStationDistance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["min_supply"] = MinSupply.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["order_by"] = "distance",
            ["limit"] = Limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["tag"] = origin is null ? ShipTag : Tag,
        };

        if (origin is not null)
        {
            values["near"] = origin;
        }

        return values;
    }
}
