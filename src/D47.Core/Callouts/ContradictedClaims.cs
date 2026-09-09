using System.Text.RegularExpressions;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// What the ship can prove about itself right now, as a snapshot a flavour line can be checked against
/// (#338).
/// </summary>
public sealed record ShipFacts
{
    /// <summary>Nothing known, which contradicts nothing.</summary>
    public static readonly ShipFacts Unknown = new();

    /// <summary>Whether the ship's own manifest has been read.</summary>
    public bool HoldKnown { get; init; }

    /// <summary>
    /// Elite's own total tonnage in the ship's hold, meaningful only with <see cref="HoldKnown"/>.
    /// </summary>
    public int HoldTonnes { get; init; }

    /// <summary>What the hold could take, from the loadout, or null where no loadout can say.</summary>
    public int? HoldCapacity { get; init; }

    /// <summary>What the Commander is doing, as far as the journal has said.</summary>
    public FlightMode Mode { get; init; } = FlightMode.Unknown;

    /// <summary>Whether the journal says they are docked.</summary>
    public bool Docked { get; init; }

    /// <summary>Whether a Fuel Scoop is fitted, or null where no loadout can answer.</summary>
    public bool? ScoopFitted { get; init; }

    /// <summary>Whether any limpet controller is fitted, or null where no loadout can answer.</summary>
    public bool? LimpetControllerFitted { get; init; }

    /// <summary>The snapshot for one Commander, or <see cref="Unknown"/> for none.</summary>
    public static ShipFacts Of(CommanderGameState? state)
    {
        if (state is null)
        {
            return Unknown;
        }

        var hold = state.Hold;
        var known = hold is { IsKnown: true, IsShip: true };
        var ship = state.FlownShip;

        return new ShipFacts
        {
            HoldKnown = known,
            HoldTonnes = known ? hold.Count : 0,
            HoldCapacity = ship.CargoCapacity,
            Mode = state.Location.Mode,
            Docked = state.Location.Docked,
            ScoopFitted = ship.Fitted(ShipLoadout.FuelScoop),
            LimpetControllerFitted = LimpetController(ship),
        };
    }

    /// <summary>Whether any limpet controller is fitted.</summary>
    private static bool? LimpetController(ShipLoadout ship) =>
        ship.Fitted("int_dronecontrol_") is { } single
            ? single || ship.Fitted("int_multidronecontrol_") is true
            : null;
}

/// <summary>One claim a line made that the ship's own state disproves.</summary>
/// <param name="Claim">Which claim it is, for the log and for a test to name.</param>
/// <param name="Matched">
/// The words in the line that matched, so the vocabulary can be grown from what got caught.
/// </param>
/// <param name="State">The state value that settles it, in words — "the hold holds 0 t".</param>
/// <param name="Correction">
/// The sentence handed back to the model on the one retry — "the hold is empty; do not mention cargo".
/// </param>
public sealed record Contradiction(string Claim, string Matched, string State, string Correction);

/// <summary>A check between composition and speech, on the flavour paths only (#338).</summary>
public static class ContradictedClaims
{
    /// <summary>
    /// One verifiable claim: what to call it, the words that assert it, when the state disproves it,
    /// and what to say about the state.
    /// </summary>
    private sealed record Claim(
        string Name,
        string[] Words,
        Func<ShipFacts, bool> Disproved,
        Func<ShipFacts, string> State,
        Func<ShipFacts, string> Correction);

    /// <summary>Whether the ship is definitely not sitting on a pad.</summary>
    private static bool Flying(ShipFacts facts) =>
        !facts.Docked
        && facts.Mode is FlightMode.Normal or FlightMode.Supercruise or FlightMode.Hyperspace or FlightMode.Landed;

