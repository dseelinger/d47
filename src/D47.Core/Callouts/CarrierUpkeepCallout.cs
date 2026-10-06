using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The carrier captain, warning that the balance covers under four weeks of upkeep (#835).</summary>
public sealed class CarrierUpkeepCallout : ICallout
{
    public string Id => "carrier-upkeep";

    public const string Key = "carrier.upkeep";

    private const long WeeksWarned = 4;

    /// <summary>Where the Commander's answers to this warning are kept.</summary>
    public StandingWarnings Warnings { get; set; } = new();

    /// <summary>The recorded <c>Balance</c> last spoken for; cleared by a <c>LoadGame</c>.</summary>
    private long? _spokenFor;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var docked = false;
        var requested = false;

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _spokenFor = null;
                    Warnings.NewSession();
                    break;

                case "Docked":
                    docked = true;
                    break;

                case "CarrierJumpRequest":
                    requested = true;
                    break;
            }
        }

        var commander = context.State?.Identity.FrontierId ?? string.Empty;

        if (context.State is { Carrier: { Owned: true, IsSquadron: false, Balance: not null } read }
            && CarrierUpkeep.Now(read, context.Now) is { WeeksCovered: { } covered } && covered >= WeeksWarned)
        {
            _spokenFor = null;
            Warnings.Cleared(commander, Key);
        }

        if (context.IsPriming
            || context.State is not { Carrier: { Owned: true, IsSquadron: false } carrier }
            || !(requested || (docked && carrier.DockedAtOwnCarrier))
            || carrier.Balance is not { } recorded
            || (recorded == _spokenFor && !Warnings.Repeats(commander, Key))
            || Warnings.Silenced(commander, Key)
            || CarrierUpkeep.Now(carrier, context.Now) is not { WeeksCovered: { } weeks, Weekly: { } weekly } balance
            || weeks >= WeeksWarned)
        {
            yield break;
        }

        _spokenFor = recorded;
        Warnings.Fired(commander, Key, "the carrier's upkeep warning", context.Now);

        var name = carrier.Name is { Length: > 0 } called ? called : carrier.CallSign ?? "The carrier";
        var cover = weeks switch
        {
            0 => "less than a week of upkeep",
            1 => "one more week of upkeep",
            2 => "two more weeks of upkeep",
            _ => "three more weeks of upkeep",
        };
        var text = $"{name}'s account covers {cover} at {weekly:N0} a week.";

        if (balance.Adjusted)
        {
            text += " That is the recorded balance less the upkeep since.";
        }

        yield return new Announcement(Key, text)
        {
            Voice = VoiceRole.CarrierCaptain,
            Cooldown = TimeSpan.FromSeconds(30),
        };
    }
}
