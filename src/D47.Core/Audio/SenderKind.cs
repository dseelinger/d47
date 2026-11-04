namespace D47.Core.Audio;

/// <summary>What an NPC sender is, which decides how long a voice cast for it lasts.</summary>
public enum SenderKind
{
    /// <summary>A named NPC, a liner or any other ship: one voice from arrival in a system until the next arrival.</summary>
    Person,

    /// <summary>A station, settlement or another Commander's carrier: one voice from a drop into normal space until it is left.</summary>
    Station,

    /// <summary>An authority or navy ship, with more than one person aboard: a voice for each message.</summary>
    GroupShip,
}
