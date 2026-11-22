namespace D47.Core.Books;

/// <summary>Display names for the language codes in the Gutenberg catalogue.</summary>
public static class GutenbergLanguages
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["af"] = "Afrikaans",
        ["ar"] = "Arabic",
        ["bg"] = "Bulgarian",
        ["br"] = "Breton",
        ["ca"] = "Catalan",
        ["cs"] = "Czech",
        ["cy"] = "Welsh",
        ["da"] = "Danish",
        ["de"] = "German",
        ["el"] = "Greek",
        ["en"] = "English",
        ["eo"] = "Esperanto",
        ["es"] = "Spanish",
        ["et"] = "Estonian",
        ["fa"] = "Persian",
        ["fi"] = "Finnish",
        ["fr"] = "French",
        ["fy"] = "Frisian",
        ["ga"] = "Irish",
        ["gl"] = "Galician",
        ["he"] = "Hebrew",
        ["hu"] = "Hungarian",
        ["ia"] = "Interlingua",
        ["is"] = "Icelandic",
        ["it"] = "Italian",
        ["ja"] = "Japanese",
        ["la"] = "Latin",
        ["lt"] = "Lithuanian",
        ["nl"] = "Dutch",
        ["no"] = "Norwegian",
        ["oc"] = "Occitan",
        ["pl"] = "Polish",
        ["pt"] = "Portuguese",
        ["ro"] = "Romanian",
        ["ru"] = "Russian",
        ["sa"] = "Sanskrit",
        ["sl"] = "Slovenian",
        ["sr"] = "Serbian",
        ["sv"] = "Swedish",
        ["tl"] = "Tagalog",
        ["zh"] = "Chinese",
    };

    /// <summary>The language's name, or the code in upper case when it is not in the table.</summary>
    public static string NameOf(string code) =>
        Names.TryGetValue(code, out var name) ? name : code.ToUpperInvariant();
}
