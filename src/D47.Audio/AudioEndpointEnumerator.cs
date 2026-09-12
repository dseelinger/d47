using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace D47.Audio;

/// <summary>An audio endpoint's id and name, decoupled from NAudio's <see cref="MMDevice"/> for testing.</summary>
public readonly record struct AudioEndpoint(string Id, string Name);

/// <summary>
/// Resolves audio endpoints by id and by Windows role, without exposing NAudio's own device type, so
/// device selection can be exercised against a stub.
/// </summary>
public interface IAudioEndpointEnumerator
{
    /// <summary>The active endpoints for one direction, for the settings picker.</summary>
    IReadOnlyList<AudioEndpoint> Active(DataFlow flow);

    /// <summary>What Windows currently resolves one role to, or null when that cannot be read.</summary>
    AudioEndpoint? Default(DataFlow flow, Role role);

    /// <summary>Raised on a COM thread when Windows moves a default endpoint (#67).</summary>
    event Action<DataFlow, Role>? DefaultDeviceChanged;
}

/// <summary>
/// The real enumerator, backed by the Windows MMDevice API. Holds one live <see cref="MMDeviceEnumerator"/>
/// for the process lifetime so it can stay registered for default-endpoint notifications (#67).
/// </summary>
public sealed class WasapiEndpointEnumerator : IAudioEndpointEnumerator, IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator? _enumerator;
    private bool _disposed;

    public WasapiEndpointEnumerator()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _enumerator.RegisterEndpointNotificationCallback(this);
        }
        catch (Exception)
        {
            // A machine with no audio subsystem at all.
            _enumerator = null;
        }
    }

    /// <inheritdoc />
    public event Action<DataFlow, Role>? DefaultDeviceChanged;

    public IReadOnlyList<AudioEndpoint> Active(DataFlow flow)
    {
        if (_enumerator is null)
        {
            return [];
        }

        try
        {
            return
            [
                .. _enumerator
                    .EnumerateAudioEndPoints(flow, DeviceState.Active)
                    .Select(device => new AudioEndpoint(device.ID, device.FriendlyName)),
            ];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public AudioEndpoint? Default(DataFlow flow, Role role)
    {
        if (_enumerator is null)
        {
            return null;
        }

        try
        {
            var device = _enumerator.GetDefaultAudioEndpoint(flow, role);
            return new AudioEndpoint(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
    }

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
    {
    }

    void IMMNotificationClient.OnDeviceRemoved(string deviceId)
    {
    }

    // A COM thread. Only marks that something changed; the tick loop does the work (#67).
    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) =>
        DefaultDeviceChanged?.Invoke(flow, role);

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _enumerator?.UnregisterEndpointNotificationCallback(this);
        }
        catch (Exception)
        {
            // Shutting down anyway.
        }

        _enumerator?.Dispose();
    }
}
