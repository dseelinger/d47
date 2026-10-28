using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>One thing an outfitting search asks for: a module, with an optional size and rating, or a ship.</summary>
public sealed record OutfittingItem
{
    private static readonly Lazy<Tables> Loaded = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["Vessel Hangar"] = "Fighter Hangar",
    };

    private sealed record Tables(IReadOnlyList<OutfittingItem> All, IReadOnlyList<string> Unmapped);

    private OutfittingItem()
    {
    }

    /// <summary>The catalogue name of the module or ship.</summary>
    public required string Name { get; init; }

    public bool IsShip { get; init; }

    /// <summary>Module size, 0 to 8.</summary>
    public int? Class { get; init; }

    /// <summary>Module rating, A to I.</summary>
    public string? Rating { get; init; }

    /// <summary>"5A Frame Shift Drive (SCO)", "Fuel Scoop" or "Python MkII".</summary>
    public string Display =>
        IsShip || (Class is null && Rating is null)
            ? Name
            : $"{Class?.ToString(CultureInfo.InvariantCulture)}{Rating} {Name}";

    public override string ToString() => Display;

    /// <summary>Every ship, every catalogue module bare, and each size and rating the specification table lists.</summary>
    public static IReadOnlyList<OutfittingItem> All => Loaded.Value.All;

    /// <summary>Specification-table module names that map to no catalogue name and carry a size or rating.</summary>
    public static IReadOnlyList<string> Unmapped => Loaded.Value.Unmapped;

    /// <summary>Reads "5A Frame Shift Drive (SCO)", "fuel scoop" or "Python MkII"; otherwise says why not.</summary>
    public static bool TryParse(string? text, out OutfittingItem item, out string failure)
    {
        item = null!;
        failure = string.Empty;

        var spoken = text?.Trim() ?? string.Empty;

        if (spoken.Length == 0)
        {
            failure = "An item has no name.";
            return false;
        }

        var (size, rating, remainder) = SplitPrefix(spoken, out var prefixed);

        if (prefixed)
        {
            if (size is > 8)
            {
                failure = $"'{spoken}' has a module size of {size}. They run 0 to 8.";
                return false;
            }

            if (rating is { Length: > 0 } && !OutfittingCatalogue.Ratings.Contains(rating))
            {
                failure = $"'{spoken}' has a module rating of {rating}. They run A to I.";
                return false;
            }
        }

        var module = OutfittingCatalogue.MatchModule(remainder);

        if (module is not null && (prefixed || OutfittingCatalogue.MatchShip(remainder) is null))
        {
            item = new OutfittingItem { Name = module, Class = size, Rating = rating };
            return true;
        }

        if (!prefixed && OutfittingCatalogue.MatchShip(remainder) is { } ship)
        {
            item = new OutfittingItem { Name = ship, IsShip = true };
            return true;
        }

        var near = OutfittingCatalogue.Near(
            [.. OutfittingCatalogue.Ships, .. OutfittingCatalogue.Modules], remainder);

        failure = near.Count > 0
            ? $"'{spoken}' is not a module or ship I know. Did you mean {string.Join(", ", near)}?"
            : $"'{spoken}' is not a module or ship I know.";

        return false;
    }

    /// <summary>Lowest and highest list price across the specifications this module matches; null for a ship or when none has a cost.</summary>
    public (long Low, long High)? PriceRange()
    {
        if (IsShip)
        {
            return null;
        }

        var catalogue = Catalogue.Relax(Name);
        long? low = null;
        long? high = null;

        foreach (var spec in EliteSpecifications.Modules)
        {
            if (spec.Cost is not { } cost
                || spec.Name.EndsWith("(free)", StringComparison.Ordinal)
                || (Class is not null && spec.Class != Class)
                || (Rating is not null && !string.Equals(spec.Rating, Rating, StringComparison.Ordinal))
                || Catalogue.Relax(Aliases.TryGetValue(spec.Name, out var alias) ? alias : spec.Name) != catalogue)
            {
                continue;
            }

            low = low is { } l ? Math.Min(l, cost) : cost;
            high = high is { } h ? Math.Max(h, cost) : cost;
        }

        return low is { } lo && high is { } hi ? (lo, hi) : null;
    }

    /// <summary>"250,000 CR" or "35,400–132,800 CR"; empty when there is no price.</summary>
    public string PriceText()
    {
        if (PriceRange() is not { } range)
        {
            return string.Empty;
        }

        var low = range.Low.ToString("N0", CultureInfo.InvariantCulture);

        return range.Low == range.High
            ? $"{low} CR"
            : $"{low}–{range.High.ToString("N0", CultureInfo.InvariantCulture)} CR";
    }

    /// <summary>Items whose display form, or short form, starts each typed word with a word of it; shortest first.</summary>
    public static IReadOnlyList<OutfittingItem> Suggest(string? typed, int limit = 12)
    {
        var words = Words(typed);

        if (words.Length == 0)
        {
            return [];
        }

        return
        [
            .. All
                .Where(item => words.All(word => item.Vocabulary().Any(known =>
                    known.StartsWith(word, StringComparison.OrdinalIgnoreCase))))
                .OrderBy(item => item.Display.Length)
                .ThenBy(item => item.Display, StringComparer.OrdinalIgnoreCase)
                .Take(limit),
        ];
    }

    private IEnumerable<string> Vocabulary() =>
        Words(Display).Concat(Words(IsShip ? Name : ShortNames.Of(Name))).Distinct(StringComparer.OrdinalIgnoreCase);

    private static string[] Words(string? text) =>
        (text ?? string.Empty).Replace('(', ' ').Replace(')', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A leading "5A", "5" or "A" word, then the name.</summary>
    private static (int? Size, string? Rating, string Remainder) SplitPrefix(string spoken, out bool prefixed)
    {
        prefixed = false;

        var space = spoken.IndexOf(' ', StringComparison.Ordinal);

        if (space <= 0)
        {
            return (null, null, spoken);
        }

        var head = spoken[..space];
        var rest = spoken[(space + 1)..].Trim();

        if (char.IsDigit(head[0]) && head.Length <= 2)
        {
            prefixed = true;

            return (head[0] - '0', head.Length == 2 ? head[1].ToString().ToUpperInvariant() : null, rest);
        }

        if (head.Length == 1 && char.IsLetter(head[0])
            && OutfittingCatalogue.MatchModule(rest) is not null)
        {
            prefixed = true;

            return (null, head.ToUpperInvariant(), rest);
        }

        return (null, null, spoken);
    }

    private static Tables Build()
    {
        var items = new List<OutfittingItem>();

        items.AddRange(OutfittingCatalogue.Ships.Select(ship => new OutfittingItem { Name = ship, IsShip = true }));

        foreach (var module in OutfittingCatalogue.Modules)
        {
            items.Add(new OutfittingItem { Name = module });
        }

        var catalogue = OutfittingCatalogue.Modules.ToDictionary(Catalogue.Relax, name => name);
        var unmapped = new SortedSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spec in EliteSpecifications.Modules.OrderBy(spec => spec.Class).ThenBy(spec => spec.Rating, StringComparer.Ordinal))
        {
            if ((spec.Class is null && spec.Rating is null) || spec.Name.EndsWith("(free)", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Aliases.TryGetValue(spec.Name, out var alias) ? alias : spec.Name;

            if (!catalogue.TryGetValue(Catalogue.Relax(name), out var mapped))
            {
                unmapped.Add(spec.Name);
                continue;
            }

            var item = new OutfittingItem { Name = mapped, Class = spec.Class, Rating = spec.Rating };

            if (seen.Add(item.Display))
            {
                items.Add(item);
            }
        }

        return new Tables(items, [.. unmapped]);
    }
}
