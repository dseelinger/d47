using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>Says which way the Powerplay merit modifier leans on first entering normal space in a system, once per system per session.</summary>
public sealed class PowerplayMeritsCallout : ICallout
{
    public string Id => "powerplay-merits";

    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var state = context.State;
        var location = state?.Location;

        if (location is not { Mode: FlightMode.Normal, StarSystem: { Length: > 0 } system }
            || !state!.Pledge.IsPledged
            || !_seen.Add(system)
            || context.IsPriming)
        {
            yield break;
        }

        var text = PowerplayRules.Classify(state.Pledge, location) switch
        {
            MeritSituation.OwnNotUndermined =>
                "Reinforcing here earns reduced merits. Nobody has undermined this system this cycle.",
            MeritSituation.OwnUndermined =>
                "Rivals have undermined this system this cycle. Reinforcing pays more here than in a quiet system, while that lasts.",
            MeritSituation.Rival =>
                "Undermining here earns fifteen percent more merits.",
            _ => null,
        };

        if (text is not null)
        {
            yield return new Announcement($"powerplay.merits.{system}", text);
        }
    }
}
