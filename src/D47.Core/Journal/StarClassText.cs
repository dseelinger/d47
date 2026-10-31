namespace D47.Core.Journal;

/// <summary>A star's class as the body page's CLASS tile shows it.</summary>
/// <param name="Class">"K7 V".</param>
/// <param name="Name">"Orange main-sequence star".</param>
/// <param name="Meaning">What the letter, the number and the luminosity class each say.</param>
public sealed record StarClassLine(string Class, string Name, string Meaning);

/// <summary>d47's own words for a star's spectral and luminosity class: one phrase per class letter and per luminosity class.</summary>
public static class StarClassText
{
    private sealed record Letter(string? Colour, string Name, string Phrase);

    private static readonly Dictionary<string, Letter> Letters = new(StringComparer.Ordinal)
    {
        ["O"] = new("Blue", "Blue star", "blue, the hottest class, over 30,000 K"),
        ["B"] = new("Blue-white", "Blue-white star", "blue-white, 10,000 to 30,000 K"),
        ["A"] = new("White", "White star", "white, 7,500 to 10,000 K"),
        ["F"] = new("Yellow-white", "Yellow-white star", "yellow-white, 6,000 to 7,500 K"),
        ["G"] = new("Yellow", "Yellow star", "yellow, 5,200 to 6,000 K, the Sun's class"),
        ["K"] = new("Orange", "Orange star", "orange, 3,700 to 5,200 K"),
        ["M"] = new("Red", "Red star", "red, 2,400 to 3,700 K, the coolest of the main classes"),
        ["L"] = new(null, "Brown dwarf", "a brown dwarf, 1,300 to 2,400 K"),
        ["T"] = new(null, "Brown dwarf", "a brown dwarf, 550 to 1,300 K"),
        ["Y"] = new(null, "Brown dwarf", "a brown dwarf, below 550 K"),
        ["D"] = new(null, "White dwarf", "a white dwarf, the core left when a star like the Sun dies"),
        ["N"] = new(null, "Neutron star", "a neutron star, the collapsed core of a massive star"),
        ["H"] = new(null, "Black hole", "a black hole"),
        ["W"] = new(null, "Wolf-Rayet star", "a Wolf-Rayet star, a massive star shedding its outer layers"),
        ["C"] = new(null, "Carbon star", "a carbon star, a cool giant rich in carbon"),
        ["S"] = new(null, "S-type star", "a cool giant between class M and the carbon stars"),
        ["MS"] = new(null, "MS-type star", "a cool giant between class M and class S"),
        ["TTS"] = new(null, "T Tauri star", "a young star still contracting"),
        ["AeBe"] = new(null, "Herbig Ae/Be star", "a young star of class A or B mass still contracting"),
    };

    private sealed record Size(string Word, string Phrase);

    private static readonly Size Hypergiant = new("hypergiant", "a hypergiant, the brightest luminosity class");

    private static readonly Dictionary<string, Size> Sizes = new(StringComparer.Ordinal)
    {
        ["I"] = new("supergiant", "a supergiant"),
        ["II"] = new("bright giant", "a bright giant"),
        ["III"] = new("giant", "a giant, swollen after leaving the main sequence"),
        ["IV"] = new("subgiant", "a subgiant, leaving the main sequence"),
        ["V"] = new("main-sequence", "main sequence, burning hydrogen in its core"),
        ["VI"] = new("subdwarf", "a subdwarf, dimmer than the main sequence"),
        ["VII"] = new("white dwarf", "a white dwarf"),
    };

    /// <summary>The line for a star's <paramref name="spectralClass"/> ("K7") and <paramref name="luminosity"/> ("V"); null with no class.</summary>
    public static StarClassLine? For(string? spectralClass, string? luminosity, string? subType = null)
    {
        if (string.IsNullOrWhiteSpace(spectralClass))
        {
            return null;
        }

        var spectral = spectralClass.Trim();
        var lum = luminosity?.Trim() ?? string.Empty;
        var shown = lum.Length > 0 ? $"{spectral} {lum}" : spectral;

        var letters = new string([.. spectral.TakeWhile(char.IsLetter)]);
        var digits = new string([.. spectral[letters.Length..].TakeWhile(char.IsDigit)]);
        var key = Letters.ContainsKey(letters) ? letters : letters.Length > 0 && Letters.ContainsKey(letters[..1]) ? letters[..1] : null;
        var size = SizeOf(lum);

        if (key is null)
        {
            var unknown = size is null ? string.Empty : $"{lum}: {size.Phrase}.";

            return new StarClassLine(shown, subType ?? "Star", unknown);
        }

        var letter = Letters[key];
        var name = letter.Colour is { } colour
            ? size is null ? letter.Name : $"{colour} {size.Word} star"
            : letter.Name;

        var parts = new List<string> { $"{letters}: {letter.Phrase}." };

        if (digits.Length > 0)
        {
            parts.Add($"{digits}: from 0, hottest, to 9, coolest, within {letters}.");
        }

        if (size is not null)
        {
            parts.Add($"{lum}: {size.Phrase}.");
        }

        return new StarClassLine(shown, name, string.Join(' ', parts));
    }

    /// <summary>The luminosity class for "V", "Vab", "Iab", "IV" or "Ia0"; null for anything else.</summary>
    private static Size? SizeOf(string luminosity)
    {
        if (luminosity.Length == 0)
        {
            return null;
        }

        if (luminosity == "0" || (luminosity.StartsWith('I') && luminosity.Contains('0', StringComparison.Ordinal)))
        {
            return Hypergiant;
        }

        var roman = new string([.. luminosity.TakeWhile(c => c is 'I' or 'V')]);

        return Sizes.GetValueOrDefault(roman);
    }
}
