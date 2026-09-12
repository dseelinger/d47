namespace D47.Audio;

/// <summary>
/// What a device that can follow a moved Default Device exposes to the policy that decides when to act
/// on it. <see cref="WasapiAudioSink"/> and <see cref="WasapiMicrophone"/> both already have every member
/// this asks for.
/// </summary>
public interface IDefaultDeviceReopener
{
    /// <summary>The friendly name of the device currently open.</summary>
    string? OpenDeviceName { get; }

    bool DefaultDeviceMoved(DateTimeOffset now);

    void AcknowledgeDefaultDeviceMove();

    bool OpenDeviceIsGone();

    void Reopen(string? deviceId);
}

/// <summary>
/// Decides when a settled Default Device move is actually followed: never for a device the Commander
/// chose by name, straight away when the device that was open has itself disappeared, and otherwise only
/// once whatever is using it goes idle — a line mid-playback or a turn mid-utterance is worse to lose than
/// to finish on a device Windows no longer calls the default (#67).
/// </summary>
public static class DefaultDeviceFollowPolicy
{
    /// <summary>One reopen carried out, for logging and for deciding whether to say anything about it.</summary>
    /// <param name="Interrupted">Whether it cut off whatever was using the device, because that device was gone.</param>
    public readonly record struct Move(bool Interrupted, string? OldDeviceName, string? NewDeviceName);

    /// <summary>Call once per tick. Null when nothing was due, or when a chosen device is not affected.</summary>
    public static Move? Poll(
        IDefaultDeviceReopener device,
        DateTimeOffset now,
        bool followingDefault,
        string? configuredDeviceId,
        Func<bool> isBusy,
        Action interrupt)
    {
        if (!device.DefaultDeviceMoved(now))
        {
            return null;
        }

        if (!followingDefault)
        {
            // A chosen device stays chosen.
            device.AcknowledgeDefaultDeviceMove();
            return null;
        }

        var gone = device.OpenDeviceIsGone();

        if (isBusy() && !gone)
        {
            // Try again next tick rather than cut off what is running now.
            return null;
        }

        var was = device.OpenDeviceName;
        device.AcknowledgeDefaultDeviceMove();

        if (gone)
        {
            interrupt();
        }

        device.Reopen(configuredDeviceId);

        return new Move(gone, was, device.OpenDeviceName);
    }
}
