using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A scanned planet worth mapping, said on its first scan this session, once per body.</summary>
public sealed class BodyValueCallout : ICallout
{
    private readonly HashSet<(long System, int Body)> _seen = [];

    public string Id => "body-value";

    /// <summary>The least value, in credits, a body is said at.</summary>
    public Func<long> Threshold { get; set; } = () => 500_000;

    /// <summary>Where a planet is priced; null says nothing.</summary>
    public CartographyLedger? Ledger { get; init; }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is not ("Scan" or "SAAScanComplete")
                || journalEvent.Long("SystemAddress") is not { } system
                || journalEvent.Int("BodyID") is not { } body)
            {
                continue;
            }

            // A body mapped before its first scan is left to MappingCallout.
            var first = _seen.Add((system, body));

            if (journalEvent.Kind != "Scan" || !first || context.IsPriming
                || journalEvent.String("PlanetClass") is not { Length: > 0 } planetClass
                || journalEvent.String("BodyName") is not { Length: > 0 } name
                || Ledger?.IfMapped(system, body) is not { } value
                || value < Threshold())
            {
                continue;
            }

            var terraformable = journalEvent.String("TerraformState") is "Terraformable" or "Terraforming";

            yield return new Announcement(
                $"body-value.{system}.{body}",
                $"{name}: {Phrase(planetClass, terraformable)}, about {BiologyCallout.Credits(value)} if mapped.");
        }
    }

    private static string Phrase(string planetClass, bool terraformable)
    {
        var kind = planetClass switch
        {
            "Earthlike body" => "Earth-like world",
            "Water world" => "water world",
            "Ammonia world" => "ammonia world",
            "High metal content body" => "high metal content world",
            "Metal rich body" => "metal-rich world",
            "Rocky body" => "rocky world",
            _ when planetClass.Contains("gas giant", StringComparison.OrdinalIgnoreCase) => "gas giant",
            _ => planetClass.ToLowerInvariant(),
        };

        if (terraformable)
        {
            return $"a terraformable {kind}";
        }

        return (char.ToLowerInvariant(kind[0]) is 'a' or 'e' or 'i' or 'o' or 'u' ? "an " : "a ") + kind;
    }
}
