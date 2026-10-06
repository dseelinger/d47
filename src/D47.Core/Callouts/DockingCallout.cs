using D47.Core.Knowledge;
using D47.Core.Speech;

namespace D47.Core.Callouts;

/// <summary>Says where the granted pad is in a ring station, as a clock hour and a depth, once the game's own voice line has had time to finish.</summary>
public sealed class DockingCallout : ICallout
{
    public string Id => "docking";

    public const string Key = "docking.pad";

    /// <summary>The time allowed after a grant for the game's own voice line, which d47 cannot hear.</summary>
    public static readonly TimeSpan DockingSettle = TimeSpan.FromSeconds(4);

    /// <summary>How long past its time a held line is still said; a line held while the callout was off is dropped.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    private (string Line, DateTimeOffset Due)? _held;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "DockingGranted":
                    _held = Line(journalEvent.String("StationType"), journalEvent.Int("LandingPad")) is { } line
                        ? (line, context.Now + DockingSettle)
                        : null;
                    break;

                case "Docked" or "DockingCancelled" or "DockingTimeout" or "DockingDenied":
                    _held = null;
                    break;
            }
        }

        if (context.IsPriming)
        {
            _held = null;
            yield break;
        }

        if (_held is not { } held || context.Now < held.Due)
        {
            yield break;
        }

        _held = null;

        if (context.Now - held.Due <= StaleAfter)
        {
            yield return new Announcement(Key, held.Line);
        }
    }

    /// <summary>"Pad ten, eight o'clock, halfway in.", or null for a station without the ring layout or a pad not in the table.</summary>
    public static string? Line(string? stationType, int? number)
    {
        if (!LandingPads.IsRing(stationType) || number is not { } n || LandingPads.Find(n) is not { } pad)
        {
            return null;
        }

        var depth = pad.Depth switch
        {
            <= 1 => "near the entrance",
            2 => "halfway in",
            _ => "at the back",
        };

        return $"Pad {SpokenNumber.UpTo999(pad.Number)}, {SpokenNumber.UpTo999(pad.Hour)} o'clock, {depth}.";
    }
}
