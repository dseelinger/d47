namespace D47.Core.Vr;

/// <summary>One tracked controller, as everything above the runtime needs to see it.</summary>
/// <param name="Device">
/// The tracked-device index, and the reason this is a type rather than a pair of poses: it is the only
/// durable identity a carry can hold across frames. "The nearer hand" is a question re-asked every
/// frame and can change answer mid-gesture; a device index cannot.
/// </param>
public readonly record struct VrHand(uint Device, VrPose Grip, VrPose Aim);
