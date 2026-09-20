using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class OklabColourTests
{
    // Every Accent and Background from src/D47.App/Theming/Palette.cs. D47.Core.Tests does not
    // reference D47.App, so the values are copied rather than read from the type.
    public static TheoryData<byte, byte, byte> PaletteAccentsAndBackgrounds => new()
    {
        { 0xF5, 0x85, 0x0F }, // Elite Accent
        { 0x4C, 0x8D, 0xFF }, // Dark Accent
        { 0x0A, 0x64, 0xC8 }, // Light Accent
        { 0x2F, 0xD3, 0xB5 }, // Guardian Accent
        { 0x00, 0x00, 0x00 }, // Elite Background
        { 0x12, 0x12, 0x12 }, // Dark Background
        { 0xF4, 0xF4, 0xF2 }, // Light Background
        { 0x06, 0x10, 0x0F }, // Guardian Background
    };

    [Theory]
    [MemberData(nameof(PaletteAccentsAndBackgrounds))]
    public void ARoundTripThroughOklabReturnsTheSameByte(byte r, byte g, byte b)
    {
        var (rr, rg, rb) = OklabColour.FromSrgb(r, g, b).ToSrgb();

        Assert.Equal((r, g, b), (rr, rg, rb));
    }

    // OKLab's lightness runs close to the cube root of linear luminance, the same curve behind
    // CIE Lab's own "L*=50 is an 18%-reflectance grey" convention. That cube root sits below
    // sRGB's own gamma (an exponent near 1/2.4), so the midpoint comes out darker than a naive
    // byte average, not lighter.
    [Fact]
    public void MixingBlackAndWhiteAtHalfIsDarkerThanSrgbGrey()
    {
        var (r, g, b) = OklabMixing.Mix((0, 0, 0), (255, 255, 255), 0.5);

        Assert.Equal(r, g);
        Assert.Equal(g, b);
        Assert.True(r < 0x80, $"expected darker than #808080, got #{r:X2}{g:X2}{b:X2}");
    }

    [Fact]
    public void WithHueKeepsLightnessAndChroma()
    {
        var accent = OklabColour.FromSrgb(0xF5, 0x85, 0x0F).ToOklch();

        var swapped = OklabColour.FromOklch(accent with { H = 27 }).ToOklch();

        Assert.Equal(accent.L, swapped.L, 3);
        Assert.Equal(accent.C, swapped.C, 3);
        Assert.Equal(27, swapped.H, 3);
    }

    // Elite's accent held at its own lightness and chroma but turned to 27 degrees falls outside
    // sRGB: the red channel wants more than full intensity. It must land at 255, not wrap.
    [Fact]
    public void OutOfGamutResultsClampInsteadOfWrapping()
    {
        var (r, g, b) = OklabMixing.WithHue((0xF5, 0x85, 0x0F), 27);

        Assert.Equal(255, r);
        Assert.InRange(g, 0, 255);
        Assert.InRange(b, 0, 255);
    }
}
