using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>The On this body page lists a bearing and distance to the ship, each mark and the last specimen of each open set.</summary>
public class TheBodyPageShowsBearingsToSavedPointsTests
{
    private const long Radius = 1_000_000;

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private sealed class Live
    {
        public GameStatus Status { get; set; } = At(0.001, 0);
    }

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
        state.Apply(Event(2, """{"event":"ScanOrganic","ScanType":"Log","Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum","Species":"Stratum Tectonicas","Species_Localised":"Stratum Tectonicas","SystemAddress":123,"Body":12}"""));
        state.Apply(
            Event(3, """{"event":"ScanOrganic","ScanType":"Sample","Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum","Species":"Stratum Tectonicas","Species_Localised":"Stratum Tectonicas","SystemAddress":123,"Body":12}"""),
            new SurfaceFix(0, 0.001, Radius));

        return state;
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

    private static string[] Tick(BodyPage page)
    {
        page.Tick();
        Dispatcher.UIThread.RunJobs();

        return TextOf(page);
    }

    private static void Save(Control page, string name)
    {
        if (!TestSurface.CapturesWanted)
        {
            return;
        }

        var window = (Window)TopLevel.GetTopLevel(page)!;
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void TheShipAMarkAndTheLastSpecimenEachGetARow()
    {
        var state = Landed();
        var live = new Live();
        var marks = new SurfaceMarks();
        marks.Add(state.Surface, null, new SurfaceFix(0.002, 0, Radius), Start + TimeSpan.FromMinutes(4));

        var page = new BodyPage(() => state, () => live.Status, null, null, marks);
        var drawn = TextOf(page);

        Assert.Contains("SHIP · 180° S · 17 M", drawn);
        Assert.Contains("MARK 1 · 0° N · 17 M", drawn);
        Assert.Contains("SAMPLE · STRATUM TECTONICAS · 135° SE · 25 M", drawn);
        Save(page, "body-bearings.png");
    }

    [AvaloniaFact]
    public void TheRowsFollowTheGamesPositionOnTheTick()
    {
        var state = Landed();
        var live = new Live();
        var page = new BodyPage(() => state, () => live.Status, null, null, new SurfaceMarks());

        Assert.Contains("SHIP · 180° S · 17 M", TextOf(page));

        live.Status = At(0, 0.002);

        Assert.Contains("SHIP · 270° W · 35 M", Tick(page));
    }

    [AvaloniaFact]
    public void WithNoLivePositionTheRowsSayNoPosition()
    {
        var state = Landed();
        var page = new BodyPage(() => state, () => new GameStatus(), null, null, new SurfaceMarks());

        Assert.Contains("SHIP · No position", TextOf(page));
    }

    [AvaloniaFact]
    public void WithNothingSavedThereAreNoRows()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(0, """{"event":"Location","StarSystem":"Blae","SystemAddress":123,"Body":"Blae 7 a","BodyID":12,"BodyType":"Planet"}"""));
        state.Apply(Event(1, """{"event":"SAAScanComplete","BodyName":"Blae 7 a","SystemAddress":123,"BodyID":12,"ProbesUsed":5,"EfficiencyTarget":6}"""));
        state.Apply(Event(2, """{"event":"ScanOrganic","ScanType":"Log","Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum","Species":"Stratum Tectonicas","Species_Localised":"Stratum Tectonicas","SystemAddress":123,"Body":12}"""));

        var page = new BodyPage(() => state, () => At(0, 0), null, null, new SurfaceMarks());

        Assert.DoesNotContain(TextOf(page), text => text.StartsWith("SHIP", StringComparison.Ordinal) || text.StartsWith("MARK", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void TheShipRowGoesAtLiftoffAndTheMarksAtLeavingTheBody()
    {
        var state = Landed();
        var live = new Live();
        var marks = new SurfaceMarks();
        marks.Add(state.Surface, null, new SurfaceFix(0.002, 0, Radius), Start + TimeSpan.FromMinutes(4));
        var page = new BodyPage(() => state, () => live.Status, null, null, marks);

        Assert.Contains(TextOf(page), text => text.StartsWith("SHIP", StringComparison.Ordinal));

        state.Apply(Event(5, """{"event":"Liftoff","PlayerControlled":true,"Latitude":0.0,"Longitude":0.0,"SystemAddress":123,"BodyID":12}"""));
        var drawn = Tick(page);

        Assert.DoesNotContain(drawn, text => text.StartsWith("SHIP", StringComparison.Ordinal));
        Assert.Contains(drawn, text => text.StartsWith("MARK 1", StringComparison.Ordinal));

        state.Apply(Event(6, """{"event":"LeaveBody","SystemAddress":123,"BodyID":12}"""));
        drawn = Tick(page);

        Assert.DoesNotContain(drawn, text => text.StartsWith("MARK 1", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, text => text.StartsWith("SAMPLE ·", StringComparison.Ordinal));
    }
}
