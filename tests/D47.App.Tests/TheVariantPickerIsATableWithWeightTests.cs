using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.TestSupport;
using Xunit;

namespace D47.App.Tests;

/// <summary>The "Which one" page draws a module's variants as a table with mass, power and price (#2640).</summary>
public class TheVariantPickerIsATableWithWeightTests
{
    private const string Scoop = "int_fuelscoop_size7_class5";

    private const string Drive = "int_hyperdrive_size5_class5";

    private static IReadOnlyList<ModuleSpecification> Variants(string name) =>
    [
        .. EliteSpecifications.Modules.Where(module => module.Name == name),
    ];

    private static string[] Labels(IReadOnlyList<EntryColumn> columns) => [.. columns.Select(column => column.Label)];

    private static string Cell(IReadOnlyList<EntryColumn> columns, string label, string symbol) =>
        columns.Single(column => column.Label == label).Text(symbol);

    [Fact]
    public void AFuelScoopShowsItsPowerAndPriceAndAMassOfZero()
    {
        var columns = ShipsMode.VariantColumns(Variants("Fuel Scoop"));

        Assert.Equal(["Module", "Code", "Mass", "Power", "Price"], Labels(columns));
        Assert.Equal("7A", Cell(columns, "Code", Scoop));
        Assert.Equal("0.0 t", Cell(columns, "Mass", Scoop));
        Assert.Equal("0.97 MW", Cell(columns, "Power", Scoop));
        Assert.Equal("91,180,644 cr", Cell(columns, "Price", Scoop));
    }

    [Fact]
    public void EveryFuelScoopWeighsNothing()
    {
        var columns = ShipsMode.VariantColumns(Variants("Fuel Scoop"));

        Assert.All(Variants("Fuel Scoop"), scoop => Assert.Equal("0.0 t", Cell(columns, "Mass", scoop.Symbol)));
    }

    [Fact]
    public void AFrameShiftDrivePageAddsTheDriveColumnsAndAScoopPageDoesNot()
    {
        var drives = ShipsMode.VariantColumns(Variants("Frame Shift Drive"));

        Assert.Equal(["Module", "Code", "Mass", "Power", "Price", "Optimal mass", "Max fuel per jump"], Labels(drives));
        Assert.Equal("1050 t", Cell(drives, "Optimal mass", Drive));
        Assert.Equal("5 t", Cell(drives, "Max fuel per jump", Drive));
        Assert.DoesNotContain("Optimal mass", Labels(ShipsMode.VariantColumns(Variants("Fuel Scoop"))));
    }

    [Fact]
    public void MassSortsByFigureAndAMissingMassHasNoFigure()
    {
        const string Unmeasured = "int_hyperdrive_size8_class1";
        var columns = ShipsMode.VariantColumns(Variants("Frame Shift Drive"));
        var mass = columns.Single(column => column.Label == "Mass");

        Assert.Null(mass.Figure!(Unmeasured));
        Assert.Equal("—", Cell(columns, "Mass", Unmeasured));
    }

    [Fact]
    public void ASearchMatchesAnyCell()
    {
        var columns = ShipsMode.VariantColumns(Variants("Fuel Scoop"));

        Assert.True(PanelPrompts.PickTable.Matches(Scoop, "91,180,644", columns));
        Assert.False(PanelPrompts.PickTable.Matches(Scoop, "no such figure", columns));
    }

    [AvaloniaFact]
    public void ThePageDrawsAHeaderAndPressingARowPicksIt()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        var layer = new Avalonia.Controls.Panel();
        var prompts = new PanelPrompts(nav, layer);
        var variants = Variants("Fuel Scoop");

        ChoiceOption[] options =
        [
            new(string.Empty, "Any Fuel Scoop — I do not mind which") { Pinned = true },
            .. variants.Select(variant => new ChoiceOption(variant.Symbol, variant.Name + " " + variant.Size)),
        ];

        ChoiceOption? picked = null;

        prompts.Choose(
            new ChoiceRequest("loadout.variant", "Which one", "Which Fuel Scoop?", null, options, null, ChoiceSurface.Layer)
            {
                Searchable = true,
                Columns = ShipsMode.VariantColumns(variants),
            },
            option => picked = option);

        var window = new Window { Content = layer, Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var header = layer.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.IsHeader);
        Assert.Equal(["Module", "Code", "Mass", "Power", "Price"], header.Layout.Columns.Select(column => column.Key));

        var rows = layer.GetVisualDescendants().OfType<Button>().Where(button => button.Content is TableGrid).ToList();
        Assert.Equal(variants.Count, rows.Count);

        rows[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.NotNull(picked);
        Assert.Equal(variants[0].Symbol, picked!.Key);
        window.Close();
    }
}
