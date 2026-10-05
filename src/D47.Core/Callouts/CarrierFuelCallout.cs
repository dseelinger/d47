using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The carrier captain, warning that the tank holds less than two full jumps (#834).</summary>
public sealed class CarrierFuelCallout : ICallout
{
    public string Id => "carrier-fuel";

    public const string Key = "carrier.fuel";

    /// <summary>The <c>FuelLevel</c> last spoken for; cleared by a <c>LoadGame</c>.</summary>
    private int? _spokenFor;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var docked = false;
        var requested = false;
        DateTimeOffset at = default;

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _spokenFor = null;
                    break;

                case "Docked":
                    docked = true;
                    at = journalEvent.Timestamp;
                    break;

                case "CarrierJumpRequest":
                    requested = true;
                    at = journalEvent.Timestamp;
                    break;
            }
        }

        if (context.IsPriming
            || context.State is not { Carrier: { Owned: true, IsSquadron: false } carrier }
            || !(requested || (docked && carrier.DockedAtOwnCarrier))
            || carrier.FuelLevel is not { } fuel
            || fuel == _spokenFor
            || !CarrierFuel.IsLow(carrier)
            || CarrierFuel.FullJumpCost(carrier) is not { } cost)
        {
            yield break;
        }

        _spokenFor = fuel;

        var name = carrier.Name is { Length: > 0 } called ? called : carrier.CallSign ?? "The carrier";
        var text = $"{name} has {fuel} tonnes of tritium; a full jump at this load burns {Math.Round(cost)}.";

        if (carrier.StatsSeenAt is { } read && carrier.SeenAt is { } moved && moved > read)
        {
            text += $" That is the reading from {Age(at - read)} ago. The tank can only be lower.";
        }

        yield return new Announcement(Key, text)
        {
            Voice = VoiceRole.CarrierCaptain,
            Cooldown = TimeSpan.FromSeconds(30),
        };
    }

    private static string Age(TimeSpan age) => age.TotalHours switch
    {
        < 1 => $"{Math.Max(1, (int)age.TotalMinutes)} minutes",
        < 48 => $"{(int)age.TotalHours} hours",
        _ => $"{(int)age.TotalDays} days",
    };
}
