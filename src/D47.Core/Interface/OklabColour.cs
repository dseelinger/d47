namespace D47.Core.Interface;

/// <summary>A colour in OKLab: perceptually uniform lightness on <see cref="L"/>, chroma on <see cref="A"/> and <see cref="B"/>.</summary>
public readonly record struct OklabColour(double L, double A, double B)
{
    public static OklabColour FromSrgb(byte r, byte g, byte b)
    {
        var (rl, gl, bl) = (SrgbToLinear(r), SrgbToLinear(g), SrgbToLinear(b));

        var l = (0.4122214708 * rl) + (0.5363325363 * gl) + (0.0514459929 * bl);
        var m = (0.2119034982 * rl) + (0.6806995451 * gl) + (0.1073969566 * bl);
        var s = (0.0883024619 * rl) + (0.2817188376 * gl) + (0.6299787005 * bl);

        var l_ = Math.Cbrt(l);
        var m_ = Math.Cbrt(m);
        var s_ = Math.Cbrt(s);

        return new OklabColour(
            (0.2104542553 * l_) + (0.7936177850 * m_) - (0.0040720468 * s_),
            (1.9779984951 * l_) - (2.4285922050 * m_) + (0.4505937099 * s_),
            (0.0259040371 * l_) + (0.7827717662 * m_) - (0.8086757660 * s_));
    }

    public (byte R, byte G, byte B) ToSrgb()
    {
        var l_ = L + (0.3963377774 * A) + (0.2158037573 * B);
        var m_ = L - (0.1055613458 * A) - (0.0638541728 * B);
        var s_ = L - (0.0894841775 * A) - (1.2914855480 * B);

        var l = l_ * l_ * l_;
        var m = m_ * m_ * m_;
        var s = s_ * s_ * s_;

        var rl = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var gl = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var bl = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        return (LinearToSrgb(rl), LinearToSrgb(gl), LinearToSrgb(bl));
    }

    public OklchColour ToOklch()
    {
        var c = Math.Sqrt((A * A) + (B * B));
        var h = Math.Atan2(B, A) * 180.0 / Math.PI;

        return new OklchColour(L, c, h < 0 ? h + 360.0 : h);
    }

    public static OklabColour FromOklch(OklchColour oklch)
    {
        var radians = oklch.H * Math.PI / 180.0;
        return new OklabColour(oklch.L, oklch.C * Math.Cos(radians), oklch.C * Math.Sin(radians));
    }

    private static double SrgbToLinear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte LinearToSrgb(double linear)
    {
        var clamped = Math.Clamp(linear, 0.0, 1.0);
        var encoded = clamped <= 0.0031308 ? clamped * 12.92 : (1.055 * Math.Pow(clamped, 1.0 / 2.4)) - 0.055;

        return (byte)Math.Clamp(Math.Round(encoded * 255.0), 0, 255);
    }
}

/// <summary>OKLab in cylindrical form: <see cref="L"/> unchanged, <see cref="A"/> and <see cref="B"/> as chroma and hue degrees.</summary>
public readonly record struct OklchColour(double L, double C, double H);

/// <summary>Colour arithmetic done in OKLab rather than sRGB, so a mix or a hue swap matches how the eye sees lightness.</summary>
public static class OklabMixing
{
    /// <summary>Lerps two sRGB colours in OKLab. <paramref name="t"/> runs 0 to 1.</summary>
    public static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double t)
    {
        var oa = OklabColour.FromSrgb(a.R, a.G, a.B);
        var ob = OklabColour.FromSrgb(b.R, b.G, b.B);

        return new OklabColour(
            oa.L + ((ob.L - oa.L) * t),
            oa.A + ((ob.A - oa.A) * t),
            oa.B + ((ob.B - oa.B) * t)).ToSrgb();
    }

    /// <summary>Keeps <paramref name="colour"/>'s lightness and chroma, substituting its hue for <paramref name="degrees"/>.</summary>
    public static (byte R, byte G, byte B) WithHue((byte R, byte G, byte B) colour, double degrees)
    {
        var oklch = OklabColour.FromSrgb(colour.R, colour.G, colour.B).ToOklch();
        return OklabColour.FromOklch(oklch with { H = degrees }).ToSrgb();
    }
}
