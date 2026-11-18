using System.Numerics;

namespace D47.Core.Vr;

/// <summary>One tracked controller, as everything above the runtime needs to see it.</summary>
/// <param name="Device">
/// The tracked-device index, and the reason this is a type rather than a pair of poses: it is the only
/// durable identity a carry can hold across frames. "The nearer hand" is a question re-asked every
/// frame and can change answer mid-gesture; a device index cannot.
/// </param>
/// <param name="Held">The buttons held down, one bit per <see cref="VrButton"/> at <see cref="Bit"/>.</param>
/// <param name="Stick">The stick's x (right is positive) and y (forward is positive), each from -1 to 1.</param>
/// <param name="Side">Which hand the runtime says holds it.</param>
public readonly record struct VrHand(
    uint Device, VrPose Grip, VrPose Aim, uint Held = 0, Vector2 Stick = default, VrSide Side = VrSide.Unknown)
{
    /// <summary>The button's bit in <see cref="Held"/>.</summary>
    public static uint Bit(VrButton button) => 1u << (int)button;
}

/// <summary>Which hand a controller is in.</summary>
public enum VrSide
{
    Unknown = 0,
    Left = 1,
    Right = 2,
}

/// <summary>A motion controller's buttons, in the order every list of them follows.</summary>
public enum VrButton
{
    Trigger = 0,
    Grip = 1,

    /// <summary>A on the right Touch controller, X on the left.</summary>
    Primary = 2,

    /// <summary>B on the right Touch controller, Y on the left.</summary>
    Secondary = 3,

    Menu = 4,
    StickClick = 5,
}
