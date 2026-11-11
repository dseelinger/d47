using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Pressing a genus row's picture on Search › System › Biology shows it whole in a dialog; a row with no picture does not press.</summary>
[Trait("Category", "Integration")]
public sealed class APressedSpeciesPictureEnlargesTests
{
    private const long Here = 7_700_100;

    private const string Region = "Inner Orion Spur";

    private const string Folder = @"C:\d47\data\codex-pictures";

    private const long Orange = 2_320_701;

    private const long Magenta = 2_320_702;

    private static int _second;

    private static StarPosition Position => GalacticRegions.LabelAt(GalacticRegions.IdOf(Region)!.Value)!.Value;

    private static string Time() =>
        new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero).AddSeconds(Interlocked.Increment(ref _second))
            .ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile
            {
                Name = "Vask Reach",
                SystemAddress = Here,
                Position = Position,
                Bodies =
                [
                    new BodyProfile { BodyId = 0, Name = "Vask Reach", Type = "Star", IsMainStar = true },
                    new BodyProfile { BodyId = 3, Name = "Vask Reach 3", Type = "Planet", DistanceToArrival = 288, ParentId = 0 },
                ],
            });

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class NotFound : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    private static void Apply(GameStateStore store, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static GameStateStore Store(string? variant)
    {
        var store = new GameStateStore();
        Apply(store, """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F7","Name":"Tamsin Vey"}""");
        Apply(store, $$"""{"timestamp":"2026-08-16T09:30:00Z","event":"Location","StarSystem":"Vask Reach","SystemAddress":{{Here}},"Docked":false}""");
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"SAASignalsFound","BodyName":"Vask Reach 3","BodyID":3,"SystemAddress":{{Here}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2}],
             "Genuses":[{"Genus":"$Codex_Ent_Bacterium_Genus_Name;","Genus_Localised":"Bacterium"},{"Genus":"$Codex_Ent_Tussock_Genus_Name;","Genus_Localised":"Tussock"}]}
            """);

        var variantFields = variant is null ? "" : $",\"Variant\":\"$Codex_Ent_Bacterium_Variant;\",\"Variant_Localised\":\"{variant}\"";
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"ScanOrganic","ScanType":"Log",
             "Genus":"$Codex_Ent_Bacterium_Genus_Name;","Genus_Localised":"Bacterium",
             "Species":"$Codex_Ent_Bacterium_Name;","Species_Localised":"Bacterium Tela"{{variantFields}},
             "SystemAddress":{{Here}},"Body":3}
            """);

        return store;
    }

    private static (StarSystemPage Page, Window Window, List<CodexEntry> Opened) Shown(string? variant, params long[] pictured)
    {
        var files = new MemoryFileSystem();
        var region = GalacticRegions.IdOf(Region)!.Value;

        foreach (var entry in pictured)
        {
            files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, new CodexPictureKey(entry, region).FileName), Png(300, 200));
        }

        var store = new CodexPictureStore(files, Folder, () => true, NullLogger.Instance, new NotFound(), commander: () => "F1");
        var pictures = new CodexPictureSet(store, () => true, files, @"C:\d47\codex-placeholders");

        var game = Store(variant);
        var surface = new StarSystemSurface(
            new Systems(), () => new JournalLocation("Vask Reach", null, false, null) { SystemAddress = Here })
        {
            Biology = (address, at, held) => BiologyInSystem.For(game.Active!, address, at, held, null),
            Surveyed = new SurveyedBiology(),
            Heads = new BiologyHeads(),
        };

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(TestSurface.Settings());

        var opened = new List<CodexEntry>();
        var page = new StarSystemPage(surface) { Codex = new BodyCodex(CodexCatalogue.Table, pictures, opened.Add, _ => { }) };
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Until(() => page.GetVisualDescendants().OfType<Segment>().Any());
        page.GetVisualDescendants().OfType<Segment>().Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Sections")
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(4).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        return (page, window, opened);
    }

    private static void Until(Func<bool> done)
    {
        var stop = DateTime.UtcNow.AddSeconds(10);

        while (!done() && DateTime.UtcNow < stop)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }

    private static Border Row(Control within, string genus) =>
        within.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(StarSystemPage.GenusRowClass) && Avalonia.Automation.AutomationProperties.GetName(border) == genus);

    private static Button? PictureButton(Control within, string genus) =>
        Row(within, genus).GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Name == StarSystemPage.SpeciesPictureButtonName);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static SpeciesPictureDialog? Dialog(Visual from) =>
        TopLevel.GetTopLevel(from) is Window window
            ? window.GetVisualDescendants().OfType<SpeciesPictureDialog>().SingleOrDefault()
            : null;

    private static string TextOf(Control within, string name) =>
        within.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == name).Text!;

    [AvaloniaFact]
    public void PressingAPictureOpensTheDialogWithTheSpeciesTitle()
    {
        var (page, window, _) = Shown("Bacterium Tela - Orange", Orange);

        Assert.Null(Dialog(page));

        Press(PictureButton(page, "Bacterium")!);

        var dialog = Dialog(page)!;
        Assert.Equal("BACTERIUM TELA", TextOf(dialog, "ModalTitle"));
        Assert.Equal("BACTERIUM · VASK REACH 3", TextOf(dialog, "ModalContext"));

        var image = dialog.GetVisualDescendants().OfType<Image>().Single(item => item.Name == SpeciesPictureDialog.PictureName);
        Assert.Equal(Avalonia.Media.Stretch.Uniform, image.Stretch);
        Assert.Equal(600, image.Width);
        Assert.Equal(400, image.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void ARowWithNoPictureHasNoButton()
    {
        var (page, window, _) = Shown("Bacterium Tela - Orange");

        Assert.Null(PictureButton(page, "Bacterium"));
        Assert.Null(PictureButton(page, "Tussock"));

        window.Close();
    }

    [AvaloniaFact]
    public void CodexInTheDialogOpensTheEntryAndClosesTheDialog()
    {
        var (page, window, opened) = Shown("Bacterium Tela - Orange", Orange);
        Press(PictureButton(page, "Bacterium")!);

        var codex = Dialog(page)!.GetVisualDescendants().OfType<Button>().Single(button => button.Name == BodyCodex.LinkName);
        Press(codex);

        Assert.Equal([Orange], opened.Select(entry => entry.EntryId));
        Assert.Null(Dialog(page));

        window.Close();
    }

    [AvaloniaFact]
    public void TheLikelyColourSubtitleShowsOnlyWhenTheVariantIsNotKnown()
    {
        var (known, knownWindow, _) = Shown("Bacterium Tela - Orange", Orange, Magenta);
        Press(PictureButton(known, "Bacterium")!);

        Assert.DoesNotContain(
            Dialog(known)!.GetVisualDescendants().OfType<TextBlock>(), block => block.Name == "ModalSubtitle");
        knownWindow.Close();

        var (unknown, unknownWindow, _) = Shown(null, Magenta);
        Press(PictureButton(unknown, "Bacterium")!);

        Assert.Equal(SpeciesPictureDialog.LikelyColour, TextOf(Dialog(unknown)!, "ModalSubtitle"));
        unknownWindow.Close();
    }
}
