using NAudio.CoreAudioApi;

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
}

/// <summary>The real enumerator, backed by the Windows MMDevice API.</summary>
public sealed class WasapiEndpointEnumerator : IAudioEndpointEnumerator
{
    public IReadOnlyList<AudioEndpoint> Active(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            return
            [
                .. enumerator
                    .EnumerateAudioEndPoints(flow, DeviceState.Active)
                    .Select(device => new AudioEndpoint(device.ID, device.FriendlyName)),
            ];
        }
        catch (Exception)
        {
            // A machine with no audio subsystem at all.
            return [];
        }
    }

    public AudioEndpoint? Default(DataFlow flow, Role role)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(flow, role);

            return new AudioEndpoint(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
