using System.Numerics;

namespace D47.Core.Vr;

/// <summary>
/// One step of moving a surface that is already in the room
/// (<a href="https://github.com/dseelinger/d47/issues/199">#199</a>).
/// <para>
/// Six of these are translations and four are rotations, and they are one enumeration because
/// they are one gesture: <em>the panel is not quite where I want it</em>. What separates them
/// from the placement rows is that a row is a value and these are deltas — a world-locked
/// surface's position is not in <c>settings.json</c> at all, it is the anchor pose in view
/// state, and there is no number there for a row to name.
/// </para>
/// </summary>
public enum VrNudge
{
    Left,
    Right,
    Up,
    Down,

    /// <summary>Towards the Commander, along the ground rather than along the tilt.</summary>
    Nearer,

    /// <summary>Away from the Commander, the same way.</summary>
    Further,

    /// <summary>Swings the panel's face towards the Commander's left, about the vertical.</summary>
    TurnLeft,

    TurnRight,

    /// <summary>Tips the panel's face upwards, which is what a panel below eye level wants.</summary>
    TiltUp,

    TiltDown,
}

/// <summary>
/// How a nudge went, so the sentence the Commander hears is written once, in Core, rather than
/// by whichever host happened to move the panel.
/// </summary>
public enum VrNudgeOutcome
{
    /// <summary>The surface was already down in the room, and moved.</summary>
    Moved,

    /// <summary>
    /// It was riding the head, so it was put down in front of the Commander first and then
    /// moved. The same ruling a carry already makes — see <c>VrHost.Carry</c>, where picking the
    /// panel up switches the lock to world because a Commander who has moved it has said where
    /// they want it.
    /// </summary>
    PutDown,

    /// <summary>
    /// There is no head pose to put anything down against. Not a refusal to move a surface — a
    /// statement that there is no session with a surface in it yet.
    /// </summary>
    NoHeadset,
}

/// <summary>
/// The arithmetic of nudging, and the words for it. Pure and in Core for the reason all the
/// placement maths is (architecture.md §8): if a headset is needed to check this, it is in the
/// wrong place.
/// <para>
/// <b>Every axis is worked out from the surface's own pose, and none of them from the head.</b>
/// A panel put down in the room stays where it is while the Commander looks around, so a nudge
/// resolved against the head would mean something different depending on which way they happened
/// to be facing when they said it. The surface's face already points at where they were when they
/// placed it, which is the frame they mean.
/// </para>
/// </summary>
public static class VrNudges
{
    /// <summary>
    /// How far one step moves it. The same 0.05 m the distance and size rows step by, so a
    /// Commander who has used one already knows how big a nudge is.
    /// </summary>
    public const float StepMetres = 0.05f;

    /// <summary>How far one step turns or tilts it.</summary>
    public const float StepDegrees = 5f;

    /// <summary>
    /// The most a single call may do. Not a clamp on where a panel can end up — repeated calls
    /// go as far as anybody likes — but on how much one mis-heard number can move it, which
    /// matters because a panel that leaves the room is a panel nobody can find to move back.
    /// </summary>
    public const int MostSteps = 20;

    /// <summary>
    /// What each direction is called on the wire, in the enum's own order, so the tool schema and
    /// <see cref="Parse"/> cannot disagree about the vocabulary.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
        ["left", "right", "up", "down", "nearer", "further", "turn-left", "turn-right", "tilt-up", "tilt-down"];

    /// <summary>One of <see cref="Names"/>, or null for anything else.</summary>
    public static VrNudge? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var index = -1;

