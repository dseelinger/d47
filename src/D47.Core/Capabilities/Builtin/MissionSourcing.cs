using System.Globalization;
using System.Text;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Where to buy the cargo of the accepted collect missions, in flying order.</summary>
internal static class MissionSourcing
{
    private const string CollectFamily = "Mission_Collect";

    public static async Task<string> AnswerAsync(
        CommanderGameState? state,
        ITradePlanService? trade,
        SettingsService? settings,
        CancellationToken cancellationToken)
    {
        var collect = state?.Missions.Missions
            .Where(mission => mission.HasDetail
                && mission.Commodity is not null
                && mission.Count is > 0
                && MissionCatalogue.Find(mission.Name)?.Family == CollectFamily)
            .ToList() ?? [];

        if (collect.Count == 0)
        {
            return "You have no collect missions on your board, so there is nothing to source.";
        }

        if (trade is null || settings is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return "Looking markets up is switched off, so I cannot say where to buy any of it.";
        }

        var needs = Needs(state!, collect, out var aboard);

        var handIns = collect
            .Where(mission => mission.DestinationSystem is not null)
            .Select(mission => (Station: mission.DestinationStation ?? mission.DestinationSystem!, System: mission.DestinationSystem!))
            .Distinct()
            .ToList();

        if (handIns.Count == 0)
        {
            return "None of your collect missions names a place to hand the cargo in, so I have nowhere to search around.";
        }

        var toBuy = needs.Sum(need => need.Tonnes);

        if (toBuy == 0)
        {
            return $"Your hold already covers all of it: {Tonnes(aboard)} aboard.";
        }

        SourcingAnswer answer;

        try
        {
            answer = await trade
                .SourceConstructionAsync(
                    new SourcingSearch(
                        handIns[0].System,
                        handIns[0].Station,
                        needs,
                        Rank: SourcingRank.Nearest,
                        AlsoAround: [.. handIns.Skip(1).Select(handIn => handIn.System).Distinct(StringComparer.OrdinalIgnoreCase)]),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException error)
        {
            return error.Message;
        }

        var positions = answer.SystemPositions;

        var legs = SourcingRoute.Order(
            state!.Location.StarPos is { } here ? (here.X, here.Y, here.Z) : null,
            answer.Plan.Stops,
            [.. handIns.Select(handIn => new HandIn(
                handIn.Station,
                handIn.System,
                positions is not null && positions.TryGetValue(handIn.System, out var at) ? at : null))]);

        return Describe(state, answer, legs, aboard, toBuy);
    }

    private static List<SourcingNeed> Needs(CommanderGameState state, List<Mission> collect, out int aboard)
    {
        var needs = new List<SourcingNeed>();
        aboard = 0;

        foreach (var group in collect.GroupBy(mission => mission.Commodity!, StringComparer.Ordinal))
        {
            var total = group.Sum(mission => mission.Count!.Value);
            var held = state.Hold.Of(group.Key);
            var first = group.First();

            aboard += Math.Min(total, held);
            needs.Add(new SourcingNeed(first.CommodityLocalised ?? first.Commodity!, group.Key, Math.Max(0, total - held)));
        }

        return needs;
    }

    private static string Describe(
        CommanderGameState state,
        SourcingAnswer answer,
        IReadOnlyList<RouteLeg> legs,
        int aboard,
        int toBuy)
    {
        var said = new StringBuilder();

        if (answer.Plan.Stops.Count == 0)
        {
            said.AppendLine("Nothing within range of your hand-in systems is selling any of it.");
        }
        else
        {
            said.AppendLine(
                $"{Tonnes(toBuy)} to buy"
                + (aboard > 0 ? $", with {Tonnes(aboard)} already aboard" : string.Empty)
                + ". In flying order:");

            var number = 1;

            foreach (var leg in legs)
            {
                var distance = leg.Distance is { } ly ? $", {ly.ToString("0.#", CultureInfo.InvariantCulture)} ly" : string.Empty;

                if (leg.IsHandIn)
                {
                    said.AppendLine($"  {number++}. Hand in at {leg.Station} ({leg.System}){distance}.");

                    continue;
                }

                var stop = answer.Plan.Stops.First(stop => stop.Market.IsSamePlaceAs(leg.Station, leg.System));
                var lots = stop.Lots
                    .OrderByDescending(lot => lot.Tonnes)
                    .Select(lot => $"{Tonnes(lot.Tonnes)} {lot.Commodity} at {Credits(lot.UnitPrice)}");

                said.AppendLine($"  {number++}. Buy at {leg.Station} ({leg.System}){distance}: {string.Join(", ", lots)}.");
            }

            var total = legs.Sum(leg => leg.Distance ?? 0);

            said.AppendLine($"The whole round is {total.ToString("0.#", CultureInfo.InvariantCulture)} ly.");
        }

        if (answer.Plan.Unpriced.Count > 0)
        {
            said.AppendLine($"Nothing in range prices: {string.Join(", ", answer.Plan.Unpriced)}.");
        }

        if (answer.Plan.Shortfalls.Count > 0)
        {
            var short_ = answer.Plan.Shortfalls
                .OrderByDescending(pair => pair.Value)
                .Select(pair => $"{pair.Key} by {Tonnes(pair.Value)}");

            said.AppendLine($"Stocked but not enough: {string.Join(", ", short_)}.");
        }

        if (state.Ship.CargoCapacity is { } capacity and > 0 && toBuy > capacity)
        {
            var runs = (toBuy + capacity - 1) / capacity;

            said.AppendLine($"At {Tonnes(capacity)} a run, that is {runs} full loads.");
        }

        if (!state.Hold.IsKnown)
        {
            said.AppendLine("I have not read your cargo hold yet, so nothing here is netted off against what you are carrying.");
        }

        said.Append("Prices are other Commanders' reports and supply ages fastest during a rush.");

        return said.ToString();
    }

    private static string Tonnes(int value) =>
        $"{value.ToString("N0", CultureInfo.InvariantCulture)} tonne{(value == 1 ? string.Empty : "s")}";

    private static string Credits(long value) => $"{value.ToString("N0", CultureInfo.InvariantCulture)} cr";
}
