using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Seats;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Crew: the seats on the ship flown, filled from the page (#847).</summary>
[Trait("Category", "Integration")]
public class TheCrewPageFillsTheShipFlownTests
{
    private sealed record Surface(Window Window, CrewPage Page, GameStateStore Store, CrewSeatStore Seats, string Path);

    private static Surface Open(string ship, string shipName, params string[] more)
    {
        var path = Path.Combine(TempFolders.Create("d47-crew-seats-tests"), "crew-seats.json");
        var store = new GameStateStore();
        var seats = new CrewSeatStore(path, NullLogger<CrewSeatStore>.Instance);

        string[] journal =
        [
            """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            $$"""{"timestamp":"2026-10-05T09:00:01Z","event":"Loadout","Ship":"{{ship}}","ShipID":7,"Modules":[],"ShipName":"{{shipName}}","ShipIdent":"SW-01"}""",
            """{"timestamp":"2026-10-05T09:01:00Z","event":"CrewHire","Name":"Samira Voss","CrewID":1,"CombatRank":"Dangerous"}""",
            .. more,
        ];

        foreach (var line in journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var host = new CrewSeatsHost(
            () => seats,
            () => [("am_michael", "Michael"), ("af_heart", "Heart")],
            () => "kokoro",
            () => ["Ava"]);

        var page = new CrewPage(() => store.Active, host);
        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, page, store, seats, path);
    }

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static List<Button> Buttons(Control page, string name) =>
        [.. page.GetVisualDescendants().OfType<Button>().Where(button => button.Name == name)];

    private static List<TextBox> Boxes(Control page, string name) =>
        [.. page.GetVisualDescendants().OfType<TextBox>().Where(box => box.Name == name)];

    private static void Press(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AnAnacondaShowsThreeEmptySeatsAndOfferFillsThem()
    {
        var surface = Open("anaconda", "Sacred Wings");

        Assert.Contains("SEATS ABOARD SACRED WINGS", Text(surface.Page).Select(text => text.ToUpperInvariant()));
        Assert.Equal(3, Boxes(surface.Page, CrewSeatsSection.NameBoxName).Count);
        Assert.All(Boxes(surface.Page, CrewSeatsSection.NameBoxName), box => Assert.True(string.IsNullOrEmpty(box.Text)));

        Press(Buttons(surface.Page, CrewSeatsSection.OfferName).Single());

        Assert.All(Boxes(surface.Page, CrewSeatsSection.NameBoxName), box => Assert.False(string.IsNullOrEmpty(box.Text)));

        var again = new CrewSeatStore(surface.Path, NullLogger<CrewSeatStore>.Instance);
        again.Poll();
        Assert.Equal(3, again.For("F1", 7)?.Seats.Count);

        Capture(surface.Window, "fleet-crew-seats.png");
        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ASidewinderSaysItHasNoSeatAndDrawsNoRows()
    {
        var surface = Open("sidewinder", "Little Wing");

        Assert.Contains(CrewSeatRules.NoSeats("Sidewinder"), Text(surface.Page));
        Assert.Empty(Boxes(surface.Page, CrewSeatsSection.NameBoxName));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ASeatNamedLikeAHiredPilotIsRefusedAndNothingIsWritten()
    {
        var surface = Open("anaconda", "Sacred Wings");

        Boxes(surface.Page, CrewSeatsSection.NameBoxName)[0].Text = "samira voss";
        Press(Buttons(surface.Page, CrewSeatsSection.SaveName)[0]);

        var reason = surface.Page.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Name == CrewSeatsSection.ReasonName && block.IsVisible);

        Assert.Contains("samira voss", reason.Text);
        Assert.False(File.Exists(surface.Path));
        Capture(surface.Window, "fleet-crew-seats-refused.png");

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ASeatSavedFromARowIsStoredAndClearRemovesIt()
    {
        var surface = Open("anaconda", "Sacred Wings");

        Boxes(surface.Page, CrewSeatsSection.NameBoxName)[0].Text = "Rook";
        Press(Buttons(surface.Page, CrewSeatsSection.SaveName)[0]);

        Assert.Equal("Rook", surface.Seats.For("F1", 7)?.Seats.Single().Name);

        Press(Buttons(surface.Page, CrewSeatsSection.ClearName)[0]);

        Assert.Empty(surface.Seats.For("F1", 7)!.Seats);

        surface.Window.Close();
    }

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }
}
