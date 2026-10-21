using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class SharingWithEddnIsTheCommandersChoiceTests
{
    private sealed class Sink : IEddnSink
    {
        public List<EddnEnvelope> Posted { get; } = [];

        public void Post(EddnEnvelope envelope) => Posted.Add(envelope);
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static string Header() =>
        """{"timestamp":"2026-10-21T11:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r1"}""";

    private static string Market(DateTimeOffset at) =>
        $$"""{"timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}","event":"Market","MarketID":1,"StationName":"Port","StarSystem":"Sol"}""";

    private static string MarketFile(DateTimeOffset at) =>
        $$"""
        {"timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}","event":"Market","MarketID":1,"StationName":"Port","StarSystem":"Sol",
         "Items":[{"Name":"$gold_name;","Category":"$MARKET_category_metals;","BuyPrice":1,"SellPrice":1,"MeanPrice":1,
         "StockBracket":0,"DemandBracket":0,"Stock":1,"Demand":1}]}
        """;

    private static (EddnContribution Contribution, Sink Sink) Build(DateTimeOffset marketAt, bool enabled = true)
    {
        var composer = new EddnComposer(() => "anon", "1", () => MarketFile(marketAt));
        var sink = new Sink();

        return (new EddnContribution(composer, sink, () => enabled), sink);
    }

    private static void Apply(EddnContribution contribution, bool first, bool offDuty, params string[] lines) =>
        contribution.Apply([.. lines.Select(Event)], Now, first, offDuty);

    [Fact]
    public void ALiveMarketEventPostsOneMessage()
    {
        var at = Now.AddSeconds(-5);
        var (contribution, sink) = Build(at);

        Apply(contribution, first: false, offDuty: false, Header(), Market(at));

        Assert.Single(sink.Posted);
    }

    [Fact]
    public void TheBacklogOnTheFirstTickPostsNothing()
    {
        var at = Now.AddSeconds(-5);
        var (contribution, sink) = Build(at);

        Apply(contribution, first: true, offDuty: false, Header(), Market(at));

        Assert.Empty(sink.Posted);
    }

    [Fact]
    public void AnEventOlderThanFiveMinutesPostsNothing()
    {
        var at = Now.AddMinutes(-6);
        var (contribution, sink) = Build(at);

        Apply(contribution, first: false, offDuty: false, Header(), Market(at));

        Assert.Empty(sink.Posted);
    }

    [Fact]
    public void AnotherCommandersEventPostsNothing()
    {
        var at = Now.AddSeconds(-5);
        var (contribution, sink) = Build(at);

        Apply(contribution, first: false, offDuty: true, Header(), Market(at));

        Assert.Empty(sink.Posted);
    }

    [Fact]
    public void WithTheSettingOffNothingIsPosted()
    {
        var at = Now.AddSeconds(-5);
        var (contribution, sink) = Build(at, enabled: false);

        Apply(contribution, first: false, offDuty: false, Header(), Market(at));

        Assert.Empty(sink.Posted);
    }

    [Fact]
    public void TheVersionFromTheBacklogIsStillKnownWhenTheLiveEventArrives()
    {
        var at = Now.AddSeconds(-5);
        var (contribution, sink) = Build(at);

        Apply(contribution, first: true, offDuty: false, Header());
        Apply(contribution, first: false, offDuty: false, Market(at));

        Assert.Single(sink.Posted);
    }

    [Fact]
    public void TheSettingIsOffByDefault() =>
        Assert.False(new D47Settings().Contribution.Eddn);

    [Fact]
    public void OffItSaysNothingIsSent()
    {
        var entry = Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.Eddn);

        Assert.False(entry.Active);
        Assert.Contains("EDDN → nothing sent", EgressDisclosure.Describe(new D47Settings(), false), StringComparison.Ordinal);
    }

    [Fact]
    public void OnItNamesTheHostAndWhatDoesNotGo()
    {
        var settings = new D47Settings { Contribution = new ContributionSettings { Eddn = true } };

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.Eddn);

        Assert.True(entry.Active);
        Assert.Equal("eddn.edcd.io", entry.Destination);
        Assert.Contains("not your Commander name", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void ItSitsAfterTheSignalIndex()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.SignalIndex) + 1, ids.IndexOf(EgressDisclosure.Eddn));
    }

    [Fact]
    public void TheModelIsRefusedTheSetting()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(GalaxyCapability.EddnKey, "true", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.False(surface.Settings.Current.Contribution.Eddn);
    }

    [Fact]
    public void TheCommanderCanTurnItOn()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(GalaxyCapability.EddnKey, "true", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.True(surface.Settings.Current.Contribution.Eddn);
    }

    [Fact]
    public void EachCommanderKeepsTheirOwnNumberAcrossRestarts()
    {
        var install = new MemoryInstall();
        var first = new EddnUploaderIds(install.Files, install.Paths.EddnUploadersFile);

        var a = first.For("F1");
        var b = first.For("F2");

        Assert.NotEqual(a, b);
        Assert.True(D47.Core.Diagnostics.Donation.DonorToken.IsWellFormed(a));

        var second = new EddnUploaderIds(install.Files, install.Paths.EddnUploadersFile);

        Assert.Equal(a, second.For("F1"));
        Assert.Equal(b, second.For("F2"));
    }
}
