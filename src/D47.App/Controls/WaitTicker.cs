using Avalonia;

namespace D47.App.Controls;

/// <summary>Beats the wait marks shown under one root, from a host's tick.</summary>
public sealed class WaitTicker(Visual root)
{
    private HashSet<WaitingState> _beaten = [];

    /// <summary>
    /// Beats every mark shown under the root, on any tab; true when one stepped, appeared or went. Returns false
    /// at once when no mark is shown anywhere.
    /// </summary>
    public bool Tick()
    {
        var shown = WaitingState.ShownUnder(root);

        if (shown.Count == 0 && _beaten.Count == 0)
        {
            return false;
        }

        var moved = !_beaten.SetEquals(shown);

        foreach (var mark in shown)
        {
            moved |= mark.Beat();
        }

        _beaten = [.. shown];

        return moved;
    }
}
