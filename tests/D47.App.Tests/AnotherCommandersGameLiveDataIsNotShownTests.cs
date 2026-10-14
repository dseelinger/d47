using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>While another Commander shows, the pages that draw the game's live files say who is in the game instead (#1105).</summary>
public class AnotherCommandersGameLiveDataIsNotShownTests
{
    private const string Line = "CMDR Kestrel Vane is in the game; their route shows when they are the Commander shown.";

    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private sealed class Duty
    {
        public bool Off { get; set; }

        public string? Say(string what) => Off ? $"CMDR Kestrel Vane is in the game; their {what} shows when they are the Commander shown." : null;
    }

    private static NavRoute Route() => new()
    {
        Hops = [new RouteHop("Sol", "G") { Position = (0, 0, 0) }, new RouteHop("Colonia", "K") { Position = (9, 0, 0) }],
    };

    private static string[] TextOf(Control page)
    {
        if (page.GetVisualParent() is null)
        {
            new Window { Content = page, Width = 900, Height = 700 }.Show();
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

    private static string[] Redrawn(Control page, Action refresh)
    {
        refresh();
        Dispatcher.UIThread.RunJobs();

        return TextOf(page);
    }

    [AvaloniaFact]
    public void TheRoutePageShowsNoHopsOrHereMarkerForAnotherCommander()
    {
        var duty = new Duty { Off = true };
        var page = new RouteProgressPage(Route, () => "Sol", null, duty.Say);

        var drawn = TextOf(page);

        Assert.Contains(Line, drawn);
        Assert.DoesNotContain(drawn, text => text.Equals("here", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(drawn, text => text.Contains("Colonia", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void ThePickedBackInGameCommanderGetsTheRoutePageOnTheNextRefresh()
    {
        var duty = new Duty { Off = true };
        var page = new RouteProgressPage(Route, () => "Sol", null, duty.Say);
        TextOf(page);

        duty.Off = false;
        var drawn = Redrawn(page, page.Refresh);

        Assert.DoesNotContain(Line, drawn);
        Assert.Contains(drawn, text => text.Equals("here", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(drawn, text => text.Contains("COLONIA", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void TheMiniRouteShowsNoWaypointsForAnotherCommander()
    {
        var plans = new RoutePlanBook("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
        plans.Record(
            new PlottedRoute("Sol", "Colonia", 1_000, 2, [new RouteWaypoint("Waypoint 0", 1, 700, IsNeutron: true), new RouteWaypoint("Colonia", 1, 0, IsNeutron: false)]),
            "Sol to Colonia",
            Start);

        var duty = new Duty { Off = true };
        var mini = new RouteMini(plans, Route, () => "Sol", duty.Say);

        var drawn = TextOf(mini);

        Assert.Contains(Line, drawn);
        Assert.DoesNotContain("Waypoint 0", drawn);

        duty.Off = false;
        drawn = Redrawn(mini, mini.Refresh);

        Assert.Contains("Waypoint 0", drawn);
        Assert.DoesNotContain(Line, drawn);
    }

    [AvaloniaFact]
    public void BestCargoOffersNoRouteEndForAnotherCommander()
    {
        var duty = new Duty { Off = true };
        var page = new RouteBestCargoPage(
            CapabilityRegistry.Build([]), new BestCargoBoard(), () => null, Route, () => false, offDuty: duty.Say);

        Assert.Null(page.RouteEnd);

        duty.Off = false;
        Assert.True(page.Tick());

        Assert.Equal("Colonia", page.RouteEnd);
    }

    [AvaloniaFact]
    public void OnThisBodyShowsNoDistanceForAnotherCommander()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(0, """{"event":"Location","StarSystem":"Blae","SystemAddress":123,"Body":"Blae 7 a","BodyID":12,"BodyType":"Planet"}"""));
        state.Apply(Event(1, """{"event":"ScanOrganic","ScanType":"Log","Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum","Species":"Stratum Tectonicas","Species_Localised":"Stratum Tectonicas","SystemAddress":123,"Body":12}"""));
        state.Apply(Event(2, """{"event":"ScanOrganic","ScanType":"Sample","Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum","Species":"Stratum Tectonicas","Species_Localised":"Stratum Tectonicas","SystemAddress":123,"Body":12}"""), new SurfaceFix(0, 0, 1_000_000));

        var live = new GameStatus { Latitude = 0.001, Longitude = 0, PlanetRadius = 1_000_000, BodyName = "Blae 7 a", ReadAt = Start };
        var duty = new Duty { Off = true };
        var page = new BodyPage(() => state, () => live, null, duty.Say);

        var drawn = TextOf(page);

        Assert.Contains(drawn, text => text.Equals("CMDR Kestrel Vane is in the game; their position shows when they are the Commander shown.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(BodyPage.NoPosition, drawn);
        Assert.DoesNotContain(drawn, text => text.EndsWith(" M", StringComparison.Ordinal));

        duty.Off = false;
        page.Tick();
        Dispatcher.UIThread.RunJobs();
        drawn = TextOf(page);

        Assert.DoesNotContain(drawn, text => text.Contains("in the game", StringComparison.Ordinal));
        Assert.Contains(drawn, text => text.EndsWith(" M", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLineNamesTheCommanderInTheGameOnlyWhileAnotherIsShown()
    {
        var store = new GameStateStore();
        var kestrel = new CommanderIdentity("F1", "Kestrel Vane");
        var other = new CommanderIdentity("F2", "Other Person");

        store.Apply(Journal(0, """{"event":"Commander","FID":"F1","Name":"Kestrel Vane"}"""));
        store.Pick(kestrel);
        var line = OffDutyNote.For(store);

        Assert.Null(line(OffDutyNote.Route));

        store.Pick(other);

        Assert.Equal(Line, line(OffDutyNote.Route));
    }

    [Fact]
    public void TheLineSaysNobodyIsLoggedInWhileNobodyIs()
    {
        var store = new GameStateStore();

        store.Seed(new CommanderIdentity("F1", "Kestrel Vane"));

        Assert.Equal("No Commander is logged in yet; the route shows when one is.", OffDutyNote.For(store)(OffDutyNote.Route));
    }

    private static JournalEvent Journal(int minute, string json) => Event(minute, json);

    private static JournalEvent Event(int minute, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Start + TimeSpan.FromMinutes(minute), root.GetProperty("event").GetString()!, root);
    }
}
