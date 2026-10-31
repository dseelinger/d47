using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The journal fold applies the ledgers before the callouts tick, so a callout reads totals that include this tick's events.</summary>
public class ACalloutReadsLedgersThatHoldThisTicksEventsTests
{
    private const string LoadGame =
        """{"timestamp":"2026-10-31T12:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture","Credits":1000}""";

    private const string Analyse =
        """{"timestamp":"2026-10-31T12:05:00Z","event":"ScanOrganic","ScanType":"Analyse","Genus":"$Codex_Ent_Cactoid_Genus_Name;","Genus_Localised":"Cactoida","Species":"$Codex_Ent_Cactoid_01_Name;","Species_Localised":"Cactoida Cortexum","SystemAddress":3274702866819,"Body":41}""";

    private const string Sale =
        """{"timestamp":"2026-10-31T12:06:00Z","event":"MarketSell","MarketID":2,"Type":"gold","Count":10,"SellPrice":9000,"TotalSale":90000,"AvgPricePaid":1}""";

    [Fact]
    public void AnAnalysisAndASaleFromThisTickAreInTheLedgersWhenTheCalloutsTick()
    {
        var fixture = new JournalTickFixture();
        var reader = new LedgerReader(fixture);
        fixture.Callouts.Add(reader);

        fixture.Write(LoadGame);
        fixture.Tick.Fold(fixture.Next());

        fixture.Write(Analyse, Sale);
        fixture.Tick.Fold(fixture.Next());

        Assert.Equal([(true, 0, false), (false, 1, true)], reader.Read);
    }

    private sealed class LedgerReader(JournalTickFixture fixture) : ICallout
    {
        public List<(bool Priming, int Held, bool Sold)> Read { get; } = [];

        public string Id => "ledger-reader";

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            Read.Add((
                context.IsPriming,
                fixture.Exobiology.Unsold("F1").Held.Count,
                fixture.CommodityLedger.LastSale("F1", "gold") is not null));

            return [];
        }
    }
}
