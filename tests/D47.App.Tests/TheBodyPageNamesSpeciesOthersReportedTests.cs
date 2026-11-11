using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Navigation › On this body names the species other Commanders reported for a genus the Commander has not sampled.</summary>
public class TheBodyPageNamesSpeciesOthersReportedTests
{
    private const long System = 123;

    private const int BodyId = 12;

    private const string Body = "Blae 7 a";

    private const string Reported = "Stratum Cucumisis ×7 — 16,202,800 cr";

    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private static readonly SystemBiology Spansh = new(
        System,
        [new SurveyedBody(Body, BodyId, 0, [new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800)])]);

    private static JournalEvent Event(int minute, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Start + TimeSpan.FromMinutes(minute), root.GetProperty("event").GetString()!, root);
    }

    private static CommanderGameState Mapped(bool sampleBacterium = false)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(0, $$"""{"event":"Location","StarSystem":"Blae","SystemAddress":123,"Body":"{{Body}}","BodyID":12,"BodyType":"Planet"}"""));
        state.Apply(Event(1, $$"""{"event":"SAASignalsFound","BodyName":"{{Body}}","BodyID":12,"SystemAddress":123,"Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2}],"Genuses":[{"Genus":"$Codex_Ent_Bacterial_Genus_Name;","Genus_Localised":"Bacterium"},{"Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum"}]}"""));

        if (sampleBacterium)
        {
            state.Apply(Event(2, """{"event":"ScanOrganic","ScanType":"Log","Genus":"Bacterium","Genus_Localised":"Bacterium","Species":"Bacterium Aurasus","Species_Localised":"Bacterium Aurasus","SystemAddress":123,"Body":12}"""));
        }

        return state;
    }

    private static CommanderGameState Unmapped()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(0, $$"""{"event":"Location","StarSystem":"Blae","SystemAddress":123,"Body":"{{Body}}","BodyID":12,"BodyType":"Planet"}"""));
        state.Apply(Event(1, $$"""{"event":"FSSBodySignals","BodyName":"{{Body}}","BodyID":12,"SystemAddress":123,"Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}]}"""));

        return state;
    }

    private static BodyPage PageWith(CommanderGameState state, SurveyedBiology? held = null, Func<long, CancellationToken, Task<SystemBiology>>? ask = null, EarlierFinds? finds = null) =>
        new(() => state, () => new GameStatus(), null, null, null, () => finds, held, ask);

    private static SurveyedBiology Holding(SystemBiology biology)
    {
        var held = new SurveyedBiology();
        held.Set(biology);

        return held;
    }

    [AvaloniaFact]
    public void ANotStartedGenusSpanshReportsNamesTheSpeciesWithItsValueAndTheTag()
    {
        var drawn = PageText.Of(PageWith(Mapped(), Holding(Spansh)));

        Assert.Contains(Reported, drawn);
        Assert.Contains("REPORTED", drawn);
        Assert.DoesNotContain(drawn, text => text == SessionPage.Credits(16_202_800));
    }

    [AvaloniaFact]
    public void ANotStartedGenusWithNoReportSaysNotYetSampled()
    {
        var drawn = PageText.Of(PageWith(Mapped(), Holding(new SystemBiology(System, []))));

        Assert.Equal(2, drawn.Count(text => text == "Not yet sampled"));
        Assert.DoesNotContain("REPORTED", drawn);
    }

    [AvaloniaFact]
    public void AnUnmappedBodySpanshReportsListsItsGeneraAndNotTheNoGenusNote()
    {
        var drawn = PageText.Of(PageWith(Unmapped(), Holding(Spansh)));

        Assert.Contains(Reported, drawn);
        Assert.Contains(drawn, text => string.Equals(text, "Stratum", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(drawn, text => text.StartsWith("No genus is known", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AnUnmappedBodyNobodyReportedKeepsTheNoGenusNote()
    {
        var drawn = PageText.Of(PageWith(Unmapped(), Holding(new SystemBiology(System, []))));

        Assert.Contains(drawn, text => text.StartsWith("No genus is known", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AnOpenSetShowsTheSampledSpeciesNotTheReportedOne()
    {
        var spansh = new SystemBiology(
            System,
            [new SurveyedBody(Body, BodyId, 0, [new ExobiologySpecies("Bacterium", "Bacterium Nebulus", 3, 5_000_000)])]);

        var drawn = PageText.Of(PageWith(Mapped(sampleBacterium: true), Holding(spansh)));

        Assert.Contains("Bacterium Aurasus", drawn);
        Assert.DoesNotContain(drawn, text => text.Contains("Bacterium Nebulus", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task WithTheHolderEmptyThePageAsksOnceAndRedrawsWithTheReport()
    {
        var held = new SurveyedBiology();
        var asked = 0;
        var answer = new TaskCompletionSource<SystemBiology>();

        Task<SystemBiology> Ask(long address, CancellationToken token)
        {
            Interlocked.Increment(ref asked);

            return answer.Task;
        }

        var page = PageWith(Mapped(), held, Ask);

        Assert.DoesNotContain(Reported, PageText.Of(page));

        page.Tick();
        page.Tick();

        answer.SetResult(Spansh);

        for (var tries = 0; tries < 200 && !held.TryGet(System, out _); tries++)
        {
            await Task.Delay(10);
        }

        for (var tries = 0; tries < 200 && !PageText.Of(page).Contains(Reported); tries++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.Equal(1, asked);
        Assert.Contains(Reported, PageText.Of(page));
        Assert.Contains("REPORTED", PageText.Of(page));
    }

    [AvaloniaFact]
    public void ASpeciesCanonnLoggedOnTheBodyShowsTheTagAndNoValue()
    {
        var finds = new EarlierFinds();
        finds.Set(System, [new EarlierFind(Body, 2100001, "Stratum Tectonicas - Red", 0.1, 0.1)]);

        var drawn = PageText.Of(PageWith(Mapped(), Holding(new SystemBiology(System, [])), finds: finds));

        Assert.Contains("Stratum Tectonicas", drawn);
        Assert.Contains("REPORTED", drawn);
        Assert.DoesNotContain(drawn, text => text.Contains(" cr", StringComparison.Ordinal));
    }
}
