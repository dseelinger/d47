using System.Text.Json;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>
/// The 274,927-word pronunciation dictionary, which is the top rung of the ladder.
/// <para>
/// <b>Its entries are exact and the rules below it are not</b>, so this is what makes ordinary
/// English come out right: <em>through</em>, <em>one</em> and <em>colonel</em> are not recoverable
/// from their spelling by any rule anybody would want to write.
/// </para>
/// <para>
/// <b>Only the dictionary is taken from that repository.</b> It also ships a 61 MB neural
/// grapheme-to-phoneme model, and the spike measured it at <b>0.0% exact</b> on words drawn from
/// its own training set — <c>station</c> came back as <c>stetɔn</c>. <see cref="Phonemiser"/>'s
/// rules replace it, so that file is never downloaded.
/// </para>
/// </summary>
public sealed class PhonemeDictionary : IPronunciationDictionary
{
    private readonly Dictionary<string, string> _words;

    private PhonemeDictionary(Dictionary<string, string> words) => _words = words;

    public string? Lookup(string word) => _words.GetValueOrDefault(word);

    /// <summary>
    /// Reads the file, or answers a dictionary that knows nothing.
    /// <para>
    /// <b>A missing or unreadable file is not a failure to speak.</b> The ladder's lower rungs
    /// still work, so the voice degrades to rules rather than going silent — which is the right way
    /// round, because the alternative is a Commander whose speech stops because a 10 MB file was
    /// corrupted.
    /// </para>
    /// </summary>
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
