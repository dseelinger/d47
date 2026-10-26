using D47.Core.Capabilities.Builtin;

namespace D47.Core.Conversation;

/// <summary>Moves the comms selection on every step and says it once, after steps stop for <see cref="Quiet"/>.</summary>
public sealed class CommsStepSpeaker(CommsChannels? channels, Action<string> say)
{
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(400);

    private DateTimeOffset? _lastStep;

    public bool Waiting => _lastStep is not null;

    public void Step(string channel, string? subChannel, DateTimeOffset now)
    {
        var result = CommsCapability.Choose(channels, channel, subChannel);

        if (result.IsError || channels?.Selected is null)
        {
            _lastStep = null;
            say(result.Spoken);
            return;
        }

        _lastStep = now;
    }

    public void Poll(DateTimeOffset now)
    {
        if (_lastStep is not { } last || now - last < Quiet)
        {
            return;
        }

        _lastStep = null;

        if (channels?.Selected is { } selected)
        {
            say(CommsCapability.Say(selected));
        }
    }
}
