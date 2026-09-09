using System.Text.Json;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>The 274,927-word pronunciation dictionary, which is the top rung of the ladder.</summary>
public sealed class PhonemeDictionary : IPronunciationDictionary
{
    private readonly Dictionary<string, string> _words;

    private PhonemeDictionary(Dictionary<string, string> words) => _words = words;

    public string? Lookup(string word) => _words.GetValueOrDefault(word);

    /// <summary>Reads the file, or answers a dictionary that knows nothing.</summary>
    public static PhonemeDictionary Read(string path, ILogger logger)
    {
        var words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (document.RootElement.TryGetProperty("en_us", out var english))
            {
                foreach (var entry in english.EnumerateObject())
                {
                    if (entry.Value.GetString() is { Length: > 0 } ipa)
                    {
                        words[entry.Name] = ipa;
                    }
                }
            }

            logger.LogInformation("The pronunciation dictionary holds {Count} words", words.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                ex,
                "The pronunciation dictionary at {Path} could not be read, so names will be worked "
                + "out by rule alone",
                path);
        }

        return new PhonemeDictionary(words);
    }
}
