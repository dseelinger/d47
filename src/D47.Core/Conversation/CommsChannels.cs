using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Journal;

namespace D47.Core.Conversation;

/// <summary>A comms channel, in cycling order.</summary>
public enum CommsChannel
{
    Carrier,
    Target,
    System,
    Story,
    Crew,
    Characters,
}

/// <summary>One party on a channel. <see cref="Key"/> stays the same while they stay on it; <see cref="Name"/> may change.</summary>
public sealed record CommsSubChannel(string Key, string Name);

/// <summary>The selected channel and who on it; a null <see cref="SubChannel"/> is "no one".</summary>
public sealed record CommsSelection(CommsChannel Channel, CommsSubChannel? SubChannel)
{
    public bool IsNoOne => SubChannel is null;
}

/// <summary>What the channels are built from, read once per update.</summary>
public sealed record CommsInputs
{
    public bool CarrierOwned { get; init; }

    public TargetedShip? Target { get; init; }

    /// <summary>The roster, newest first, as <see cref="NpcsHeardHere.Present"/> gives it.</summary>
    public IReadOnlyList<NpcHeard> Heard { get; init; } = [];

    public bool StoryRunning { get; init; }

    /// <summary>The running story's cast members the Commander has been introduced to.</summary>
    public IReadOnlyList<AdventureSpeaker> StoryCast { get; init; } = [];

    /// <summary>Whether the narrator takes replies on the STORY channel.</summary>
    public bool NarratorAnswers { get; init; }

    public ShipCrew Crew { get; init; } = ShipCrew.Empty;

    public IReadOnlyList<CustomCharacter> Characters { get; init; } = [];
}

/// <summary>
/// The comms channels available now and the one selected. Reads no clock; the caller pushes state through
/// <see cref="Update"/>. Once a channel is selected the selection never moves by itself.
/// </summary>
public sealed class CommsChannels
{
    public const string CaptainKey = "captain";

    public const string TowerKey = "tower";

    public const string TargetKey = "target";

    public const string NarratorKey = ":narrator";

    private static readonly CommsChannel[] Order = Enum.GetValues<CommsChannel>();

    private readonly Lock _lock = new();
    private CommsInputs _inputs = new();
    private CommsChannel? _channel;
    private string? _key;

    /// <summary>Raised, outside any lock, when the selection or the available set changes.</summary>
    public event Action? Changed;

    /// <summary>The selection, or null until a channel has first been available.</summary>
    public CommsSelection? Selected
    {
        get
        {
            lock (_lock)
            {
                return Selection();
            }
        }
    }

    /// <summary>The channels available now, in cycling order.</summary>
    public IReadOnlyList<CommsChannel> Available
    {
        get
        {
            lock (_lock)
            {
                return AvailableNow();
            }
        }
    }

    /// <summary>The parties on <paramref name="channel"/> now; empty when it is unavailable.</summary>
    public IReadOnlyList<CommsSubChannel> SubChannels(CommsChannel channel)
    {
        lock (_lock)
        {
            return PartiesOn(channel);
        }
    }

    /// <summary>Replaces the state the channels are built from.</summary>
    public void Update(CommsInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        Change(() =>
        {
            _inputs = inputs;

            if (_channel is { } channel)
            {
                if (_key is not null && !PartiesOn(channel).Any(party => Same(party.Key, _key)))
                {
                    _key = null;
                }
            }
            else if (AvailableNow() is [var first, ..])
            {
                Enter(first);
            }

            return true;
        });
    }

    /// <summary>Moves to the next available channel, wrapping.</summary>
    public void NextChannel() => Change(() => Step(1));

    /// <summary>Moves to the previous available channel, wrapping.</summary>
    public void PreviousChannel() => Change(() => Step(-1));

    /// <summary>Moves to the next party on the selected channel, wrapping within it.</summary>
    public void NextSubChannel() => Change(() => StepWithin(1));

    /// <summary>Moves to the previous party on the selected channel, wrapping within it.</summary>
    public void PreviousSubChannel() => Change(() => StepWithin(-1));

    /// <summary>
    /// Selects an available channel and, when named, the party whose whole name matches ignoring case, else the first whose
    /// last word is the name given. False, with the selection unchanged, when the channel is unavailable or no party matches.
    /// </summary>
    public bool Select(CommsChannel channel, string? subChannel = null) => Change(() =>
    {
        if (!AvailableNow().Contains(channel))
        {
            return false;
        }

        if (subChannel is null)
        {
            Enter(channel);
            return true;
        }

        if (Match(PartiesOn(channel), subChannel) is not { } party)
        {
            return false;
        }

        _channel = channel;
        _key = party.Key;
        return true;
    });

