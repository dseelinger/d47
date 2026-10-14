using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The D47 page a switch position opens is picked from rows grouped under their tab (#1151).</summary>
[Trait("Category", "Integration")]
public class ThePageAPositionOpensIsPickedByTabTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static readonly NavCrumb Stories = new("stories", "Stories");

    private static readonly NavCrumb Catalogue = new("stories.catalogue", "Catalogue");

    private static List<PanelDestination> Destinations() =>
    [
        new(PanelTab.Transcript, new NavCrumb("transcript.technical", "Technical")),
        new(PanelTab.Stories, Stories),
        new(PanelTab.Stories, Stories, Catalogue),
        new(PanelTab.Stories, Stories, MessagesView.Crumb),
        new(PanelTab.Assets, new NavCrumb("assets.ships", "Ships")),
        new(PanelTab.Settings, new NavCrumb("settings", "Settings")),
    ];

    private sealed record Shown(SwitchPage Page, Window Window, PanelView Panel);

    private static Shown Open(string destination)
    {
        var root = TempFolders.Create("d47-switch-picker");
        var store = new SwitchStore(
            Path.Combine(root, "switches.json"), new D47.Core.Storage.DiskFileSystem(), NullLogger<SwitchStore>.Instance);

        store.Save(
        [
            new SwitchMapping
            {
                Name = "WING FOLD",
                DeviceId = "stick-1",
                Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes",
                Positions =
                [
                    new SwitchPosition(4, null, DesiredState.On, destination.Length == 0 ? null : destination),
                    new SwitchPosition(5, "landing_gear", DesiredState.On),
                ],
            },
        ]);
        store.Poll();

        var page = new SwitchPage(
            store,
            new FakeHotasReader(),
            new SwitchReconciler(NullLogger<SwitchReconciler>.Instance),
            () => Instant,
            Path.Combine(root, "capture.txt"),
            Destinations());

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1024, Height = 900 };

        window.Show();
        _ = panel.Open(page);
        Jobs();

        return new Shown(page, window, panel);
    }

    private static Button PageButton(Shown shown) =>
        shown.Page.GetVisualDescendants().OfType<Button>().First(button => button.Name == SwitchPage.PageButtonName);

    private static PickerPage OpenPicker(Shown shown)
    {
        Press(PageButton(shown));

        return shown.Panel.GetVisualDescendants().OfType<PickerPage>().Single();
    }

    private static List<PickerChoice> Rows(PickerPage picker) =>
        [.. picker.GetControl<ListBox>("Choices").Items.OfType<PickerChoice>()];

    [AvaloniaFact]
    public void EveryDestinationIsListedUnderItsTabWithTheSubPageIndentedBeneathItsRoot()
    {
        using var look = AppLook.Put();
        var shown = Open(string.Empty);
        var rows = Rows(OpenPicker(shown));

        Assert.Equal(
            ["Nothing", "Technical", "Stories", "Catalogue", "Story log", "Ships", "Settings"],
            rows.Select(row => row.Text));
        Assert.Equal(
            [null, "TRANSCRIPT", "STORIES", null, null, "ASSET MGMT", "SETTINGS"],
            rows.Select(row => row.Head));
        Assert.Equal(["1", "3", "1", "1"], rows.Where(row => row.Head is not null).Select(row => row.HeadCount));
        Assert.Equal([false, false, false, true, true, false, false], rows.Select(row => row.Nested));
        Assert.Equal(
            [null, null, null, null, "Only while a story is running", null, null],
            rows.Select(row => row.Detail));

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void FilteringBySubPageWordKeepsItAndItsGroupHeadFollows()
    {
        using var look = AppLook.Put();
        var shown = Open(string.Empty);
        var picker = OpenPicker(shown);

        picker.GetControl<TextBox>("FilterBox").Text = "catalogue";
        Jobs();

        var rows = Rows(picker);
        var row = Assert.Single(rows);

        Assert.Equal("Catalogue", row.Text);
        Assert.Equal("STORIES", row.Head);
        Assert.Equal("1", row.HeadCount);

        var count = picker.GetControl<TextBlock>("MatchCount");

        Assert.True(count.IsVisible);
        Assert.Equal("1 OF 7 PAGES", count.Text ?? string.Concat(count.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text)));

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ChoosingASubPageSavesItsKeyAndTheRowAndReopenedPickerShowIt()
    {
        using var look = AppLook.Put();
        var shown = Open(string.Empty);
        var picker = OpenPicker(shown);
        var choices = picker.GetControl<ListBox>("Choices");

        choices.SelectedItem = Rows(picker).Single(row => row.Value == Catalogue.Key);
        Jobs();
        Press(picker.GetControl<Button>("AcceptButton"));

        Assert.Contains("Stories › Catalogue", string.Concat(PageButton(shown).GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)), StringComparison.Ordinal);

        var reopened = Rows(OpenPicker(shown));

        Assert.Equal(Catalogue.Key, Assert.Single(reopened, row => row.InUse).Value);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ASavedDestinationNoSurfaceOffersIsStillListedByItsKey()
    {
        using var look = AppLook.Put();
        var shown = Open("gone.page");
        var rows = Rows(OpenPicker(shown));

        var gone = Assert.Single(rows, row => row.Value == "gone.page");

        Assert.Equal("gone.page", gone.Text);
        Assert.Equal(1, rows.IndexOf(gone));
        Assert.True(gone.InUse);
        Assert.Null(gone.Head);

        shown.Window.Close();
    }
}
