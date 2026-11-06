using D47.Core.Knowledge;

namespace D47.Core.Tests.Knowledge;

/// <summary>Every member throws; a fake overrides the ones its test uses.</summary>
public class StubGalaxyService : IGalaxyService
{
    public virtual Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<ColonisationScan> ScanForColonisationAsync(
        ColonisationQuery query,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(
        long systemAddress,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<MissionStationScan> FindMissionStationsAsync(
        MissionStationQuery query,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<FactionReport> FactionAsync(string faction, CancellationToken cancellationToken, bool refresh = false, IProgress<FactionProgress>? progress = null) =>
        throw new NotSupportedException();
}

/// <summary>Every member throws; a fake overrides the ones its test uses.</summary>
public class StubTradePlanService : ITradePlanService
{
    public virtual Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>Every member throws; a fake overrides the ones its test uses.</summary>
public class StubRouteService : IRouteService
{
    public virtual Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
