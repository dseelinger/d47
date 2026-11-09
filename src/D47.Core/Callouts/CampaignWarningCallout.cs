using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Says when the Commander's work helps a rival of the campaign faction or hurts the faction, once per kind per session.</summary>
public sealed class CampaignWarningCallout : ICallout
{
    public string Id => "campaign-warnings";

    public const string KeyPrefix = "campaign.warning.";

    /// <summary>The campaign faction of the shown Commander, or null.</summary>
    public Func<string?> Faction { get; set; } = () => null;

    private enum Kind
    {
        RivalBounty,
        MissionFailed,
        MissionAgainst,
        Murder,
        CarrierSale,
    }

    private readonly Dictionary<long, Mission> _accepted = [];
    private readonly HashSet<Kind> _said = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var faction = Faction();

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "MissionAccepted" && Mission.Of(journalEvent) is { } accepted)
            {
                _accepted[accepted.Id] = accepted;
            }

            if (faction is null || context.IsPriming)
            {
                continue;
            }

            if (Warning(journalEvent, context.State, faction) is { } warning && _said.Add(warning.Kind))
            {
                yield return new Announcement(KeyPrefix + warning.Kind, warning.Text) { Cooldown = TimeSpan.Zero };
            }
        }
    }

    private (Kind Kind, string Text)? Warning(JournalEvent journalEvent, CommanderGameState? state, string faction)
    {
        switch (journalEvent.Kind)
        {
            case "RedeemVoucher" when IsBounty(journalEvent) && FactionPresent(state, faction):
                var rival = journalEvent.Items("Factions")
                    .Select(item => item.String("Faction"))
                    .FirstOrDefault(name => name is { Length: > 0 } && !Same(name, faction));

                return rival is null
                    ? null
                    : (Kind.RivalBounty, $"That bounty voucher raises {rival} here, where {faction} is present. It works against the campaign.");

            case "MissionFailed" when journalEvent.Long("MissionID") is { } failedId && MissionOf(failedId, state) is { } failed && Same(failed.Faction, faction):
                return (Kind.MissionFailed, $"Failing that mission costs {faction} influence.");

            case "MissionAccepted" when Mission.Of(journalEvent) is { } accepted
                && Same(accepted.TargetFaction, faction)
                && accepted.Faction is { Length: > 0 } issuer
                && !Same(issuer, faction):
                return (Kind.MissionAgainst, $"That mission, for {issuer}, is against {faction}. Completing it works against the campaign.");

            case "CommitCrime" when string.Equals(journalEvent.String("CrimeType"), "murder", StringComparison.OrdinalIgnoreCase)
                && MurderTouches(journalEvent, state, faction):
                return (Kind.Murder, $"A murder in {state?.Location.StarSystem ?? "this system"} counts against {faction}.");

            case "MarketSell" when IsFleetCarrierSale(journalEvent, state) && FactionPresent(state, faction):
                return (Kind.CarrierSale, $"A sale to a fleet carrier does not count toward the BGS, so it does nothing for {faction}.");

            default:
                return null;
        }
    }

    private static bool IsBounty(JournalEvent journalEvent) =>
        string.Equals(journalEvent.String("Type"), "bounty", StringComparison.OrdinalIgnoreCase);

    private static bool MurderTouches(JournalEvent journalEvent, CommanderGameState? state, string faction) =>
        journalEvent.String("Faction") is { Length: > 0 } jurisdiction
            ? Same(jurisdiction, faction)
            : FactionPresent(state, faction);

    private static bool IsFleetCarrierSale(JournalEvent journalEvent, CommanderGameState? state) =>
        state?.Location is { Docked: true } location
        && string.Equals(location.StationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase)
        && (journalEvent.Long("MarketID") is not { } market || location.MarketId is not { } docked || market == docked);

    private static bool FactionPresent(CommanderGameState? state, string faction) =>
        state?.Location.SystemAddress is { } address
        && state.BgsWork.FactionsIn(address).Any(name => Same(name, faction));

    private Mission? MissionOf(long id, CommanderGameState? state) =>
        _accepted.GetValueOrDefault(id) ?? state?.Missions.Missions.FirstOrDefault(mission => mission.Id == id);

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
