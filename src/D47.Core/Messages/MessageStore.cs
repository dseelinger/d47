using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Messages;

/// <summary>
/// The Commander's messages on disk — <c>data/messages.json</c>, append-only and capped — with the clips spoken
/// messages keep, deleted with their message.
/// </summary>
public sealed class MessageStore(string path, IFileSystem files, ILogger<MessageStore> logger, MessageClips? clips = null)
{
    public const string Narrator = "narrator";

    /// <summary>The most messages held; past it the oldest read message goes first, then the oldest.</summary>
    public const int Capacity = 200;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private List<D47Message>? _messages;

    public event Action? Changed;

    /// <summary>Every message, newest first.</summary>
    public IReadOnlyList<D47Message> All
    {
        get
        {
            lock (_gate)
            {
                return [.. Loaded().OrderByDescending(message => message.Sent)];
            }
        }
    }

    public int UnreadCount
    {
        get
        {
            lock (_gate)
            {
                return Loaded().Count(message => !message.Read);
            }
        }
    }

    /// <summary>Posts a message, keeping <paramref name="spoken"/> as its clip. Writes files, so never call it on the tick.</summary>
    public D47Message Post(string from, string subject, string body, DateTimeOffset sent, string? adventureKey = null, IReadOnlyList<MessageAnswer>? answers = null, string? picture = null, SpokenClip? spoken = null, string? cast = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var key = Guid.NewGuid().ToString("N");
        var message = new D47Message
        {
            Key = key,
            Sent = sent,
            From = from,
            Subject = subject,
            Body = body,
            AdventureKey = adventureKey,
            Answers = answers ?? [],
            Picture = picture,
            Cast = cast,
        };

        if (spoken is not null && SaveClip(key, spoken) is { } file)
        {
            message = message with { Clip = file, Voice = new MessageVoice(spoken.Provider, spoken.VoiceId) };
        }

        lock (_gate)
        {
            var messages = Loaded();
            messages.Add(message);

            while (messages.Count > Capacity)
            {
                var oldestRead = messages.FindIndex(other => other.Read);
                var at = oldestRead >= 0 ? oldestRead : 0;

                DeleteClip(messages[at]);
                messages.RemoveAt(at);
            }

            Write(messages);
        }

        Changed?.Invoke();
        return message;
    }

    /// <summary>
    /// Keeps <paramref name="spoken"/> as the clip of a message already posted; false when the message has gone.
    /// Writes files, so never call it on the tick.
    /// </summary>
    public bool Attach(string key, SpokenClip spoken)
    {
        ArgumentNullException.ThrowIfNull(spoken);

        if (SaveClip(key, spoken) is not { } file)
        {
            return false;
        }

        lock (_gate)
        {
            var messages = Loaded();
            var index = messages.FindIndex(message => message.Key == key);

            if (index < 0)
            {
                clips?.Delete(file);
                return false;
            }

            if (messages[index].Clip is { } earlier && earlier != file)
            {
                clips?.Delete(earlier);
            }

            messages[index] = messages[index] with { Clip = file, Voice = new MessageVoice(spoken.Provider, spoken.VoiceId) };
            Write(messages);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>The message's clip, read and decrypted into memory, or null when it has none or it cannot be read.</summary>
    public AudioClip? ClipOf(D47Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Clip is { } file ? clips?.Load(file, message.Body) : null;
    }

    /// <summary>
    /// Deletes every clip spoken in the Commander's own voice and keeps those messages' text; returns the count.
    /// </summary>
    public int ForgetOwnVoice() =>
        Forget(message => message.Voice?.Id is not { } id
                          || string.Equals(id, OwnVoice.VoiceId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Deletes every clip spoken in custom voice <paramref name="voiceId"/> and keeps those messages' text.</summary>
    public int ForgetCustomVoice(string voiceId) =>
        Forget(message => string.Equals(message.Voice?.Id, voiceId, StringComparison.Ordinal));

    private int Forget(Func<D47Message, bool> spokenInIt)
    {
        var forgotten = 0;

        lock (_gate)
        {
            var messages = Loaded();

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Clip is { } file && MessageClips.IsProtected(file) && spokenInIt(messages[i]))
                {
                    clips?.Delete(file);
                    messages[i] = messages[i] with { Clip = null, Voice = null };
                    forgotten++;
                }
            }

            if (forgotten > 0)
            {
                Write(messages);
            }
        }

        if (forgotten > 0)
        {
            Changed?.Invoke();
        }

        return forgotten;
    }

    /// <summary>Marks one read; false when there is no such message or it already was.</summary>
    public bool MarkRead(string key)
    {
        lock (_gate)
        {
            var messages = Loaded();
            var index = messages.FindIndex(message => message.Key == key);

            if (index < 0 || messages[index].Read)
            {
                return false;
            }

            messages[index] = messages[index] with { Read = true };
            Write(messages);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes every message keyed to something <paramref name="owned"/> rejects; returns the count.</summary>
    public int RemoveOrphans(Func<string, bool> owned)
    {
        ArgumentNullException.ThrowIfNull(owned);

        int removed;

        lock (_gate)
        {
            var messages = Loaded();

            foreach (var message in messages.Where(message => message.AdventureKey is { } key && !owned(key)))
            {
                DeleteClip(message);
            }

            removed = messages.RemoveAll(message => message.AdventureKey is { } key && !owned(key));

            if (removed > 0)
            {
                Write(messages);
            }
        }

        if (removed > 0)
        {
            Changed?.Invoke();
        }

        return removed;
    }

    private List<D47Message> Loaded()
    {
        if (_messages is not null)
        {
            return _messages;
        }

        try
        {
            _messages = files.ReadText(path) is { } text
                ? JsonSerializer.Deserialize<List<D47Message>>(text, Json) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Could not read {Path}", path);
            _messages = [];
        }

        SweepClips(_messages);
        return _messages;
    }

    private string? SaveClip(string key, SpokenClip spoken)
    {
        if (clips is null || spoken.Parts.Count == 0)
        {
            return null;
        }

        // Read first: the first read sweeps files no message names, which would take this one.
        lock (_gate)
        {
            Loaded();
        }

        try
        {
            return clips.Save(key, spoken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            logger.LogWarning(ex, "Could not keep the clip for message {Key}", key);
            return null;
        }
    }

    private void DeleteClip(D47Message message)
    {
        if (message.Clip is { } file)
        {
            clips?.Delete(file);
        }
    }

    private void SweepClips(List<D47Message> messages) =>
        clips?.Sweep(messages.Select(message => message.Clip).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase));

    private void Write(List<D47Message> messages)
    {
        try
        {
            files.WriteText(path, JsonSerializer.Serialize(messages, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }
}
