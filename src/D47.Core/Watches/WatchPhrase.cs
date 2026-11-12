using System.Text.RegularExpressions;
using D47.Core.Knowledge;

namespace D47.Core.Watches;

/// <summary>What <see cref="WatchPhrase.Read"/> made of an utterance.</summary>
public abstract record WatchReading
{
    private WatchReading()
    {
    }

    /// <summary>A next-system watch to add.</summary>
    public sealed record Add(string Service, string? Kind, PadSize? Pad) : WatchReading;

    /// <summary>A next-system watch to delete.</summary>
    public sealed record Delete(string Service, string? Kind) : WatchReading;

    /// <summary>A watch request whose service is not one a station has; <paramref name="Said"/> is the words as spoken.</summary>
    public sealed record Declined(string Said) : WatchReading;
}

/// <summary>The model-free grammar for next-system watches: "tell me the next time we're in a system with &lt;service&gt;" and its inverse.</summary>
public static partial class WatchPhrase
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private const string KindWords = "raw|manufactured|encoded|human|guardian";

    private static readonly (string Service, Regex Regex)[] Services =
    [
        (NextSystemWatch.MaterialTrader, new Regex(
            $@"^(?:(?<kind>raw|manufactured|encoded) )?(?:materials?|mats) traders?(?: (?:for|of|in|that (?:buys|sells|trades)) (?<after>raw|manufactured|encoded)(?: materials?)?)?$",
            Options)),
        (NextSystemWatch.TechnologyBroker, new Regex(
            $@"^(?:(?<kind>human|guardian) )?(?:technology|tech) brokers?(?: (?:for|of) (?<after>human|guardian)(?: technology)?)?$",
            Options)),
        ("Black Market", new Regex(@"^black ?markets?$", Options)),
        ("Interstellar Factors", new Regex(@"^interstellar factors(?: contact)?$", Options)),
        ("Outfitting", new Regex(@"^outfitting(?: station)?$", Options)),
        ("Refuel", new Regex(@"^refuel(?:l?ing)?(?: station)?$", Options)),
        ("Repair", new Regex(@"^repairs?(?: station)?$", Options)),
        ("Shipyard", new Regex(@"^shipyards?$", Options)),
        ("Universal Cartographics", new Regex(@"^(?:universal )?cartographics$", Options)),
        ("Vista Genomics", new Regex(@"^vista genomics$", Options)),
    ];

    /// <summary>The utterance read as a next-system watch request, or null when it is not one this grammar takes.</summary>
    public static WatchReading? Read(string? input)
    {
        var said = Normalise(input);

        if (said.Length == 0)
        {
            return null;
        }

        if (AddPattern().Match(said) is { Success: true } add)
        {
            return Reading(add.Groups["what"].Value, adding: true);
        }

        return StopPattern().Match(said) is { Success: true } stop
            ? Reading(stop.Groups["what"].Value, adding: false)
            : null;
    }

    private static WatchReading Reading(string what, bool adding)
    {
        PadSize? pad = null;
        var words = what.Trim().Trim(',', ' ');

        if (PadPattern().Match(words) is { Success: true } padded)
        {
            pad = padded.Groups["size"].Value.Equals("large", StringComparison.OrdinalIgnoreCase) ? PadSize.Large : PadSize.Medium;
            words = words[..padded.Index].Trim().Trim(',', ' ');
        }

        var name = Article().Replace(words, string.Empty).Trim();

        foreach (var (service, regex) in Services)
        {
            if (regex.Match(name) is not { Success: true } match)
            {
                continue;
            }

            var kind = match.Groups["kind"] is { Success: true } before ? before.Value : match.Groups["after"] is { Success: true } after ? after.Value : null;
            var spelled = NextSystemWatch.KindFor(service, kind);

            return adding ? new WatchReading.Add(service, spelled, pad) : new WatchReading.Delete(service, spelled);
        }

        return new WatchReading.Declined(words);
    }

    /// <summary>One line, typographic apostrophes straightened, no leading courtesy and no closing punctuation.</summary>
    private static string Normalise(string? input)
    {
        var said = Whitespace().Replace((input ?? string.Empty).Replace('’', '\''), " ").Trim();
        said = said.TrimEnd('.', '!', '?', ' ');

        return Courtesy().Replace(said, string.Empty).Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(?:please,? |hey,? |ok,? |okay,? )|,? please$", Options)]
    private static partial Regex Courtesy();

    [GeneratedRegex(@"^(?:a|an|the|any|one) ", Options)]
    private static partial Regex Article();

    [GeneratedRegex(@"(?:,? (?:and )?(?:with|that has|which has|having) (?:a |an )?|,? (?:and )?on )(?<size>large|medium)(?: landing)? pad$", Options)]
    private static partial Regex PadPattern();

    [GeneratedRegex(
        @"^(?:tell me|let me know|notify me|warn me|alert me)(?: (?:when|the next time|next time)) (?:we're|we are|i'm|i am|we get to|i get to|we arrive in|i arrive in|we jump to|i jump to) (?:in )?a system (?:with|that has|which has|having) (?<what>.+)$",
        Options)]
    private static partial Regex AddPattern();

    [GeneratedRegex(
        @"^(?:stop (?:watching|looking) for|delete the watch for|cancel the watch for|remove the watch for|forget the watch for) (?<what>.+)$",
        Options)]
    private static partial Regex StopPattern();
}
