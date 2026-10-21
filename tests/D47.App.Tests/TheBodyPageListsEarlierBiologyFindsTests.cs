using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The On this body page lists the nearest earlier find of each variant on the body, after the saved points.</summary>
public class TheBodyPageListsEarlierBiologyFindsTests
{
    private const long Radius = 1_000_000;

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private static GameStatus At(double latitude, double longitude) =>
        new() { Latitude = latitude, Longitude = longitude, PlanetRadius = Radius, BodyName = "Blae 7 a", ReadAt = Start };

    private static JournalEvent Event(int minute, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Start + TimeSpan.FromMinutes(minute), root.GetProperty("event").GetString()!, root);
    }

    private static CommanderGameState Landed()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(0, """{"event":"Location","StarSystem":"Blae","SystemAddress":123,"Body":"Blae 7 a","BodyID":12,"BodyType":"Planet"}"""));
        state.Apply(Event(1, """{"event":"Touchdown","PlayerControlled":true,"Latitude":0.0,"Longitude":0.0,"SystemAddress":123,"BodyID":12}"""));
        state.Apply(Event(2, """{"event":"SAAScanComplete","BodyName":"Blae 7 a","SystemAddress":123,"BodyID":12,"ProbesUsed":5,"EfficiencyTarget":6}"""));
        state.Apply(Event(2, """{"event":"ScanOrganic","ScanType":"Log","Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum","Species":"Stratum Tectonicas","Species_Localised":"Stratum Tectonicas","SystemAddress":123,"Body":12}"""));

        return state;
    }

    private static EarlierFind Find(string name, double latitude, double longitude, string body = "Blae 7 a") =>
        new(body, 2100000 + Math.Abs(name.GetHashCode() % 1000), name, latitude, longitude);

    private static EarlierFinds Store(params EarlierFind[] finds)
    {
        var store = new EarlierFinds();
        store.Set(123, finds);

        return store;
    }

    private static string[] TextOf(Control page)
    {
        if (page.GetVisualParent() is null)
        {
            new Window { Content = page, Width = 900, Height = 900 }.Show();
        }

        Dispatcher.UIThread.RunJobs();

        return
        [
            .. page.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(block => block.Inlines is { Count: > 0 } inlines
                    ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
                    : block.Text ?? string.Empty)
                .Where(text => text.Length > 0),
        ];
    }

    private static BodyPage PageWith(CommanderGameState state, Func<EarlierFinds?> store, GameStatus? live = null)
    {
        var status = live ?? At(0.001, 0);

        return new BodyPage(() => state, () => status, null, null, new SurfaceMarks(), store);
    }

    [AvaloniaFact]
    public void EachVariantGetsItsNearestFindNearestFirst()
    {
        var store = Store(
            Find("Bacterium Aurasus - Green", 0.0035, 0),
            Find("Bacterium Aurasus - Green", 0.002, 0),
            Find("Stratum Tectonicas - Red", 0.001, 0.003));

        var page = PageWith(Landed(), () => store);
        var drawn = TextOf(page);

        Assert.Contains("AURASUS GREEN · 0° N · 17 M", drawn);
        Assert.Contains("TECTONICAS RED · 90° E · 52 M", drawn);
        Assert.Equal(1, drawn.Count(text => text.StartsWith("AURASUS GREEN", StringComparison.Ordinal)));
        Assert.True(Array.IndexOf(drawn, "AURASUS GREEN · 0° N · 17 M") < Array.IndexOf(drawn, "TECTONICAS RED · 90° E · 52 M"));

        if (TestSurface.CapturesWanted)
        {
            using var frame = ((Window)TopLevel.GetTopLevel(page)!).CaptureRenderedFrame()!;
            frame.SaveCapture("body-earlier-finds.png");
        }
    }

    [AvaloniaFact]
    public void FindsFollowTheSavedPointRows()
    {
        var state = Landed();
        var drawn = TextOf(PageWith(state, () => Store(Find("Bacterium Aurasus - Green", 0.002, 0))));

        Assert.True(Array.FindIndex(drawn, text => text.StartsWith("SHIP", StringComparison.Ordinal))
                    < Array.FindIndex(drawn, text => text.StartsWith("AURASUS", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void OnlyTheFiveNearestShowAndTheRestAreCounted()
    {
        var store = Store(
            Find("Aleoida Arcus - Green", 0.002, 0),
            Find("Aleoida Coronamus - Blue", 0.003, 0),
            Find("Bacterium Aurasus - Green", 0.004, 0),
            Find("Bacterium Cerbrus - Teal", 0.005, 0),
            Find("Stratum Tectonicas - Red", 0.006, 0),
            Find("Tussock Pennata - Lime", 0.007, 0),
            Find("Tussock Ventusa - Red", 0.008, 0));

        var drawn = TextOf(PageWith(Landed(), () => store));

        Assert.Contains("TECTONICAS RED · 0° N · 87 M", drawn);
        Assert.DoesNotContain(drawn, text => text.StartsWith("PENNATA", StringComparison.Ordinal));
        Assert.Contains("and 2 more earlier finds", drawn);
    }

    [AvaloniaFact]
    public void WithNoLivePositionTheRowsSayNoPosition()
    {
        var drawn = TextOf(PageWith(Landed(), () => Store(Find("Bacterium Aurasus - Green", 0.002, 0)), new GameStatus()));

        Assert.Contains("AURASUS GREEN · No position", drawn);
    }

    [AvaloniaFact]
    public void WithTheSettingOffOrNoFindsOnThisBodyThereAreNoRows()
    {
        var off = TextOf(PageWith(Landed(), () => null));
        var elsewhere = TextOf(PageWith(Landed(), () => Store(Find("Bacterium Aurasus - Green", 0.002, 0, "Blae 9"))));

        Assert.DoesNotContain(off, text => text.StartsWith("AURASUS", StringComparison.Ordinal));
        Assert.DoesNotContain(elsewhere, text => text.StartsWith("AURASUS", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void TheRowsGoWhenTheCommanderLeavesTheBody()
    {
        var state = Landed();
        var store = Store(Find("Bacterium Aurasus - Green", 0.002, 0));
        var page = PageWith(state, () => store);

        Assert.Contains(TextOf(page), text => text.StartsWith("AURASUS", StringComparison.Ordinal));

        state.Apply(Event(3, """{"event":"LeaveBody","SystemAddress":123,"BodyID":12}"""));
        page.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(TextOf(page), text => text.StartsWith("AURASUS", StringComparison.Ordinal));
    }
}
