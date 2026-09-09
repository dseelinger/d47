using D47.Core.Journal;

namespace D47.Core.Input;

/// <summary>
/// Which control mode the Commander is in right now, as a set so an action can declare the several it
/// works in (Phase 10, "Offer only the actions that work right now").
/// </summary>
[Flags]
public enum ControlContext
{
    None = 0,

    /// <summary>Docked at a station or outpost.</summary>
    Docked = 1 << 0,

    /// <summary>Landed on a surface in the ship.</summary>
    Landed = 1 << 1,

    /// <summary>In a ship, in normal space, flying.</summary>
    NormalSpace = 1 << 2,

    Supercruise = 1 << 3,

    /// <summary>Mid-jump.</summary>
    Hyperspace = 1 << 4,

    Srv = 1 << 5,

    OnFoot = 1 << 6,

    /// <summary>In a ship-launched fighter, which has its own much smaller control set.</summary>
    Fighter = 1 << 7,

    /// <summary>Everything a main ship can be doing.</summary>
    AnyShip = Docked | Landed | NormalSpace | Supercruise,

    /// <summary>Flying rather than parked, which is what most flight actions actually want.</summary>
    Flying = NormalSpace | Supercruise,
}

public static class ControlContexts
{
    /// <summary>Reads the current context off Status.json.</summary>
    public static ControlContext Of(GameStatus status)
    {
        if (!status.IsKnown)
        {
            return ControlContext.None;
        }

        // Order matters.
        if (status.OnFoot)
        {
            return ControlContext.OnFoot;
        }

        if (status.Has(StatusFlags.InSrv))
        {
            return ControlContext.Srv;
        }

        if (status.Has(StatusFlags.InFighter))
        {
            return ControlContext.Fighter;
        }

        // Mid-jump before anything else about the ship: the controls belong to the game during the tunnel,
        // and a key sent then is a key the Commander did not press and cannot undo.
        if (status.Has(StatusFlags.FsdJump))
        {
            return ControlContext.Hyperspace;
        }

        if (status.Has(StatusFlags.Docked))
        {
            return ControlContext.Docked;
        }

        if (status.Has(StatusFlags.Landed))
        {
            return ControlContext.Landed;
        }

        if (status.Has(StatusFlags.Supercruise))
        {
            return ControlContext.Supercruise;
        }

        return status.Has(StatusFlags.InMainShip) ? ControlContext.NormalSpace : ControlContext.None;
    }

    /// <summary>How a Commander would hear the mode named, for the reason an action gives back.</summary>
    public static string Describe(ControlContext context) => context switch
    {
        ControlContext.Docked => "docked",
        ControlContext.Landed => "landed",
        ControlContext.NormalSpace => "in normal space",
        ControlContext.Supercruise => "in supercruise",
        ControlContext.Hyperspace => "in witchspace",
        ControlContext.Srv => "in the SRV",
        ControlContext.OnFoot => "on foot",
        ControlContext.Fighter => "in a fighter",
        _ => "somewhere D47 cannot identify",
    };
}
