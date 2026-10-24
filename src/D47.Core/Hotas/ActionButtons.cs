using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.Core.Hotas;

/// <summary>The stick buttons other than push-to-talk and cancel, polled together.</summary>
public sealed class ActionButtons
{
    private static readonly HashSet<string> Elsewhere =
        [ListeningCapability.PushToTalkButtonKey, ListeningCapability.CancelButtonKey];

    private readonly Lock _binding = new();

    private Slot[] _bound = [];

    /// <summary>The slots this poller reads.</summary>
    public static IReadOnlyList<ButtonSlot> Slots { get; } =
        [.. ButtonSlots.All.Where(slot => !Elsewhere.Contains(slot.Key))];

    /// <summary>The slots whose action needs the D47 window to have focus, as its key does.</summary>
    public static IReadOnlySet<string> WindowOnly { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        InterfaceCapability.OpenSettingsButtonKey,
        InterfaceCapability.FocusAskButtonKey,
        InterfaceCapability.FindButtonKey,
    };

    /// <summary>Raised on the poll that first sees a bound button down, with the slot's settings key.</summary>
    public event Action<string>? Pressed;

    /// <summary>Whether a press of the slot <paramref name="key"/> acts, given whether the D47 window is active.</summary>
    public static bool Acts(string key, bool windowActive) => windowActive || !WindowOnly.Contains(key);

    /// <summary>
    /// Rebinds every slot whose stored button differs from the settings; a slot left as it was keeps its
    /// held state. Callable from any thread.
    /// </summary>
    public void Bind(D47Settings settings)
    {
        lock (_binding)
        {
            var next = new List<Slot>();

            foreach (var slot in Slots)
            {
                var key = slot.Key;
                var stored = slot.Read(settings);

                if (_bound.FirstOrDefault(bound => bound.Key == key) is { } kept && kept.Stored == stored)
                {
                    next.Add(kept);
                    continue;
                }

                var button = new BoundButton();

                button.Bind(HotasButton.Parse(stored));
                button.Pressed += () => Pressed?.Invoke(key);
                next.Add(new Slot(key, stored, button));
            }

            _bound = [.. next];
        }
    }

    /// <summary>Samples the controllers; tick thread only.</summary>
    public void Poll(IReadOnlyList<HotasReading> readings)
    {
        foreach (var slot in _bound)
        {
            slot.Button.Poll(readings);
        }
    }

    private sealed record Slot(string Key, string? Stored, BoundButton Button);
}
