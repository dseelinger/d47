using D47.Core.Journal;

namespace D47.Core.Memory;

/// <summary>
/// The one thing in Phase 31 that writes a memory nobody asked for (Phase 31, "the Commander told me
/// this, d47 observed it in the journal, or d47 inferred it").
/// </summary>
public sealed class MemoryObserver(MemoryBook book)
{
    /// <summary>The prefix every observed key carries.</summary>
    public const string KeyPrefix = "seen";

    /// <summary>Where the Commander was, and when.</summary>
    public const string WhereKey = KeyPrefix + "-where";

    /// <summary>What they were flying.</summary>
    public const string ShipKey = KeyPrefix + "-ship";

    /// <summary>Both keys, for a caller that needs to tell an observation from a remembered fact.</summary>
    public static IReadOnlyList<string> Keys { get; } = [WhereKey, ShipKey];

    /// <summary>Whether a key is one of this observer's.</summary>
    public static bool IsObservation(string key) =>
        key.StartsWith(KeyPrefix + "-", StringComparison.Ordinal);

    /// <summary>Records what is currently true, if it has changed.</summary>
    /// <param name="state">The folded state, or null before the journal has said anything.</param>
    /// <param name="now">
    /// The tick's time, used only where the journal has reported no instant of its own.
    /// </param>
    public int Observe(CommanderGameState? state, DateTimeOffset now)
    {
        if (state is null)
        {
            return 0;
        }

        var at = state.Session.LastEventAt ?? now;
        var wrote = 0;

        if (Where(state) is { } where && Changed(WhereKey, where))
        {
            book.Observe(WhereKey, where, at);
            wrote++;
        }

        if (Ship(state) is { } ship && Changed(ShipKey, ship))
        {
            book.Observe(ShipKey, ship, at);
            wrote++;
        }

        return wrote;
    }

    /// <summary>
    /// Bumps the stamp on the location observation without changing what it says, so time away is
    /// measured from the last moment d47 saw the Commander rather than from the last time they moved.
    /// </summary>
    public void Touch(CommanderGameState? state, DateTimeOffset now)
    {
        if (state is null || Existing(WhereKey) is not { } existing)
        {
            return;
        }

        var at = state.Session.LastEventAt ?? now;

        // Only forward, and only when it is worth a write.
        if (existing.AddedAt is { } was && at - was < TimeSpan.FromMinutes(1))
        {
            return;
        }

        book.Observe(WhereKey, existing.Fact, at);
    }

    private static string? Where(CommanderGameState state)
    {
        if (state.Location.StarSystem is not { Length: > 0 } system)
        {
            return null;
        }

        // The station where there is one, because "docked at Farseer Inc" is where a Commander left off in a
        // way "in Deciat" is not.
        return state.Location.StationName is { Length: > 0 } station
            ? $"you were last aboard in {system}, docked at {station}."
            : $"you were last aboard in {system}.";
    }

    private static string? Ship(CommanderGameState state)
    {
        var hull = state.Ship.TypeName ?? state.Ship.Type;

        if (hull is not { Length: > 0 })
        {
            return null;
        }

        // The name where the Commander gave it one, because that is how they refer to the ship.
        return state.Ship.Name is { Length: > 0 } name
            ? $"you were flying {name}, a {hull}."
            : $"you were flying a {hull}.";
    }

    private MemoryEntry? Existing(string key) =>
        book.Mine.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));

    private bool Changed(string key, string fact) =>
        Existing(key) is not { } existing || !string.Equals(existing.Fact, fact, StringComparison.Ordinal);
}
