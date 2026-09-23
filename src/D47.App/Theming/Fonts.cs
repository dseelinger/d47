namespace D47.App.Theming;

/// <summary>The three embedded typefaces: Saira for chrome, Sintony for prose, JetBrains Mono for
/// machine text.</summary>
public static class Fonts
{
    /// <summary>Saira, with Regular, Medium, SemiBold and Bold faces chosen by <c>FontWeight</c>.</summary>
    public const string ChromeFamily = "avares://d47/Assets/Fonts#Saira";

    /// <summary>Sintony, Regular and Bold. It has no italic face, so italic prose is a synthesised slant.</summary>
    public const string ProseFamily = "avares://d47/Assets/Fonts#Sintony";

    public const string MonoFamily =
        "avares://d47/Assets/Fonts/JetBrainsMono-Regular.ttf#JetBrains Mono";

    /// <summary>Letter-spacing on upper-case chrome, as a fraction of its font size. Names, values and
    /// prose take none.</summary>
    public const double ChromeTracking = 0.06;
}
