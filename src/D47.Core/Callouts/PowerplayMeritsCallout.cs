using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// Says which way the Powerplay merit modifier leans on first entering normal space in a system, once per visit.
/// The system the Commander is in at priming counts as already said, so a restart of d47 or the game says nothing
/// until the Commander leaves and arrives somewhere.
/// </summary>
public sealed class PowerplayMeritsCallout : ICallout
{
    public string Id => "powerplay-merits";

    /// <summary>The system said this visit, or null once the Commander is in a different one.</summary>
    private string? _said;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var state = context.State;
        var location = state?.Location;

        if (location is not { StarSystem: { Length: > 0 } system })
        {
            yield break;
        }

        if (!string.Equals(_said, system, StringComparison.OrdinalIgnoreCase))
        {
            _said = null;
        }

        if (context.IsPriming)
        {
            _said = system;
            yield break;
        }

        if (location.Mode != FlightMode.Normal || !state!.Pledge.IsPledged || _said is not null)
        {
            yield break;
        }

        _said = system;

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
