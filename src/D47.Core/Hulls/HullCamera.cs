using System.Numerics;

namespace D47.Core.Hulls;

/// <summary>Orbit camera for the hull viewer. Orbit turns the camera about Target; Radius is the mesh radius.</summary>
public readonly record struct HullCamera(Quaternion Orbit, float Distance, Vector3 Target, float Light, float Radius)
{
    public const float FieldOfViewDegrees = 35f;
    public const float DegreesPerPixel = 0.4f;
    public const float ZoomStep = 1.15f;
    public const float MinDistanceRadii = 0.2f;
    public const float MaxDistanceRadii = 10f;
    public const float MaxLight = 2f;

    private const float Margin = 1.1f;
    private const float MinNearRadii = 0.01f;

    private static float FieldOfView => FieldOfViewDegrees * MathF.PI / 180f;

    /// <summary>Looks at the origin from the mesh's rest view, at the distance that fits its radius.</summary>
    public static HullCamera Rest(HullMesh mesh)
    {
        var back = Vector3.Normalize(mesh.RestView);
        var right = Vector3.Cross(Vector3.UnitY, back);
        right = right.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(right);
        var up = Vector3.Cross(back, right);
        var basis = new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            up.X, up.Y, up.Z, 0,
            back.X, back.Y, back.Z, 0,
            0, 0, 0, 1);
        var distance = mesh.Radius * Margin / MathF.Sin(FieldOfView / 2f);
        return new HullCamera(Quaternion.CreateFromRotationMatrix(basis), distance, Vector3.Zero, 1f, mesh.Radius);
    }

    /// <summary>Drag right turns the hull right and drag down turns it down.</summary>
    public HullCamera Turn(float dxPixels, float dyPixels)
    {
        var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -Radians(dxPixels));
        var pitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -Radians(dyPixels));
        return this with { Orbit = Quaternion.Normalize(Orbit * yaw * pitch) };
    }

    public HullCamera Roll(float dxPixels)
    {
        var roll = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, Radians(dxPixels));
        return this with { Orbit = Quaternion.Normalize(Orbit * roll) };
    }

    /// <summary>Moves Target so the point at Target's depth stays under the cursor.</summary>
    public HullCamera Pan(float dxPixels, float dyPixels, int viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            return this;
        }

        var perPixel = 2f * Distance * MathF.Tan(FieldOfView / 2f) / viewportHeight;
        var right = Vector3.Transform(Vector3.UnitX, Orbit);
        var up = Vector3.Transform(Vector3.UnitY, Orbit);
        return this with { Target = Target - right * (dxPixels * perPixel) + up * (dyPixels * perPixel) };
    }

    public HullCamera Zoom(float notches) =>
        this with { Distance = Math.Clamp(Distance * MathF.Pow(ZoomStep, -notches), Radius * MinDistanceRadii, Radius * MaxDistanceRadii) };

    public HullCamera Relight(float delta) => this with { Light = Math.Clamp(Light + delta, 0f, MaxLight) };

    public Matrix4x4 View()
    {
        var eye = Target + Vector3.Transform(Vector3.UnitZ, Orbit) * Distance;
        return Matrix4x4.CreateLookAt(eye, Target, Vector3.Transform(Vector3.UnitY, Orbit));
    }

    /// <summary>Clip planes bracket the mesh sphere at the origin, wherever Target has been panned.</summary>
    public Matrix4x4 Projection(float aspect)
    {
        var reach = Radius + Target.Length();
        var near = MathF.Max(MinNearRadii * Radius, Distance - reach);
        return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspect, near, Distance + reach);
    }

    private static float Radians(float pixels) => pixels * DegreesPerPixel * MathF.PI / 180f;
}
