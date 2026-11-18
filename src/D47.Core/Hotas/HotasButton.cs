using System.Globalization;

namespace D47.Core.Hotas;

/// <summary>One button on one controller (Phase 53).</summary>
/// <param name="DeviceId">The <see cref="HidIdentity"/> of the interface.</param>
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

    /// <summary>How it is said aloud and drawn: "button 5" on a stick, "right controller B" on a motion controller.</summary>
    public string Describe()
    {
        var side = DeviceId switch
        {
            Vr.MotionControllerButtons.LeftId => "left",
            Vr.MotionControllerButtons.RightId => "right",
            _ => null,
        };

        if (side is null || ControllerInput(Button, side == "left") is not { } input)
        {
            return $"button {Button + 1}";
        }

        return $"{side} controller {input}";
    }

    private static string? ControllerInput(int button, bool left) => button switch
    {
        (int)Vr.VrButton.Trigger => "trigger",
        (int)Vr.VrButton.Grip => "grip",
        (int)Vr.VrButton.Primary => left ? "X" : "A",
        (int)Vr.VrButton.Secondary => left ? "Y" : "B",
        (int)Vr.VrButton.Menu => "menu button",
        (int)Vr.VrButton.StickClick => "stick click",
        Vr.MotionControllerButtons.StickUp => "stick up",
        Vr.MotionControllerButtons.StickDown => "stick down",
        Vr.MotionControllerButtons.StickLeft => "stick left",
        Vr.MotionControllerButtons.StickRight => "stick right",
        _ => null,
    };
}
