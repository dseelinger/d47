using System.Text.Json;

namespace D47.Core.Interface;

/// <summary>The gamma applied to a picture cut from a headset capture, and the <c>crop.json</c> that records it with the square.</summary>
public static class PictureLift
{
    /// <summary>The gamma of a headset capture's lift: each channel becomes 255 × (v / 255)^gamma.</summary>
    public const double HeadsetGamma = 0.5;

    /// <summary>The darkest brightness step, gamma 2.</summary>
    public const int DarkestStep = -4;

    /// <summary>The brightest brightness step, gamma 0.25.</summary>
    public const int BrightestStep = 8;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The lift a capture taken from <paramref name="source"/> is cut with; null when it is not lifted.</summary>
    public static double? For(string? source) => source == ScreenPictures.FromHeadset ? HeadsetGamma : null;

    /// <summary>The gamma of a brightness step: 2^(-step / 4), with the step clamped to <see cref="DarkestStep"/>..<see cref="BrightestStep"/>.</summary>
    public static double GammaOf(int step) => Math.Pow(2, -Math.Clamp(step, DarkestStep, BrightestStep) / 4.0);

    /// <summary>The brightness step of a gamma, rounded and clamped to the range; 0 when there is no gamma.</summary>
    public static int StepOf(double? gamma) =>
        gamma is { } g and > 0 && double.IsFinite(g)
            ? (int)Math.Clamp(Math.Round(-4 * Math.Log2(g)), DarkestStep, BrightestStep)
            : 0;

    /// <summary>The 256-entry table mapping a channel value through <paramref name="gamma"/>.</summary>
    public static byte[] Table(double gamma)
    {
        var table = new byte[256];

        for (var v = 0; v < table.Length; v++)
        {
            table[v] = (byte)Math.Clamp(Math.Round(255 * Math.Pow(v / 255.0, gamma)), 0, 255);
        }

        return table;
    }

    /// <summary>The <c>crop.json</c> text for a square and the gamma it is cut with; no gamma writes the square alone.</summary>
    public static string Write(CropSquare square, double? gamma) =>
        JsonSerializer.Serialize(new Record(square.X, square.Y, square.Side, gamma), Options);

    /// <summary>The gamma recorded in <c>crop.json</c> text; null when none is, or the text is not one.</summary>
    public static double? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Record>(json, Options) is { Gamma: > 0 and not 1 } record ? record.Gamma : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Record(int X, int Y, int Side, double? Gamma);
}
