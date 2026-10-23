using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.Core.Hotas;

/// <summary>The stick buttons other than push-to-talk and cancel, polled together.</summary>
public sealed class ActionButtons
{
    private static readonly HashSet<string> Elsewhere =
        [ListeningCapability.PushToTalkButtonKey, ListeningCapability.CancelButtonKey];

    private BoundButton[] _bound = [];

    /// <summary>Raised on the poll that first sees a bound button down, with the slot's settings key.</summary>
    public event Action<string>? Pressed;

    /// <summary>Rebinds every slot from the settings; callable from any thread.</summary>
    public void Bind(D47Settings settings)
    {
        var next = new List<BoundButton>();

        foreach (var slot in ButtonSlots.All.Where(slot => !Elsewhere.Contains(slot.Key)))
        {
            var key = slot.Key;
            var button = new BoundButton();

            button.Bind(HotasButton.Parse(slot.Read(settings)));
            button.Pressed += () => Pressed?.Invoke(key);
            next.Add(button);
        }

        _bound = [.. next];
    }

    /// <summary>Samples the controllers; tick thread only.</summary>
    public void Poll(IReadOnlyList<HotasReading> readings)
    {
        foreach (var button in _bound)
        {
            button.Poll(readings);
        }
    }
}
