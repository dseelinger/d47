using D47.Core.Audio;

namespace D47.Core.Conversation;

/// <summary>The delivery notes each speaker used lately, newest last. Safe to call from any thread; not persisted.</summary>
public sealed class RecentDelivery
{
    /// <summary>Notes kept per speaker.</summary>
    public const int Kept = 6;

    private readonly Dictionary<string, List<string>> _notes = new(StringComparer.Ordinal);

    /// <summary>Remembers the notes in a line this speaker said.</summary>
    public void Record(string speaker, string line)
    {
        var used = AudioTags.In(line);

        if (used.Count == 0)
        {
            return;
        }

        lock (_notes)
        {
            if (!_notes.TryGetValue(speaker, out var list))
            {
                _notes[speaker] = list = [];
            }

            list.AddRange(used);

            if (list.Count > Kept)
            {
                list.RemoveRange(0, list.Count - Kept);
            }
        }
    }

    /// <summary>What to tell the model about this speaker's recent notes, or null when there are none.</summary>
    public string? Describe(string speaker)
    {
        lock (_notes)
        {
            return _notes.TryGetValue(speaker, out var list) && list.Count > 0
                ? $"Delivery notes this speaker used recently: {string.Join(", ", list)}. Choose a different one, or none."
                : null;
        }
    }
}