    /// <summary>Selects the roster NPC who hailed the Commander. False, with the selection unchanged, when they are not on the roster.</summary>
    public bool Hailed(string rosterName) => Change(() =>
    {
        var party = PartiesOn(CommsChannel.System).FirstOrDefault(party => Same(party.Key, rosterName));

        if (party is null)
        {
            return false;
        }

        _channel = CommsChannel.System;
        _key = party.Key;
        return true;
    });

    private T Change<T>(Func<T> change)
    {
        T result;
        bool changed;

        lock (_lock)
        {
            var available = AvailableNow();
            var selected = Selection();

            result = change();

            changed = !available.SequenceEqual(AvailableNow()) || selected != Selection();
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return result;
    }

    private bool Step(int by)
    {
        var available = AvailableNow();

        if (available.Count == 0)
        {
            return false;
        }

        var from = _channel is { } channel ? Array.IndexOf(Order, channel) : (by > 0 ? -1 : 0);

        for (var i = 1; i <= Order.Length; i++)
        {
            var next = Order[((from + (by * i)) % Order.Length + Order.Length) % Order.Length];

            if (available.Contains(next))
            {
                Enter(next);
                return true;
            }
        }

        return false;
    }

    private bool StepWithin(int by)
    {
        if (_channel is not { } channel || PartiesOn(channel) is not { Count: > 0 } parties)
        {
            return false;
        }

        var at = _key is null ? -1 : parties.ToList().FindIndex(party => Same(party.Key, _key));
        var next = at < 0
            ? (by > 0 ? 0 : parties.Count - 1)
            : ((at + by) % parties.Count + parties.Count) % parties.Count;

        _key = parties[next].Key;
        return true;
    }

    private void Enter(CommsChannel channel)
    {
        _channel = channel;
        _key = PartiesOn(channel).FirstOrDefault()?.Key;
    }

    private CommsSelection? Selection()
    {
        if (_channel is not { } channel)
        {
            return null;
        }

        var parties = PartiesOn(channel);

        var party = channel == CommsChannel.Target
            ? parties.FirstOrDefault()
            : parties.FirstOrDefault(party => _key is not null && Same(party.Key, _key));

        return new CommsSelection(channel, party);
    }

    private List<CommsChannel> AvailableNow() => [.. Order.Where(IsAvailable)];

    private bool IsAvailable(CommsChannel channel) => channel switch
    {
        CommsChannel.Carrier => _inputs.CarrierOwned,
        CommsChannel.Target => NpcTarget() is not null,
        CommsChannel.System => _inputs.Heard.Count > 0,
        CommsChannel.Story => _inputs.StoryRunning,
        CommsChannel.Crew => _inputs.Crew.Members.Any(member => member.Active),
        CommsChannel.Characters => _inputs.Characters.Count > 0,
        _ => false,
    };

    private List<CommsSubChannel> PartiesOn(CommsChannel channel)
    {
        if (!IsAvailable(channel))
        {
            return [];
        }

        return channel switch
        {
            CommsChannel.Carrier => [new(CaptainKey, "Captain"), new(TowerKey, "Tower")],
            CommsChannel.Target when NpcTarget() is { } target =>
                [new(TargetKey, target.PilotName is { Length: > 0 } pilot ? pilot : target.Ship ?? "Target")],
            CommsChannel.System => [.. _inputs.Heard.Select(npc => new CommsSubChannel(npc.Name, npc.Name))],
            CommsChannel.Story =>
            [
                .. _inputs.StoryCast.Select(member => new CommsSubChannel(member.Id, member.Name)),
                .. _inputs.NarratorAnswers ? [new CommsSubChannel(NarratorKey, "Narrator")] : (CommsSubChannel[])[],
            ],
            CommsChannel.Crew =>
            [
                .. _inputs.Crew.Members
                    .Where(member => member.Active)
                    .Select(member => new CommsSubChannel(member.CrewId.ToString(CultureInfo.InvariantCulture), member.Name)),
            ],
            CommsChannel.Characters => [.. _inputs.Characters.Select(character => new CommsSubChannel(character.Id, character.Name))],
            _ => [],
        };
    }

    private TargetedShip? NpcTarget() => _inputs.Target is { IsCommander: false } target ? target : null;

    private static CommsSubChannel? Match(List<CommsSubChannel> parties, string name)
    {
        var wanted = name.Trim();

        return parties.FirstOrDefault(party => Same(party.Name, wanted))
            ?? parties.FirstOrDefault(party => Same(LastWord(party.Name), wanted));
    }

    private static string LastWord(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is [.., var last] ? last : name;

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
