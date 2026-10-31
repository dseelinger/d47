using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Capabilities.Builtin;

namespace D47.Core.Knowledge;

/// <summary>What <see cref="NamedLookupPhrase.Read"/> made of an utterance: the capability, the tool and its arguments.</summary>
public sealed record NamedLookup(string CapabilityId, string Tool, IReadOnlyDictionary<string, string> Arguments);

/// <summary>
/// The model-free grammar for a hull, a module or a blueprint asked about by name: "what is the Python like",
/// "what does a 6A power plant weigh", "what does a grade 5 dirty drive cost". A name the shipped tables do not
/// resolve to exactly one entry is not read, so the sentence goes on to the model.
/// </summary>
public static partial class NamedLookupPhrase
{
    /// <summary>The lookup the utterance asks for, or null when it is not one this grammar takes.</summary>
    public static NamedLookup? Read(string? input)
    {
        var said = (input ?? string.Empty).Replace('’', '\'').Trim().TrimEnd('.', '!', '?').Trim();

        if (Module().Match(said) is { Success: true } module)
        {
            var name = module.Groups["module"].Value.Trim();
            var size = int.Parse(module.Groups["size"].Value, CultureInfo.InvariantCulture);
            var rating = module.Groups["rating"].Value.ToUpperInvariant();

            return EliteSpecifications.ModulesNamed(name, size, rating).Count > 0
                ? new(SpecificationCapability.Id, "get_module_specification", Arguments(
                    ("module", name),
                    ("size", size.ToString(CultureInfo.InvariantCulture)),
                    ("rating", rating)))
                : null;
        }

        if (Ship().Match(said) is { Success: true } ship
            && ship.Groups["ship"].Value.Trim() is { Length: >= 3 } hull
            && EliteSpecifications.Ship(hull) is not null)
        {
            return new(SpecificationCapability.Id, "get_ship_specification", Arguments(("ship", hull)));
        }

        if (Blueprint().Match(said) is { Success: true } blueprint
            && blueprint.Groups["blueprint"].Value.Trim() is { Length: >= 3 } recipe
            && BlueprintCatalogue.Named(recipe).Count > 0)
        {
            return new(EngineeringCapability.Id, "get_blueprint", Arguments(("blueprint", recipe)));
        }

        return null;
    }

    private static Dictionary<string, string> Arguments(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    [GeneratedRegex(
        @"^(?:what|how much) does (?:a |an |the )?(?<size>[1-8]) ?(?<rating>[a-i]) (?<module>.+?) (?:weigh|draw|cost)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Module();

    [GeneratedRegex(
        @"^what(?:'s| is) (?:the |a |an )?(?<ship>.+?) like$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Ship();

    [GeneratedRegex(
        @"^what does (?:a |an |the )?(?:grade (?:[1-5]|one|two|three|four|five) )?(?<blueprint>.+?) cost$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Blueprint();
}
