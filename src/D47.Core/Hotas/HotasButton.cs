using System.Globalization;

namespace D47.Core.Hotas;

/// <summary>One button on one controller (Phase 53).</summary>
/// <param name="DeviceId">The <c>NonRoamableId</c>.</param>
/// <param name="Button">Zero-based index, as <see cref="HotasReading.Buttons"/> reports it.</param>
public readonly record struct HotasButton(string DeviceId, int Button)
{
    /// <summary>The stored form.</summary>
    public override string ToString() =>
        $"{DeviceId}#{Button.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Reads the stored form back, or nothing.</summary>
    public static HotasButton? Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        var split = stored.LastIndexOf('#');

        if (split <= 0 || split == stored.Length - 1)
        {
            return null;
        }

        return int.TryParse(
            stored[(split + 1)..],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var button) && button >= 0
            ? new HotasButton(stored[..split], button)
            : null;
    }

    /// <summary>How it is said aloud and drawn.</summary>
    public string Describe() => $"button {Button + 1}";
}
