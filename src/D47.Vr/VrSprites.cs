using D47.Core.Vr;

namespace D47.Vr;

/// <summary>
/// The two pictures that are not the panel: the aim beam and the cursor on the end of it.
/// <para>
/// Built in code rather than shipped as files, because both are a few hundred bytes of gradient
/// and a ring — an asset would be a build-and-publish concern bought for nothing. Uploaded once
/// each, at creation, and thereafter moved only by transform.
/// </para>
/// <para>
/// <b>Premultiplied.</b> The overlays carry <c>IsPremultiplied</c>, like the panel, so every colour
/// here is already scaled by its own alpha. Straight alpha over a cockpit shows up as a dark fringe
/// rather than as anything that reports itself.
/// </para>
/// </summary>
public static class VrSprites
{
    /// <summary>
    /// The accent both share, so the beam visibly belongs to the dot on its end. d47's amber,
    /// which is the one colour the panel does not use for text.
    /// </summary>
    private static readonly (byte R, byte G, byte B) Accent = (255, 176, 64);

    /// <summary>
    /// The beam: solid at the hand and fading along its length, softened across it so the edges
    /// are not two hard lines a pixel wide.
    /// </summary>
    public static byte[] Beam()
    {
        var pixels = new byte[VrAim.BeamPixelsWide * VrAim.BeamPixelsTall * 4];

        // Across the beam. Four pixels, dimmer at the edges than in the middle.
        float[] across = [0.45f, 1f, 1f, 0.45f];

        for (var row = 0; row < VrAim.BeamPixelsTall; row++)
        {
            // Row zero is the far end, because the quad's +Y runs along the aim and V counts down
            // from the top. Brightest there and fading back towards the hand, which puts the
            // strongest part of the beam where the Commander is trying to look rather than on the
            // controller they can already see.
            var along = 1f - ((1f - 0.15f) * row / (VrAim.BeamPixelsTall - 1));

            for (var column = 0; column < VrAim.BeamPixelsWide; column++)
            {
                Put(pixels, ((row * VrAim.BeamPixelsWide) + column) * 4, along * across[column]);
            }
        }

        return pixels;
    }

    /// <summary>
    /// The cursor: a ring rather than a filled disc, so it marks where the ray lands without
    /// hiding whatever it lands on.
    /// </summary>
    public static byte[] Cursor()
    {
        const int Size = 64;
        const float Outer = (Size / 2f) - 1f;
        const float Inner = Outer - 4f;

        var pixels = new byte[Size * Size * 4];
        var centre = (Size - 1) / 2f;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var dx = x - centre;
                var dy = y - centre;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));

                // Feathered on both edges by a pixel, which is what stops a small ring looking
                // like a staircase at the distance it is actually read from.
                var alpha = Math.Clamp(Outer - distance, 0f, 1f)
                            * Math.Clamp(distance - Inner, 0f, 1f);

                Put(pixels, ((y * Size) + x) * 4, alpha);
            }
        }

        return pixels;
    }

    public static int CursorSize => 64;

    private static void Put(byte[] pixels, int at, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);

        pixels[at] = (byte)(Accent.R * alpha);
        pixels[at + 1] = (byte)(Accent.G * alpha);
        pixels[at + 2] = (byte)(Accent.B * alpha);
        pixels[at + 3] = (byte)(255 * alpha);
    }
}