    /// <summary>The vocabulary, and the state each entry is checked against.</summary>
    private static readonly Claim[] Claims =
    [
        // **The incident's own shape.** A remark about the cargo, while Elite's manifest says the ship is
        // carrying nothing.
        new Claim(
            "cargo aboard",
            [
                "hold is full", "hold's full", "holds are full", "full hold", "hold full of",
                "fully loaded", "cargo hold is full", "tonnes of", "tons of", "our cargo",
                "the cargo in the hold", "cargo aboard", "cargo in the hold", "carrying cargo",
                "the cargo we", "cargo we're carrying", "cargo we are carrying", "a full load",
                "loaded to the", "packed to the",
            ],
            facts => facts.HoldKnown && facts.HoldTonnes == 0,
            _ => "the hold holds nothing",
            _ => "The cargo hold is empty; do not mention cargo, a load or a tonnage."),

        new Claim(
            "hold empty",
            [
                "hold is empty", "hold's empty", "empty hold", "nothing in the hold",
                "hold is bare", "no cargo", "nothing aboard", "running empty", "flying empty",
                "empty holds", "empty cargo hold", "empty cargo holds",
            ],
            facts => facts.HoldKnown && facts.HoldTonnes > 0,
            facts => $"the hold holds {facts.HoldTonnes} t",
            facts => $"The cargo hold is not empty — there are {facts.HoldTonnes} tonnes aboard; "
                     + "do not say it is empty."),

        // The partial case the claim above cannot reach: a hold with real room in it, called full.
        new Claim(
            "hold full",
            [
                "hold is full", "hold's full", "holds are full", "full hold", "fully loaded",
                "at capacity", "no room left", "loaded to the", "packed to the",
            ],
            facts => facts.HoldKnown
                     && facts.HoldTonnes > 0
                     && facts.HoldCapacity is { } capacity
                     && capacity > 0
                     && facts.HoldTonnes < capacity,
            facts => $"the hold holds {facts.HoldTonnes} of {facts.HoldCapacity} t",
            facts => $"The cargo hold is not full — {facts.HoldTonnes} tonnes of "
                     + $"{facts.HoldCapacity} are aboard; do not say it is full."),

        new Claim(
            "docked",
            [
                "on the pad", "we're docked", "we are docked", "you're docked", "you are docked",
                "docked at", "while docked", "in the docking bay", "sitting on the pad",
            ],
            Flying,
            facts => $"the ship is not docked ({facts.Mode})",
            _ => "The ship is not docked; do not say or imply that it is."),

        // Only where the ship is definitely not on any surface.
        new Claim(
            "landed",
            ["touched down", "we're landed", "we are landed", "on the surface", "on the ground", "set down on"],
            facts => facts.Mode is FlightMode.Supercruise or FlightMode.Hyperspace,
            facts => $"the ship is not landed ({facts.Mode})",
            _ => "The ship is not landed; do not say or imply that it is on a surface."),

        // Only where the ship cannot be in supercruise at all.
        new Claim(
            "supercruise",
            ["in supercruise", "supercruising", "in cruise"],
            facts => facts.Mode is FlightMode.Docked or FlightMode.Landed,
            facts => $"the ship is not in supercruise ({facts.Mode})",
            _ => "The ship is not in supercruise; do not say or imply that it is."),

        // The two fitted-module claims from #323 and #324, where the loadout answers and only where it
        // answers — null is "no evidence", exactly as ShipLoadout.Fitted means it.
        new Claim(
            "scoop fitted",
            ["fuel scoop", "scooping", "scoop the", "scoop off", "scoop up"],
            facts => facts.ScoopFitted is false,
            _ => "no Fuel Scoop is fitted",
            _ => "This ship has no Fuel Scoop fitted; do not mention scooping or a scoop."),

        new Claim(
            "limpet controller fitted",
            ["limpet", "drone controller"],
            facts => facts.LimpetControllerFitted is false,
            _ => "no limpet controller is fitted",
            _ => "This ship has no limpet controller fitted; do not mention limpets."),
    ];

    private static readonly Claim CargoAboardClaim = Claims.First(claim => claim.Name == "cargo aboard");
    private static readonly Claim HoldEmptyClaim = Claims.First(claim => claim.Name == "hold empty");

    /// <summary>
    /// Words that mean a stated tonnage names free capacity rather than cargo aboard — the object of
    /// "tonnes of"/"tons of" that keeps "1200 tonnes of empty cargo hold" from reading as a claim of
    /// cargo (#369).
    /// </summary>
    private static readonly string[] EmptinessWords = ["empty", "nothing", "space", "room", "capacity"];

