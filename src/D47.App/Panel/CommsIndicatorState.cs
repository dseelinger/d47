using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>What the comms indicator says: the channel and who is on it, upper-case. A null <see cref="Channel"/> is "no channel".</summary>
public sealed record CommsIndicatorState(string? Channel, string? Who, string? Note = null)
{
    public static CommsIndicatorState NoChannel { get; } = new(null, null);

    public bool IsNoChannel => Channel is null;

    public bool IsNoOne => Channel is not null && Who is null;

    /// <summary>Reads the selection; "no channel" when none is available. A target whose pilot is not yet scanned carries the note UNSCANNED.</summary>
    public static CommsIndicatorState Of(CommsChannels channels, TargetedShip? target)
    {
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Available.Count == 0 || channels.Selected is not { } selected)
        {
            return NoChannel;
        }

        var unscanned = selected.Channel == CommsChannel.Target && selected.SubChannel is not null && target is { IsNameable: false };

        return new CommsIndicatorState(
            selected.Channel.ToString().ToUpperInvariant(),
            selected.SubChannel?.Name.ToUpperInvariant(),
            unscanned ? "UNSCANNED" : null);
    }
}
