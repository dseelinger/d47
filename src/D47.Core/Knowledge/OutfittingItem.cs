using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>One thing an outfitting search asks for: a module, with an optional size, rating and mount, or a ship.</summary>
public sealed record OutfittingItem
{
    private static readonly Lazy<Tables> Loaded = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["Vessel Hangar"] = "Fighter Hangar",
    };

    private sealed record Tables(IReadOnlyList<OutfittingItem> All);

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

    /// <summary>Fixed, gimballed or turreted, in <see cref="ModuleSpecification.Mount"/>'s spelling; null for any.</summary>
    public string? Mount { get; init; }

    /// <summary>Whether a station's stock is checked for the mount: one is asked and the table lists more than one for the name, size and rating.</summary>
    public bool MountChecked => !IsShip && IsMountChecked(Name, Class, Rating, Mount);

    /// <summary>The mounts, in <see cref="ModuleSpecification.Mount"/>'s spelling.</summary>
    public static IReadOnlyList<string> MountNames { get; } = ["fixed", "gimballed", "turreted"];

    /// <summary>The kind of slot the module goes in; null for a ship, a type no slot row lists, or specifications that disagree.</summary>
    public ShipSlotKind? SlotKind
    {
        get
        {
            if (IsShip)
            {
                return null;
            }

            ShipSlotKind? found = null;

            foreach (var spec in EliteSpecifications.Modules)
            {
                if (!Matches(spec) || EliteSpecifications.SlotKindOfType(spec.Type) is not { } kind)
                {
                    continue;
                }

                if (found is { } seen && seen != kind)
                {
                    return null;
                }

                found = kind;
            }

            return found;
        }
    }

    /// <summary>"5A Frame Shift Drive (SCO)", "2F Multi-Cannon gimballed", "Fuel Scoop" or "Python Mk II".</summary>
    public string Display =>
        (IsShip || (Class is null && Rating is null)
            ? Name
            : $"{Class?.ToString(CultureInfo.InvariantCulture)}{Rating} {Name}")
        + (Mount is null ? string.Empty : $" {Mount}");

    public override string ToString() => Display;

    /// <summary>
    /// Every ship, every catalogue module bare, each size and rating the specification table lists, and each
    /// mount where it lists more than one at that size and rating.
    /// </summary>
    public static IReadOnlyList<OutfittingItem> All => Loaded.Value.All;

    /// <summary>
    /// Reads "5A Frame Shift Drive (SCO)", "2F Multi-Cannon gimballed", "fuel scoop" or "Python Mk II";
    /// otherwise says why not.
    /// </summary>
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

        var mount = TakeMount(spoken, out var unmounted);
        var (size, rating, remainder) = SplitPrefix(unmounted, out var prefixed);

        if (mount is null)
        {
            mount = TakeMount(remainder, out var rest, last: false);
            remainder = rest;
        }

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

        if (module is not null && (prefixed || mount is not null || OutfittingCatalogue.MatchShip(remainder) is null))
        {
            if (mount is not null && !TryMount(module, size, rating, mount, out failure))
            {
                return false;
            }

            item = new OutfittingItem { Name = module, Class = size, Rating = rating, Mount = mount };
            return true;
        }

        if (!prefixed && mount is null && OutfittingCatalogue.MatchShip(remainder) is { } ship)
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

    /// <summary>Whether a module from the specification table is this item; an unstated size, rating or mount matches any.</summary>
    public bool Matches(ModuleSpecification module) =>
        !IsShip
        && Matches(module, Name, Class, Rating)
        && (Mount is null || string.Equals(Mount, module.Mount, StringComparison.Ordinal));

    /// <summary>The mounts the specification table lists for a module, at the size and rating where they are given.</summary>
    public static IReadOnlyList<string> Mounts(string name, int? size, string? rating)
    {
        if (!Mounted.Value.TryGetValue(Catalogue.Relax(name), out var specs))
        {
            return [];
        }

        return
        [
            .. MountNames.Where(mount => specs.Any(spec =>
                string.Equals(spec.Mount, mount, StringComparison.Ordinal)
                && (size is null || size == spec.Class)
                && (rating is null || string.Equals(rating, spec.Rating, StringComparison.Ordinal)))),
        ];
    }

    /// <summary>The specifications with a mount, by relaxed name.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, ModuleSpecification[]>> Mounted = new(
        () => EliteSpecifications.Modules
            .Where(spec => spec.Mount is not null)
            .GroupBy(spec => Catalogue.Relax(Aliases.TryGetValue(spec.Name, out var alias) ? alias : spec.Name), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Reads "fixed", "gimballed", "gimbal", "turreted" or "turret" as a mount; null for anything else.</summary>
    public static string? ReadMount(string? word) =>
        (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "fixed" => "fixed",
            "gimballed" or "gimbal" => "gimballed",
            "turreted" or "turret" => "turreted",
            _ => null,
        };

    /// <summary>Whether the table lists the mount for the module at the size and rating; otherwise says which it lists.</summary>
    public static bool TryMount(string name, int? size, string? rating, string mount, out string failure)
    {
        failure = string.Empty;

        var listed = Mounts(name, size, rating);

        if (listed.Contains(mount, StringComparer.Ordinal))
        {
            return true;
        }

        var asked = $"{size?.ToString(CultureInfo.InvariantCulture)}{rating} {name}".Trim();

        failure = listed.Count > 0
            ? $"A {asked} is listed {Or(listed)}, not {mount}."
            : Mounts(name, null, null).Count == 0
            ? $"A {name} has no mount, so it cannot be {mount}. Leave the mount out."
            : $"No {asked} has a mount. Leave the mount out, or give another size and rating.";

        return false;
    }

    /// <summary>Whether a station's stock is checked for the mount: one is asked and the table lists more than one for the module at the size and rating.</summary>
    public static bool IsMountChecked(string name, int? size, string? rating, string? mount) =>
        mount is not null && Mounts(name, size, rating).Count > 1;

    private static string Or(IReadOnlyList<string> words) =>
        words.Count == 1 ? words[0] : $"{string.Join(", ", words.Take(words.Count - 1))} or {words[^1]}";

    private static bool Matches(ModuleSpecification module, string name, int? size, string? rating) =>
        Catalogue.Relax(name) == Catalogue.Relax(Aliases.TryGetValue(module.Name, out var alias) ? alias : module.Name)
        && (size is null || size == module.Class)
        && (rating is null || string.Equals(rating, module.Rating, StringComparison.Ordinal));

    /// <summary>Whether this item is the ship with this hull name.</summary>
    public bool Matches(string hullName) => IsShip && Catalogue.Relax(Name) == Catalogue.Relax(hullName);

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
                || (Mount is not null && !string.Equals(spec.Mount, Mount, StringComparison.Ordinal))
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

    /// <summary>A mount word at the end, or at the start, of <paramref name="text"/>, and the text without it; null when there is none.</summary>
    internal static string? TakeMount(string text, out string rest, bool last = true)
    {
        rest = text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length < 2)
        {
            return null;
        }

        if (last && ReadMount(words[^1]) is { } trailing)
        {
            rest = string.Join(' ', words[..^1]);
            return trailing;
        }

        if (ReadMount(words[0]) is { } leading)
        {
            rest = string.Join(' ', words[1..]);
            return leading;
        }

        return null;
    }

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
                continue;
            }

            var item = new OutfittingItem { Name = mapped, Class = spec.Class, Rating = spec.Rating };

            if (seen.Add(item.Display))
            {
                items.Add(item);
            }

            if (IsMountChecked(mapped, spec.Class, spec.Rating, spec.Mount))
            {
                var mounted = item with { Mount = spec.Mount };

                if (seen.Add(mounted.Display))
                {
                    items.Add(mounted);
                }
            }
        }

        return new Tables(items);
    }
}
