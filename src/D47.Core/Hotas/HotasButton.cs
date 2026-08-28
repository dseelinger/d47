using System.Globalization;

namespace D47.Core.Hotas;

/// <summary>
/// One button on one controller (Phase 53).
/// <para>
/// <b>The device is the hard half.</b> Several HOTAS controllers is the ordinary case — the
/// WinWing throttle alone presents four interfaces with 4x32 mode on — so <em>button 7</em> alone
/// is ambiguous and always was. The identity is the <c>NonRoamableId</c> plus the index, which is
/// exactly what <see cref="SwitchMapping.DeviceId"/> already keys on, reused rather than invented
/// a second time.
/// </para>
/// </summary>
/// <param name="DeviceId">The <c>NonRoamableId</c>. Survives a replug and a reboot.</param>
/// <param name="Button">Zero-based index, as <see cref="HotasReading.Buttons"/> reports it.</param>
public readonly record struct HotasButton(string DeviceId, int Button)
{
    /// <summary>
    /// The stored form. One string rather than two properties because <c>settings.json</c> is
    /// append-only: one new key is one thing that can never be removed, where two is two.
    /// <para>
    /// The separator is a <c>#</c> because a <c>NonRoamableId</c> is base64-ish and contains
    /// <c>+</c>, <c>/</c> and <c>=</c> — a colon or a slash would split some Commanders' device
    /// ids and not others, which is the kind of fault that only shows up on hardware nobody
    /// testing it owns.
    /// </para>
    /// </summary>
    public override string ToString() =>
        $"{DeviceId}#{Button.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Reads the stored form back, or nothing. A hand-edited file can contain anything, and an
    /// unreadable binding is an unbound button rather than a crash.
    /// </summary>
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

    /// <summary>
    /// How it is said aloud and drawn. One-based, because every stick in the world prints its
    /// buttons from one and a Commander told "button 6" would look for the wrong one.
    /// </summary>
    public string Describe() => $"button {Button + 1}";
}
