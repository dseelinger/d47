namespace D47.Core.Vr;

/// <summary>
/// One tracked controller, as everything above the runtime needs to see it.
/// <para>
/// <b><see cref="Grip"/> is not where the controller points.</b> OpenVR reports the grip pose,
/// which sits inside the handle and runs roughly along it. What a Commander means by pointing is
/// the tip, and on Touch controllers the two differ by a large angle — large enough that a ray
/// cast from the grip lands nowhere near the laser coming out of their own hand. <see cref="Aim"/>
/// is the corrected one, and is the only one anything should cast from.
/// </para>
/// <para>
/// Both are carried rather than just the aim, because they answer different questions: the aim is
/// where a ray goes, and the grip is where the hand is. A carry freezes its offset against the
/// aim, so that is what it uses; a diagnostic that wants to say where a controller physically is
/// wants the other.
/// </para>
/// </summary>
/// <param name="Device">
/// The tracked-device index, and the reason this is a type rather than a pair of poses: it is the
/// only durable identity a carry can hold across frames. "The nearer hand" is a question re-asked
/// every frame and can change answer mid-gesture; a device index cannot.
/// </param>
public readonly record struct VrHand(uint Device, VrPose Grip, VrPose Aim);
