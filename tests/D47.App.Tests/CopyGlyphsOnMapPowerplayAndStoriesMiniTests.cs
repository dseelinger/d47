using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Powerplay neighbour table, the galaxy map's current-system label and the Stories mini carry a copy glyph (#1988).</summary>
[Trait("Category", "Integration")]
public class CopyGlyphsOnMapPowerplayAndStoriesMiniTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private const string Far = "Dyson's Hollow";

    private static List<Button> Glyphs(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => CopyGlyph.GetCopies(button) is not null)];

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 1280, Height = 860 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EachPowerplayNeighbourRowCopiesItsSystem(bool copy)
    {
        var clipboard = new RecordingClipboard();
        var page = new StarSystemPage(
            new StarSystemSurface(new Systems(), () => new JournalLocation("Sol", null, false, null), () => TestSurface.Settings().Current.Knowledge.GalaxySearch, () => { }),
            copy ? (text => clipboard.SetTextAsync(text)) : null);

        var table = page.NeighbourTable(new PowerplayNeighbourhood(2,
        [
            new PowerplayNeighbour("Alioth", 5, "Power A", "Fortified", 0.5, ["Power A"]),
            new PowerplayNeighbour("Lave", 9, null, null, null, []),
        ]));
        var window = Show(table);

        var glyphs = Glyphs(table);

        if (!copy)
        {
            Assert.Empty(glyphs);
        }
        else
        {
            Assert.Equal(["Alioth", "Lave"], glyphs.Select(glyph => CopyGlyph.GetCopies(glyph)));
            Click(glyphs[1]);
            Assert.Equal(["Lave"], clipboard.Written);
        }

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheGalaxyMapCurrentSystemLabelCopiesTheCurrentSystem(bool copy)
    {
        var clipboard = new RecordingClipboard();
        var here = "Sol";
        var page = new GalaxyMapPage(
            () => null, () => null, () => new NavRoute(), () => here, null, () => Now, headset: false,
            copy: copy ? (text => clipboard.SetTextAsync(text)) : null);
        var window = Show(page);

        var glyphs = Glyphs(page);

        if (!copy)
        {
            Assert.DoesNotContain(glyphs, glyph => CopyGlyph.GetCopies(glyph) == "Sol");
        }
        else
        {
            var glyph = Assert.Single(glyphs, glyph => CopyGlyph.GetCopies(glyph) == "Sol");
            Assert.True(glyph.IsEffectivelyVisible);
            Click(glyph);
            Assert.Equal(["Sol"], clipboard.Written);

            here = string.Empty;
            page.Refresh();
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(Glyphs(page), shown => shown.IsEffectivelyVisible && CopyGlyph.GetCopies(shown) == "Sol");
        }

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheStoriesMiniCopiesTheNextSystemAndEachToldSystem(bool copy)
    {
        var paths = new AppPaths(TempFolders.Create("d47-mini-copy"));
        paths.EnsureCreated();
        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        Assert.Null(book.Write("F1", new Adventure
        {
            Key = "the-lantern-route",
            Name = "The Lantern Route",
            Source = AdventureSource.Commander,
            Spine = new AdventureSpine { Premise = "An outpost still runs a beacon." },
            Opening = "Beacons cost money.",
            Beats =
            [
                new AdventureBeat
                {
                    Title = "The Lantern",
                    Function = "setup",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, System = Far },
                    Line = "Scoop here.",
                },
            ],
            Told =
            [
                new AdventureTold
                {
                    Kind = AdventureToldKind.Beat,
                    Text = "Scoop here.",
                    At = Now,
                    Beat = 0,
                    Trigger = $"arrive at {Far}",
                    Title = "The Lantern",
                },
            ],
            AcceptedAt = Now.AddHours(-1),
        }));

        var store = new GameStateStore();
        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-08-22T19:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""", NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
        var state = store.Active!;

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => state,
            () => null, () => null, null, null, NullLogger.Instance);
        var surface = new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, _ => { }, () => false, () => false, () => null, () => { });

        var clipboard = new RecordingClipboard();
        var mini = new AdventureMini(surface, null, () => copy ? (text => clipboard.SetTextAsync(text)) : null);
        var window = Show(mini);

        var glyphs = Glyphs(mini);

        if (!copy)
        {
            Assert.Empty(glyphs);
        }
        else
        {
            Assert.Equal([Far, Far], glyphs.Select(glyph => CopyGlyph.GetCopies(glyph)));
            Click(glyphs[0]);
            Assert.Equal([Far], clipboard.Written);
        }

        window.Close();
    }
}
