using D47.Core.Journal;

namespace D47.Core.Memory;

/// <summary>
/// The one thing in Phase 31 that writes a memory nobody asked for (Phase 31, "the
/// Commander told me this, d47 observed it in the journal, or d47 inferred it").
/// <para>
/// <b>It exists because the <see cref="MemoryTier.Observed"/> tier would otherwise be an enum
/// member reachable by nothing</b>, which is a claim the code does not support — the failure
/// <see cref="Lore.LoreArrival"/>'s own comment refuses to ship, arriving by a different road.
/// </para>
/// <para>
/// <b>Two subjects, and they are deliberately dull.</b> Where the Commander was, and what they were
/// flying. Both are read straight off the folded game state, both are what item 4's first clause
/// needs, and neither is a pattern, a habit or a judgement — <em>that</em> is Phase 32, which is a
/// batch job over 912 journals and is a different phase on purpose.
/// </para>
/// <para>
/// <b>Keyed per subject and replaced, never appended.</b> A row per session would grow the file by
/// a line every time the Commander undocked, and the file is one a person is meant to be able to
/// open and read. Two entries is the whole of it, forever.
/// </para>
/// <para>
/// Owns no thread and reads no clock: <see cref="Observe"/> is called by the tick loop with the
/// tick's own time, so the replay harness drives it at 100x exactly as it drives everything else.
/// It also writes nothing while the journal backlog is being folded — an observation made from a
/// half-folded state would be a fact about the middle of last Tuesday.
/// </para>
/// </summary>
public sealed class MemoryObserver(MemoryBook book)
{
    /// <summary>The prefix every observed key carries. Not a format — the keys themselves are below.</summary>
    public const string KeyPrefix = "seen";

    /// <summary>Where the Commander was, and when. What "it has been three days" is measured from.</summary>
    public const string WhereKey = KeyPrefix + "-where";

    /// <summary>What they were flying.</summary>
    public const string ShipKey = KeyPrefix + "-ship";

    /// <summary>Both keys, for a caller that needs to tell an observation from a remembered fact.</summary>
    public static IReadOnlyList<string> Keys { get; } = [WhereKey, ShipKey];

    /// <summary>Whether a key is one of this observer's.</summary>
    public static bool IsObservation(string key) =>
        key.StartsWith(KeyPrefix + "-", StringComparison.Ordinal);

    /// <summary>
    /// Records what is currently true, if it has changed. Returns how many entries it wrote, which
    /// is zero on almost every tick.
    /// </summary>
    /// <param name="state">The folded state, or null before the journal has said anything.</param>
    /// <param name="now">
    /// The tick's time, used only where the journal has reported no instant of its own. The journal's
    /// own stamp is preferred: "you were last aboard three days ago" has to mean three days since the
    /// game said something, not three days since d47 happened to be running.
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
    /// measured from the last moment d47 saw the Commander rather than from the last time they
    /// moved. A Commander who spends a four-hour session parked in one system has not been away for
    /// four hours.
    /// </summary>
    public void Touch(CommanderGameState? state, DateTimeOffset now)
    {
        if (state is null || Existing(WhereKey) is not { } existing)
        {
            return;
        }

        var at = state.Session.LastEventAt ?? now;

        // Only forward, and only when it is worth a write. A stamp that moved every tick would
        // rewrite the file ten times a second.
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

        // The station where there is one, because "docked at Farseer Inc" is where a Commander
        // left off in a way "in Deciat" is not.
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
