using System.Text.Json;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Turns journal lines into game state.</summary>
internal static class JournalScript
{
    /// <summary>Parses one journal line; a line break inside it counts as a space.</summary>
    public static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>The store's active Commander after each line is applied in order.</summary>
    public static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines)
        {
            store.Apply(Parse(line));
        }

        return store.Active!;
    }

    /// <summary>A Commander with the given identity and each line applied in order.</summary>
    public static CommanderGameState Commander(string frontierId, string name, params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity(frontierId, name));

        foreach (var line in lines)
        {
            state.Apply(Parse(line));
        }

        return state;
    }

    /// <summary>A Commander with each line applied at its offset from the start.</summary>
    public static CommanderGameState Timed(
        DateTimeOffset start, string frontierId, string name, params (TimeSpan At, string Json)[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity(frontierId, name));

        foreach (var (at, json) in lines)
        {
            var root = JsonDocument.Parse(json).RootElement;
            state.Apply(new JournalEvent(start + at, root.GetProperty("event").GetString()!, root));
        }

        return state;
    }
}
