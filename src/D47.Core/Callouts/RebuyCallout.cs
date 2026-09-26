using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Says once when the balance will not cover the rebuy on the ship being flown (#484). Re-arms when the
/// balance covers it again or the ship changes.
/// </summary>
public sealed class RebuyCallout : ICallout
{
    public string Id => "rebuy";

    private static readonly TimeSpan ShortCooldown = TimeSpan.FromMinutes(5);

    private bool _warned;
    private int? _warnedShipId;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        var status = context.Status;

        // Unknown figures leave the edge where it was, so a gap in Status.json does not repeat the line.
        if (!status.IsKnown
            || status.Balance is not { } balance
            || !state.Ship.IsKnown
            || state.Ship.Rebuy is not { } rebuy
            || rebuy <= 0)
        {
            yield break;
        }

        // Another Commander's ship, or a taxi: not the ship this rebuy is for.
        if (status.Has2(StatusFlags2.InMulticrew) || status.Has2(StatusFlags2.InTaxi))
        {
            yield break;
        }

        if (balance >= rebuy)
        {
            _warned = false;
            yield break;
        }

        var shipId = state.Ship.ShipId;

        if (_warned && shipId == _warnedShipId)
        {
            yield break;
        }

        _warned = true;
        _warnedShipId = shipId;

        yield return new Announcement(
            "rebuy.short",
            $"Rebuy on this ship is {SpokenCredits.Band(rebuy)} credits. You have {SpokenCredits.Band(balance)}.")
        {
            Cooldown = ShortCooldown,
        };
    }
}
