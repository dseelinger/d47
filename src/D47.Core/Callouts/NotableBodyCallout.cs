using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// The notable traits of a scanned body, each said once per body. A scan re-checks every body already scanned in
/// its system, since a later scan can complete a trait of an earlier one.
/// </summary>
public sealed class NotableBodyCallout : ICallout
{
    private const int MostTraits = 3;

    private readonly HashSet<(long SystemAddress, int BodyId, NotableKind Kind)> _said = [];

    public string Id => "notable-bodies";

    /// <summary>Whether a kind of trait is said.</summary>
    public Func<NotableKind, bool> Criterion { get; set; } = _ => true;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "Scan"
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId)
            {
                continue;
            }

            var system = state.Scans.InSystem(systemAddress);

            foreach (var body in system.OrderBy(scan => scan.BodyId == bodyId ? 0 : 1))
            {
                if (body.BodyId is not { } id)
                {
                    continue;
                }

                var traits = NotableBodies.Of(body, system)
                    .Where(trait => !_said.Contains((systemAddress, id, trait.Kind)))
                    .ToList();

                if (context.IsPriming)
                {
                    foreach (var trait in traits)
                    {
                        _said.Add((systemAddress, id, trait.Kind));
                    }

                    continue;
                }

                var enabled = traits.Where(trait => Criterion(trait.Kind)).ToList();

                if (enabled.Count == 0)
                {
                    continue;
                }

                foreach (var trait in enabled)
                {
                    _said.Add((systemAddress, id, trait.Kind));
                }

                var named = enabled.Take(MostTraits).ToList();

                yield return new Announcement(
                    $"notable-bodies.{systemAddress}.{id}.{string.Join('-', named.Select(trait => trait.Kind))}",
                    $"{body.BodyName}: {string.Join(", ", named.Select(Phrase))}.");
            }
        }
    }

    private static string Phrase(NotableTrait trait) => trait.Kind switch
    {
        NotableKind.CollidingBinary => $"in a colliding binary with {trait.Detail}",
        NotableKind.ShepherdMoon => "orbiting inside its parent's rings",
        NotableKind.NestedMoon => "a moon of a moon",
        NotableKind.EccentricOrbit => "on a highly eccentric orbit",
        NotableKind.FastRotator => trait.Detail == "1" ? "turning once every hour" : $"turning once every {trait.Detail} hours",
        NotableKind.WideRings => "with very wide rings",
        NotableKind.PremiumFsdInjection => trait.Detail is { Length: > 0 }
            ? $"five of the six premium FSD injection materials, missing {trait.Detail}"
            : "all six premium FSD injection materials",
        NotableKind.LandableAtmosphere => "landable, with an atmosphere",
        _ => "notable",
    };
}
