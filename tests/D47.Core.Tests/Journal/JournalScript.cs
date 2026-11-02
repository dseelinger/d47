using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Turns journal lines into game state.</summary>
public static class JournalScript
{
    /// <summary>Parses one journal line; a line break inside it counts as a space.</summary>
    public static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A store with each line applied in order.</summary>
    public static GameStateStore Store(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines)
        {
            store.Apply(Parse(line));
        }

        return store;
    }

    /// <summary>The store's active Commander after the lines.</summary>
    public static CommanderGameState State(params string[] lines) => Store(lines).Active!;

    /// <summary>A Commander with the given identity and each line applied in order.</summary>
    public static CommanderGameState Commander(string frontierId, string name, params string[] lines) =>
        Commander(frontierId, name, lines.Select(Parse));

    /// <summary>A Commander with the given identity and each event applied in order.</summary>
    public static CommanderGameState Commander(string frontierId, string name, IEnumerable<JournalEvent> events)
    {
        var state = new CommanderGameState(new CommanderIdentity(frontierId, name));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    /// <summary>A callout context at the epoch over the state, carrying the parsed lines as its events.</summary>
    public static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    /// <summary>An event of the kind at the time, with the fields serialised in order.</summary>
    public static JournalEvent EventOf(string kind, DateTimeOffset at, params (string Key, object? Value)[] fields) =>
        EventOf(kind, at.ToString("yyyy-MM-ddTHH:mm:ssZ"), fields);

    /// <summary>An event of the kind at the timestamp text, with the fields serialised in order.</summary>
    public static JournalEvent EventOf(string kind, string at, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?> { ["timestamp"] = at, ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        return Parse(JsonSerializer.Serialize(payload));
    }
}