    private static readonly string[] TonnagePhrases = ["tonnes of", "tons of"];

    /// <summary>Space and punctuation between two words, skipped when reading the next one.</summary>
    private static readonly char[] BetweenWords = [' ', ',', '.', ';', ':'];

    /// <summary>
    /// A stated tonnage the ship's hold cannot take, against <see cref="ShipFacts.HoldCapacity"/> — the
    /// number in front of "tonnes of"/"tons of".
    /// </summary>
    private static readonly Regex StatedTonnage = new(
        @"(?<n>\d[\d,]*)\s*(?:tonnes|tons)\s+of",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// The first claim in <paramref name="line"/> that <paramref name="facts"/> disproves, or null
    /// where the line asserts nothing the ship can settle — which includes every case where the state
    /// is unknown.
    /// </summary>
    public static Contradiction? Find(string? line, ShipFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (CapacityContradiction(line, facts) is { } tooMuch)
        {
            return tooMuch;
        }

        // A line whose vocabulary matches both "cargo aboard" and "hold empty" — "our cargo is gone, empty
        // hold ahead of us" — is judged by which one the hold's actual state agrees with, not by whichever
        // claim this loop happens to reach first.
        var cargoAboardMatch = MatchCargoAboard(line);
        var holdEmptyMatch = HoldEmptyClaim.Words.FirstOrDefault(
            word => line.Contains(word, StringComparison.OrdinalIgnoreCase));

        var bothVocabularies = cargoAboardMatch is not null && holdEmptyMatch is not null && facts.HoldKnown;

        if (bothVocabularies && facts.HoldTonnes > 0)
        {
            return new Contradiction(
                HoldEmptyClaim.Name, holdEmptyMatch!, HoldEmptyClaim.State(facts), HoldEmptyClaim.Correction(facts));
        }

        foreach (var claim in Claims)
        {
            // With an empty hold and both vocabularies present, the two cargo claims are settled between
            // themselves and neither is a contradiction.
            if (bothVocabularies && (claim.Name == CargoAboardClaim.Name || claim.Name == HoldEmptyClaim.Name))
            {
                continue;
            }

            if (!claim.Disproved(facts))
            {
                continue;
            }

            var matched = claim.Name == CargoAboardClaim.Name
                ? cargoAboardMatch
                : claim.Words.FirstOrDefault(word => line.Contains(word, StringComparison.OrdinalIgnoreCase));

            if (matched is null)
            {
                continue;
            }

            return new Contradiction(claim.Name, matched, claim.State(facts), claim.Correction(facts));
        }

        return null;
    }

    /// <summary>
    /// The word in <see cref="CargoAboardClaim"/>'s vocabulary the line actually asserts, or null.
    /// </summary>
    private static string? MatchCargoAboard(string line)
    {
        foreach (var word in CargoAboardClaim.Words)
        {
            if (!line.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TonnagePhrases.Contains(word) && NamesFreeCapacity(line, word))
            {
                continue;
            }

            return word;
        }

        return null;
    }

    /// <summary>
    /// Whether the word after <paramref name="phrase"/> in <paramref name="line"/> is empty capacity
    /// rather than a cargo.
    /// </summary>
    private static bool NamesFreeCapacity(string line, string phrase)
    {
        var at = line.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        var rest = line[(at + phrase.Length)..].TrimStart(BetweenWords);
        var word = LeadingWord(rest);

        if (word.Equals("cargo", StringComparison.OrdinalIgnoreCase))
        {
            rest = rest[word.Length..].TrimStart(BetweenWords);
            word = LeadingWord(rest);
        }

        return EmptinessWords.Contains(word, StringComparer.OrdinalIgnoreCase);
    }

    private static string LeadingWord(string text)
    {
        var end = 0;

        while (end < text.Length && (char.IsLetter(text[end]) || text[end] == '\''))
        {
            end++;
        }

        return text[..end];
    }

    /// <summary>
    /// A contradiction of a different kind to every other claim: not a claim's vocabulary against the
    /// ship's state, but a number the line states against <see cref="ShipFacts.HoldCapacity"/>.
    /// </summary>
    private static Contradiction? CapacityContradiction(string line, ShipFacts facts)
    {
        if (facts.HoldCapacity is not { } capacity || capacity <= 0)
        {
            return null;
        }

        if (StatedTonnage.Match(line) is not { Success: true } match
            || !int.TryParse(match.Groups["n"].Value.Replace(",", string.Empty), out var stated)
            || stated <= capacity)
        {
            return null;
        }

        return new Contradiction(
            "hold capacity",
            match.Value,
            $"the hold's capacity is {capacity} t",
            $"This ship's cargo hold holds {capacity} tonnes at most; do not repeat that figure.");
    }

    /// <summary>
    /// Whether a line of an overheard exchange is about the Commander's ship at all — whether the
    /// claims below are claims about them.
    /// </summary>
    public static bool AboutTheCommandersShip(string? line) =>
        line is { Length: > 0 } && Addressed.Any(word => IsWordIn(word, line));

    /// <summary>
    /// The words that make a line of an exchange the Commander's business. "you" covers "you're" and
    /// "you'll" on its own, because an apostrophe ends a word.
    /// </summary>
    private static readonly string[] Addressed = ["you", "your", "yours", "commander", "cmdr"];

    /// <summary>
    /// Whole-word containment, so "your" is not found inside "yours" and "you" is not found inside
    /// "young".
    /// </summary>
    private static bool IsWordIn(string word, string line)
    {
        for (var at = 0; (at = line.IndexOf(word, at, StringComparison.OrdinalIgnoreCase)) >= 0; at++)
        {
            var end = at + word.Length;

            if ((at == 0 || !char.IsLetter(line[at - 1]))
                && (end == line.Length || !char.IsLetter(line[end])))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The line to speak, or null for silence, with no model in the path to ask again — the authored
    /// fallback at a flavour call site.
    /// </summary>
    /// <param name="what">The callout key or path, so the log says which line this was.</param>
    public static string? Sayable(string? line, ShipFacts facts, ILogger? logger, string what)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (Find(line, facts) is not { } contradiction)
        {
            return line;
        }

        Dropped(logger, what, "the authored line", line, contradiction);
        return null;
    }

    /// <summary>The line to speak, or null for silence, with exactly one retry (#338).</summary>
    /// <param name="again">Asks the model once more, given the contradiction.</param>
    public static async Task<string?> SayableAsync(
        string? line,
        ShipFacts facts,
        Func<Contradiction, Task<string?>> again,
        ILogger? logger,
        string what)
    {
        ArgumentNullException.ThrowIfNull(again);

        if (!FlavourBriefs.MayBeSpoken(line))
        {
            return null;
        }

        if (Find(line, facts) is not { } contradiction)
        {
            return line;
        }

        Dropped(logger, what, "the composed line", line!, contradiction);

        var retry = await again(contradiction).ConfigureAwait(false);

        if (!FlavourBriefs.MayBeSpoken(retry))
        {
            return null;
        }

        if (Find(retry, facts) is not { } still)
        {
            return retry;
        }

        Dropped(logger, what, "the retry", retry!, still);
        return null;
    }

    /// <summary>
    /// Every catch, in one shape, at warning level: the line, the claim, the words that matched and the
    /// state that settled it.
    /// </summary>
    private static void Dropped(
        ILogger? logger,
        string what,
        string stage,
        string line,
        Contradiction contradiction) =>
        logger?.LogWarning(
            "A flavour line contradicted the ship and was dropped: {What}, {Stage} \"{Line}\" "
            + "claims {Claim} on \"{Matched}\", but {State}.",
            what,
            stage,
            line,
            contradiction.Claim,
            contradiction.Matched,
            contradiction.State);

    /// <summary>
    /// The one-line question that replaces a single line of an invented exchange, where re-asking the
    /// exchange's own instruction would compose a whole new scene and cost the lines around it.
    /// </summary>
    public static string Rewrite(string line, Contradiction contradiction)
    {
        ArgumentNullException.ThrowIfNull(contradiction);

        return $"One line of the exchange was: \"{line}\" It is wrong. {contradiction.Correction} "
               + "Say the same beat again, in the same voice and about the same thing, without that "
               + "claim. One line only, no speaker name, nothing else.";
    }
}
