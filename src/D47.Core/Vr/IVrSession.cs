using System.Numerics;

namespace D47.Core.Vr;

/// <summary>A headset runtime with the head, the motion controllers and the surfaces the host places.</summary>
public interface IVrSession : IVrRuntime
{
    /// <summary>Whether d47 may touch the motion controllers at all (#198).</summary>
    bool Pointing { get; set; }

    /// <summary>Whether a surface's curvature is drawn. Where it is not, every surface is flat.</summary>
    bool SupportsCurvature { get; }

    /// <summary>The last head pose read.</summary>
    VrPose? Head { get; }

    /// <summary>The controllers and the head from one pose read.</summary>
    (IReadOnlyList<VrHand> Hands, VrPose? Head) HandsAndHead();

    /// <summary>Readies the trigger and the back button. Called on every tick while pointing, so it returns at once after the first success.</summary>
    void PrepareInput();

    /// <summary>Whether the trigger is held; claims the controllers while <paramref name="wanted"/> is true and gives them back otherwise.</summary>
    bool TriggerHeld(bool wanted);

    /// <summary>Whether the back button was pressed this frame.</summary>
    bool BackPressed();

    /// <summary>Gives the controllers back.</summary>
    void ReleaseInput();

    /// <summary>Moves a surface to a world pose.</summary>
    void Reposition(VrSurface surface, VrPose where);

    /// <summary>
    /// Points the beam along a hand and stops it at <paramref name="lengthMetres"/>, or takes it off
    /// screen when <paramref name="along"/> is null.
    /// </summary>
    void AimBeam(VrPose? along, VrPose head, float lengthMetres);

    /// <summary>Puts the cursor on a world point, or takes it off screen when <paramref name="at"/> is null.</summary>
    void ShowCursor(Vector3? at, VrPose head, VrCursor kind);
}
