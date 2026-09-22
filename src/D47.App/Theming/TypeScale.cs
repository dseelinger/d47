namespace D47.App.Theming;

/// <summary>The eight sizes d47 draws text at, named by the job the text is doing.</summary>
public static class TypeScale
{
    /// <summary>The minimum edge, in either dimension, of an interactive control's hit target.</summary>
    public const double MinimumTarget = 44;

    /// <summary>A screen title.</summary>
    public const double Title = 31;

    /// <summary>A group heading.</summary>
    public const double Heading = 21;

    /// <summary>A group within a surface: a settings section, a dialog's second rank.</summary>
    public const double Subheading = 17;

    /// <summary>Ordinary text.</summary>
    public const double Body = 16;

    /// <summary>
    /// Supporting text that is genuinely subordinate: a row's help line, the provenance line under the
    /// transcript.
    /// </summary>
    public const double Secondary = 15;

    /// <summary>A tooltip's own prose (#381).</summary>
    public const double Tip = 14;

    /// <summary>A badge, or a count beside something else.</summary>
    public const double Small = 13;

    /// <summary>Mono text about a control rather than in it: a stepper's position and its cost.</summary>
    public const double Meta = 12;

    /// <summary>The smallest d47 will draw: unit labels, machine captions.</summary>
    public const double Caption = 11;
}
