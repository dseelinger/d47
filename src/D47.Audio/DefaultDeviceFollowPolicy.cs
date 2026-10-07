using System.Collections.Concurrent;

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
/// to finish on a device Windows no longer calls the default (#67). The reopen runs through
/// <c>run</c>, off the caller's thread, one at a time per device.
/// </summary>
public sealed class DefaultDeviceFollowPolicy(IDefaultDeviceReopener device, Action<Action> run)
{
    private readonly ConcurrentQueue<Move> _finished = new();

    private int _reopening;

    /// <summary>Runs each reopen on the thread pool.</summary>
    public DefaultDeviceFollowPolicy(IDefaultDeviceReopener device)
        : this(device, work => ThreadPool.QueueUserWorkItem(_ => work()))
    {
    }

    /// <summary>One reopen carried out, for logging and for deciding whether to say anything about it.</summary>
    /// <param name="Interrupted">Whether it cut off whatever was using the device, because that device was gone.</param>
    /// <param name="Error">What the reopen threw, when it did.</param>
    public readonly record struct Move(bool Interrupted, string? OldDeviceName, string? NewDeviceName, Exception? Error = null);

    /// <summary>
    /// Call once per tick. Starts a due reopen unless one is still running, and returns a reopen that has
    /// finished since the last call, or null.
    /// </summary>
    public Move? Poll(
        DateTimeOffset now,
        bool followingDefault,
        string? configuredDeviceId,
        Func<bool> isBusy,
        Action interrupt)
    {
        Follow(now, followingDefault, configuredDeviceId, isBusy, interrupt);

        return _finished.TryDequeue(out var move) ? move : null;
    }

    private void Follow(
        DateTimeOffset now,
        bool followingDefault,
        string? configuredDeviceId,
        Func<bool> isBusy,
        Action interrupt)
    {
        if (!device.DefaultDeviceMoved(now))
        {
            return;
        }

        if (!followingDefault)
        {
            // A chosen device stays chosen.
            device.AcknowledgeDefaultDeviceMove();
            return;
        }

        if (Volatile.Read(ref _reopening) != 0)
        {
            // The move stays due and is followed once the reopen in flight has finished.
            return;
        }

        var gone = device.OpenDeviceIsGone();

        if (isBusy() && !gone)
        {
            // Try again next tick rather than cut off what is running now.
            return;
        }

        var was = device.OpenDeviceName;
        device.AcknowledgeDefaultDeviceMove();

        if (gone)
        {
            interrupt();
        }

        Volatile.Write(ref _reopening, 1);

        run(() =>
        {
            try
            {
                device.Reopen(configuredDeviceId);
                _finished.Enqueue(new Move(gone, was, device.OpenDeviceName));
            }
            catch (Exception ex)
            {
                _finished.Enqueue(new Move(gone, was, device.OpenDeviceName, ex));
            }
            finally
            {
                Volatile.Write(ref _reopening, 0);
            }
        });
    }
}
