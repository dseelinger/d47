namespace D47.App.Theming;

/// <summary>The five sizes d47 draws text at, named by the job the text is doing.</summary>
public static class TypeScale
{
    /// <summary>A window or section title.</summary>
    public const double Heading = 20;

    /// <summary>A group within a surface: a settings section, a dialog's second rank.</summary>
    public const double Subheading = 16;

    /// <summary>Ordinary text, and the size every framework control already draws at.</summary>
    public const double Body = 14;

    /// <summary>
    /// Supporting text that is genuinely subordinate: a row's help line, the provenance line under the
    /// transcript.
    /// </summary>
    public const double Secondary = 13;

    /// <summary>The smallest d47 will draw: a badge, a unit, a count beside something else.</summary>
    public const double Small = 12;
}
