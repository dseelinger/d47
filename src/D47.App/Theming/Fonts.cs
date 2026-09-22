namespace D47.App.Theming;

/// <summary>The three embedded typefaces: Saira Condensed for chrome, Titillium Web for prose
/// and labels, JetBrains Mono for machine text.</summary>
public static class Fonts
{
    public const string ChromeFamily =
        "avares://d47/Assets/Fonts/SairaCondensed-SemiBold.ttf#Saira Condensed";

    public const string ProseFamily =
        "avares://d47/Assets/Fonts/TitilliumWeb-Regular.ttf#Titillium Web";

    /// <summary>Titillium Web's own italic face, so emphasis in prose is not a synthesised slant.</summary>
    public const string ProseItalicFamily =
        "avares://d47/Assets/Fonts/TitilliumWeb-Italic.ttf#Titillium Web";

    public const string MonoFamily =
        "avares://d47/Assets/Fonts/JetBrainsMono-Regular.ttf#JetBrains Mono";
}
