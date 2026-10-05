using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The active hired pilot says so when the fighter launches, docks, is lost or is rebuilt.</summary>
public sealed class FighterCallout : ICallout
{
    public string Id => "fighter";

    public const string Key = "fighter";

    private static readonly string[] Launched =
    [
        "Fighter away. I'll stay on your wing.",
        "Fighter's out. I have it.",
        "Launching. I'm flying the fighter.",
    ];

    private static readonly string[] TookTheShip =
    [
        "I have the ship, Commander.",
        "The ship is mine, Commander.",
        "I'm flying the ship, Commander.",
    ];

    private static readonly string[] Docked =
    [
        "Fighter's back in the bay.",
        "Fighter docked.",
        "Fighter is aboard.",
    ];

    private static readonly string[] Lost =
    [
        "We've lost the fighter.",
        "The fighter is destroyed.",
        "Fighter's gone, Commander.",
    ];

    private static readonly string[] Rebuilt =
    [
        "Replacement fighter's ready in the bay.",
        "A new fighter is in the bay.",
        "The bay has a fresh fighter.",
    ];

    private int _picks;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State?.Crew.Active is not { } pilot)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            var lines = journalEvent.Kind switch
            {
                "LaunchFighter" => journalEvent.Bool("PlayerControlled") ? TookTheShip : Launched,
                "DockFighter" => Docked,
                "FighterDestroyed" => Lost,
                "FighterRebuilt" => Rebuilt,
                _ => null,
            };

            if (lines is null)
            {
                continue;
            }

            var variant = _picks++;

            yield return new Announcement(Key, lines[variant % lines.Length])
            {
                Voice = VoiceRole.Crew,
                Speaker = pilot.Name,
                Variant = variant,
            };
        }
    }
}
