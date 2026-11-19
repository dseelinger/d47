using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Discoveries for Inner Orion Spur lists the shipped star and planet kinds under Astronomical Bodies with a
/// "Reported in" count and their family placeholder, and a star the Commander logged there in the codex is not counted
/// as an entry the table lacks.
/// </summary>
[Trait("Category", "Integration")]
public class TheDiscoveriesListStarsAndPlanetsTests
{
    private const long NeutronStar = 1102300;

    private const long GTypeStar = 1100501;

    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Placeholders = @"C:\d47\codex-placeholders";

    private static string Codex(long id, string sub) =>
        "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_StellarBodies;\",\"Category_Localised\":\"Astronomical Bodies\""
        + ",\"SubCategory\":\"$Codex_SubCategory_Stars;\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"$Codex_RegionName_18;\""
        + ",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":0,\"IsNewEntry\":true}";

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static void Dispatch() => Dispatcher.UIThread.RunJobs();

    private static void PickStatus(Control page, int option)
    {
        In<Button>(page).First(button => button.Name == "CodexDiscoveryToken.Status").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatch();
        In<Segment>(page).Single().GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatch();
    }

    private static Button? Row(Control page, string key) =>
        In<Button>(page).SingleOrDefault(button => button.Name == "CodexMissing." + key);

    private static CodexPictureSet Pictures()
    {
        var files = new MemoryFileSystem();
        var assets = Path.Combine(AppSource.RepositoryRoot(), "assets", "codex-placeholders");

        foreach (var file in new[] { "stars.jpg", "terrestrial-planets.jpg", "gas-giant-planets.jpg" })
        {
            files.WriteBytes(Path.Combine(Placeholders, file), File.ReadAllBytes(Path.Combine(assets, file)));
        }

        var store = new CodexPictureStore(files, Folder, () => false, NullLogger.Instance, new HeldRelease(), commander: () => "F1");

        return new CodexPictureSet(store, () => false, files, Placeholders);
    }

    [AvaloniaFact]
    public void InnerOrionSpurListsStarAndPlanetKindsAndALoggedStarIsNotUnlisted()
    {
        var look = AppLook.Put();
        var state = JournalScript.Commander(
            "F735466",
            "Test Commander",
            "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
            Codex(NeutronStar, "Stars"),
            Codex(GTypeStar, "Stars"));
        var page = new CodexDiscoveriesPage(() => state, () => HistoryState.Done, 18, pictures: Pictures());
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatch();

        PickStatus(page, 1);

        Assert.All(new[] { NeutronStar, GTypeStar }, id => Assert.Null(Row(page, id.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        Assert.DoesNotContain(In<TextBlock>(page), block => block.Text?.Contains("that the codex table does not list", StringComparison.Ordinal) == true);

        PickStatus(page, 2);

        var first = In<Button>(page).First(button => button.Name?.StartsWith("CodexMissing.", StringComparison.Ordinal) == true);
        var entry = CodexCatalogue.Table.Entry(first.Name!["CodexMissing.".Length..]);

        Assert.Equal("Astronomical Bodies", entry!.Category);
        Assert.NotNull(entry.Kind);
        Assert.Equal(
            CodexCatalogue.Table.InRegion(18).Where(found => found.Entry.Category == "Astronomical Bodies").Max(found => found.Systems),
            entry.Regions[18]);
        Assert.Contains(In<Image>(first), image => image.Name == "CodexThumbPlaceholder" && image.Source is not null);

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-discoveries-stars-and-planets.png");
        }

        window.Close();
    }
}
