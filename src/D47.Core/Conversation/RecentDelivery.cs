using D47.Core.Audio;

namespace D47.Core.Conversation;

/// <summary>The delivery words each speaker used lately, newest last. Safe to call from any thread; not persisted.</summary>
public sealed class RecentDelivery
{
    /// <summary>Distinct words kept per speaker.</summary>
    public const int Kept = 12;

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal)
    {
        "a", "an", "almost", "half", "to", "himself", "herself", "myself", "yourself",
        "slightly", "very", "voice", "tone",
    };

    private readonly Dictionary<string, List<string>> _words = new(StringComparer.Ordinal);

    /// <summary>Remembers the words in the delivery notes of a line this speaker said.</summary>
    public void Record(string speaker, string line)
    {
        var used = Words(AudioTags.In(line));

        if (used.Count == 0)
        {
            return;
        }

        lock (_words)
        {
            if (!_words.TryGetValue(speaker, out var list))
            {
                _words[speaker] = list = [];
            }

            foreach (var word in used)
            {
                list.Remove(word);
                list.Add(word);
            }

            if (list.Count > Kept)
            {
                list.RemoveRange(0, list.Count - Kept);
            }
        }
    }

    /// <summary>What to tell the model about this speaker's recent delivery words, or null when there are none.</summary>
    public string? Describe(string speaker)
    {
        lock (_words)
        {
            return _words.TryGetValue(speaker, out var list) && list.Count > 0
                ? $"Delivery words this speaker used recently: {string.Join(", ", list)}. Use none of them in a delivery note this time."
                : null;
        }
    }

    private static List<string> Words(IReadOnlyList<string> notes)
    {
        var words = new List<string>();

        foreach (var note in notes)
        {
            foreach (var raw in note.ToLowerInvariant().Split([' ', ',', '-'], StringSplitOptions.RemoveEmptyEntries))
            {
                var word = raw.Length >= 5 && raw.EndsWith("ly", StringComparison.Ordinal) ? raw[..^2] : raw;

                if (!Skipped.Contains(raw) && !Skipped.Contains(word) && !words.Contains(word))
                {
                    words.Add(word);
                }
            }
        }

        return words;
    }
}
