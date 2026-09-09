using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>
/// Which of the five situations the Commander is in, from what Status.json actually says (Phase 12,
/// "Ambient music").
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

    /// <summary>A pure function of the state Elite last wrote — no clock, no thread, no history.</summary>
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

        // Landed counts as normal space rather than as docked: a rock is not a station, and it is certainly
        // not supercruise.
        return status.Has(StatusFlags.InMainShip)
               || status.Has(StatusFlags.InFighter)
               || status.Has(StatusFlags.InSrv)
            ? NormalSpace
            : General;
    }
}

/// <summary>
/// The ambience layer: which situation is playing and which track comes next (Phase 12, "Ambient
/// music").
/// </summary>
public sealed class Ambience(Random? shuffle = null)
{
    private readonly Random _shuffle = shuffle ?? Random.Shared;

    private readonly List<AudioClip> _order = [];

    /// <summary>Null until the first <see cref="Enter"/>, so the first one is always a change.</summary>
    private string? _situation;

    private int _next;

    /// <summary>What is being played for, which is not always what the Commander is doing.</summary>
    public string Situation => _situation ?? Situations.General;

    /// <summary>Where the tracks are coming from.</summary>
    public string Playing { get; private set; } = Situations.General;

    /// <summary>Points this at a situation, and says whether that is a change.</summary>
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

    /// <summary>The next clip, or null when there is nothing to play.</summary>
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

        // Fisher-Yates, in place.
        for (var i = _order.Count - 1; i > 0; i--)
        {
            var j = _shuffle.Next(i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
    }
}
