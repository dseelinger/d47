using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>
/// Which of the five situations the Commander is in, from what Status.json actually says
/// (list.md Phase 12, "Ambient music").
/// <para>
/// Five, and no more, because these are the five that file can state without guessing. There is
/// no "in combat" folder and no "exploring" folder: neither is a thing Elite reports, and a
/// situation d47 has to infer is a situation that plays the wrong music at the worst moment.
/// </para>
/// <para>
/// The names are the folder names under <c>data/audio/music/</c>. That is the whole binding —
/// there is no table mapping a situation to a directory, because a table is a second place for
/// a name to be wrong.
/// </para>
/// </summary>
public static class Situations
{
    public const string General = "general";
    public const string Docked = "docked";
    public const string Supercruise = "supercruise";
    public const string NormalSpace = "normal-space";
    public const string OnFoot = "on-foot";

    /// <summary>All five, in the order the documentation lists them.</summary>
    public static readonly IReadOnlyList<string> All =
        [General, Docked, Supercruise, NormalSpace, OnFoot];

    /// <summary>
    /// A pure function of the state Elite last wrote — no clock, no thread, no history.
    /// <para>
    /// On foot is asked first because it is the one question the first bitfield cannot answer: it
    /// keeps reporting <see cref="StatusFlags.InMainShip"/> for a Commander who has got out.
    /// After that it is docked, then supercruise, then "in something, in normal space". Anything
    /// else — the main menu, a state d47 has never seen, Status.json not written yet — is
    /// general, which is also what a Commander who put files in only that folder gets.
    /// </para>
    /// </summary>
    public static string For(GameStatus status)
    {
        if (!status.IsKnown)
        {
            return General;
        }

        if (status.OnFoot)
        {
            return OnFoot;
        }

        if (status.Has(StatusFlags.Docked))
        {
            return Docked;
        }

        if (status.Has(StatusFlags.Supercruise))
        {
            return Supercruise;
        }

        // Landed counts as normal space rather than as docked: a rock is not a station, and it is
        // certainly not supercruise. In a fighter or an SRV is the same answer for the same reason.
        return status.Has(StatusFlags.InMainShip)
               || status.Has(StatusFlags.InFighter)
               || status.Has(StatusFlags.InSrv)
            ? NormalSpace
            : General;
    }
}

/// <summary>
/// The ambience layer: which situation is playing and which track comes next
/// (list.md Phase 12, "Ambient music").
/// <para>
/// Owns no thread and reads no clock. It is told the situation and told when a track finished,
/// and answers with the next clip — so the tick loop drives it in production and a test drives
/// it directly, which is the same arrangement every other reader here uses (architecture.md §8).
/// </para>
/// <para>
/// Shuffled within the folder rather than played in name order, because a Commander who drops in
/// eight tracks did not number them and would otherwise hear the first one every time they
/// docked. Reshuffled when the order runs out, so a long session does not become a loop of one.
/// </para>
/// </summary>
public sealed class Ambience(Random? shuffle = null)
{
    private readonly Random _shuffle = shuffle ?? Random.Shared;

    private readonly List<AudioClip> _order = [];

    /// <summary>
    /// Null until the first <see cref="Enter"/>, so the first one is always a change. Started at
    /// general instead, the ambience would never begin: the Commander's first tick reports
    /// general too, and "no change" would be the answer forever.
    /// </summary>
    private string? _situation;

    private int _next;

    /// <summary>What is being played for, which is not always what the Commander is doing.</summary>
    public string Situation => _situation ?? Situations.General;

    /// <summary>
    /// Where the tracks are coming from. Not always <see cref="Situation"/>: a situation with no
    /// files falls back to general, and this says so rather than leaving the fallback invisible.
    /// </summary>
    public string Playing { get; private set; } = Situations.General;

    /// <summary>
    /// Points this at a situation, and says whether that is a change. False means the caller has
    /// nothing to do — which is the answer on almost every tick.
    /// </summary>
    public bool Enter(string situation)
    {
        if (string.Equals(_situation, situation, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _situation = situation;
        _order.Clear();
        _next = 0;

        return true;
    }

    /// <summary>
    /// The next clip, or null when there is nothing to play.
    /// <para>
    /// Null is silence rather than an error, and that is the normal case: d47 ships with no music
    /// at all, so every Commander who has not dropped any in is here. A situation with no files
    /// falls back to general, and general with no files is quiet.
    /// </para>
    /// </summary>
    public AudioClip? Next(CueLibrary library)
    {
        if (_order.Count == 0 || _next >= _order.Count)
        {
            Fill(library);
        }

        return _order.Count == 0 ? null : _order[_next++];
    }

    private void Fill(CueLibrary library)
    {
        var tracks = library.Music(Situation);
        Playing = Situation;

        if (tracks.Count == 0 && !string.Equals(Situation, Situations.General, StringComparison.Ordinal))
        {
            tracks = library.Music(Situations.General);
            Playing = Situations.General;
        }

        _order.Clear();
        _order.AddRange(tracks);
        _next = 0;

        // Fisher-Yates, in place. Shuffling a copy and keeping the folder's order around would be
        // a second list saying the same thing.
        for (var i = _order.Count - 1; i > 0; i--)
        {
            var j = _shuffle.Next(i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
    }
}