        for (var i = 0; i < Names.Count; i++)
        {
            if (string.Equals(Names[i], value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        return index < 0 ? null : (VrNudge)index;
    }

    /// <summary>How many steps a call actually takes: at least one, and never more than a score.</summary>
    public static int Steps(int asked) => Math.Clamp(asked <= 0 ? 1 : asked, 1, MostSteps);

    /// <summary>
    /// The surface, moved.
    /// <para>
    /// Up and down are the <em>world's</em> vertical rather than the surface's own, and near and
    /// far are along the ground rather than along the face. A panel below eye level is tilted back
    /// to be read, so its own axes are tilted too — nudging it nearer along its face would raise
    /// it at the same time, which is one gesture doing two things and the second one unasked for.
    /// </para>
    /// <para>
    /// Turning is about the world's vertical through the surface's own centre, which is the same
    /// pivot idiom <see cref="VrPlacementMath.Reanchored"/> uses. Tilting is about the surface's
    /// <em>own</em> lateral axis, because tilting is what that axis is for.
    /// </para>
    /// </summary>
    public static VrPose Apply(VrPose placed, VrNudge nudge, int steps)
    {
        var count = Steps(steps);
        var metres = StepMetres * count;
        var radians = StepDegrees * count * MathF.PI / 180f;
        var (right, toward) = Basis(placed);

        return nudge switch
        {
            VrNudge.Up => Shifted(placed, Vector3.UnitY * metres),
            VrNudge.Down => Shifted(placed, Vector3.UnitY * -metres),
            VrNudge.Right => Shifted(placed, right * metres),
            VrNudge.Left => Shifted(placed, right * -metres),
            VrNudge.Nearer => Shifted(placed, toward * metres),
            VrNudge.Further => Shifted(placed, toward * -metres),

            // NEGATIVE for left, and the sign is worth stating rather than deriving twice. A
            // rotation about +Y carries the surface's +Z — its visible face, which is the whole of
            // why Resting's pitch is negative — towards +X, and +X is the Commander's right while
            // the panel is facing them. Turning it left is its face going the other way.
            VrNudge.TurnLeft => Turned(placed, -radians),
            VrNudge.TurnRight => Turned(placed, radians),

            // And negative for up, for the same reason one layer down: a positive rotation about
            // X carries +Z downwards, so the obvious sign tilts a panel away from the Commander
            // exactly when they asked for it to lean back towards them.
            VrNudge.TiltUp => Tilted(placed, -radians),
            _ => Tilted(placed, radians),
        };
    }

    /// <summary>
    /// What the Commander is told. One sentence, naming the gesture rather than the numbers:
    /// "moved it 5 cm left" is a measurement of something they are looking at.
    /// </summary>
    public static string Describe(VrNudge nudge, VrNudgeOutcome outcome)
    {
        var did = Did(nudge);

        return outcome switch
        {
            VrNudgeOutcome.NoHeadset =>
                "There is no headset session to move a panel in yet.",
            VrNudgeOutcome.PutDown =>
                $"The panel was riding your head, so I have put it down in front of you and {did}.",
            _ => $"{char.ToUpperInvariant(did[0])}{did[1..]}.",
        };
    }

    /// <summary>The gesture as a past-tense clause, so it reads the same in both sentences above.</summary>
    private static string Did(VrNudge nudge) => nudge switch
    {
        VrNudge.Left => "moved it left",
        VrNudge.Right => "moved it right",
        VrNudge.Up => "moved it up",
        VrNudge.Down => "moved it down",
        VrNudge.Nearer => "brought it nearer",
        VrNudge.Further => "pushed it further away",
        VrNudge.TurnLeft => "turned it left",
        VrNudge.TurnRight => "turned it right",
        VrNudge.TiltUp => "tilted it up",
        _ => "tilted it down",
    };

    private static VrPose Shifted(VrPose placed, Vector3 by) =>
        placed with { Position = placed.Position + by };

    private static VrPose Turned(VrPose placed, float radians) => VrPose.FromMatrix(
        placed.ToMatrix()
        * Matrix4x4.CreateTranslation(-placed.Position)
        * Matrix4x4.CreateRotationY(radians)
        * Matrix4x4.CreateTranslation(placed.Position));

    private static VrPose Tilted(VrPose placed, float radians) =>
        VrPose.FromMatrix(Matrix4x4.CreateRotationX(radians) * placed.ToMatrix());

    /// <summary>
    /// The surface's compass frame: which way is right, and which way is towards the Commander,
    /// with the tilt taken out of both.
    /// <para>
    /// Right is derived from towards rather than read off the surface's own +X, and that is
    /// deliberate: the two agree for every pose anything in d47 can produce — nothing rolls a
    /// panel — and deriving leaves one degenerate case to handle instead of two.
    /// </para>
    /// </summary>
    private static (Vector3 Right, Vector3 Toward) Basis(VrPose placed)
    {
        // A surface tilted to face straight up or straight down has no horizontal face direction
        // at all. Its own +Y is horizontal there, pointing away from the Commander — so negated,
        // it is the answer, and it degenerates only where the first one does not.
        var toward = Flat(Vector3.Transform(Vector3.UnitZ, placed.Facing))
                     ?? Flat(-Vector3.Transform(Vector3.UnitY, placed.Facing))
                     ?? Vector3.UnitZ;

        // (0,1,0) x (0,0,1) is (1,0,0): with the face pointing back at a Commander looking along
        // -Z, right is +X, which is theirs.
        return (Vector3.Normalize(Vector3.Cross(Vector3.UnitY, toward)), toward);
    }

    /// <summary>The horizontal part of a direction, unit length, or null when there is none.</summary>
    private static Vector3? Flat(Vector3 direction)
    {
        var flattened = new Vector3(direction.X, 0f, direction.Z);

        return flattened.LengthSquared() < 1e-6f ? null : Vector3.Normalize(flattened);
    }
}
