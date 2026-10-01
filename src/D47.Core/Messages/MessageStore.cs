using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Messages;

/// <summary>The Commander's messages on disk — <c>data/messages.json</c>, append-only and capped.</summary>
public sealed class MessageStore(string path, ILogger<MessageStore> logger)
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

    public D47Message Post(string from, string subject, string body, DateTimeOffset sent, string? adventureKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var message = new D47Message
        {
            Key = Guid.NewGuid().ToString("N"),
            Sent = sent,
            From = from,
            Subject = subject,
            Body = body,
            AdventureKey = adventureKey,
        };

        lock (_gate)
        {
            var messages = Loaded();
            messages.Add(message);

            while (messages.Count > Capacity)
            {
                var oldestRead = messages.FindIndex(other => other.Read);
                messages.RemoveAt(oldestRead >= 0 ? oldestRead : 0);
            }

            Write(messages);
        }

        Changed?.Invoke();
        return message;
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
            _messages = File.Exists(path)
                ? JsonSerializer.Deserialize<List<D47Message>>(File.ReadAllText(path), Json) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Could not read {Path}", path);
            _messages = [];
        }

        return _messages;
    }

    private void Write(List<D47Message> messages)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(messages, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }
}
