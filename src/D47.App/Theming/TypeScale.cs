namespace D47.App.Theming;

/// <summary>
/// The five sizes d47 draws text at, named by the job the text is doing.
/// <para>
/// <b>Body is whatever the controls are.</b> That is the whole point of the scale. Avalonia's
/// controls render at 14 and d47 used to set its own text by hand at 10 to 13, so a row's label
/// came out smaller than the combo box it was labelling — a caption dwarfed by its own value.
/// Forty-six size literals across ten files with nothing naming them is how they drifted apart,
/// and the only durable fix is that there is now nowhere to write a number.
/// </para>
/// <para>
/// It resolves upwards rather than down. A settings surface is read at a glance from a metre
/// away in a headset, and the answer to text that is too small next to a control is never a
/// smaller control.
/// </para>
/// <para>
/// Sizes are device-independent pixels, unscaled: zoom is a layout transform over the whole
/// tree (see <c>ZoomHost</c>), so nothing here multiplies by a zoom level and nothing here
/// should.
/// </para>
/// </summary>
public static class TypeScale
{
    /// <summary>A window or section title. Used sparingly — one per surface.</summary>
    public const double Heading = 20;

    /// <summary>A group within a surface: a settings section, a dialog's second rank.</summary>
    public const double Subheading = 16;

    /// <summary>
    /// Ordinary text, and the size every framework control already draws at. A label beside a
    /// control is this, because a label that is not is a label that looks like a footnote.
    /// </summary>
    public const double Body = 14;

    /// <summary>
    /// Supporting text that is genuinely subordinate: a row's help line, the provenance line
    /// under the transcript. One step down, not three.
    /// </summary>
    public const double Secondary = 13;

    /// <summary>The smallest d47 will draw: a badge, a unit, a count beside something else.</summary>
    public const double Small = 12;
}
