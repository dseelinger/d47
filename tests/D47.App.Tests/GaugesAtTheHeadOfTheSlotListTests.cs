using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Power and jump range at the head of a ship's slot list, and the coin on a module a pledge is
/// needed to buy (list.md Phase 38, "A build you can watch").
/// <para>
/// The arithmetic is <c>D47.Core.Tests.Ships.BuildGaugeTests</c>'s; this is about what a Commander
/// sees — that the bars are drawn where the item says, that a modelled figure looks different from
/// a measured one, and that the coin renders as a coin rather than as a box.
/// </para>
/// </summary>
public class GaugesAtTheHeadOfTheSlotListTests
{
    private sealed record Surface(
        Window Window, PanelView Panel, ShipPlanService Ships, ChecklistService Checklists);

    private static Surface Open(bool pledged = false)
    {
        var root = TempFolders.Create("d47-gauge-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        var state = Flying(pledged);
        var ships = new ShipPlanService(store, checklists, () => state);

        // The Elite palette, because the gauges are drawn in themed colours and an unthemed run
        // paints them with a null brush — which looks exactly like a bar that failed to lay out.
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };

        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, checklists);
    }

    /// <summary>
    /// A Cobra Mk V the Commander is sitting in: a plant, a drive, a hardpoint and a shield
    /// booster, so both halves of the power split have something to be about.
    /// </summary>
    private static CommanderGameState Flying(bool pledged)
    {
        var store = new GameStateStore();

        var lines = new List<string>
        {
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":12,"ShipName":"Predestinatio","MaxJumpRange":30.0,"CargoCapacity":32,"UnladenMass":190.0,"FuelCapacity":{"Main":16.0,"Reserve":0.49},"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":1,"Health":1.0},{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size4_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"MediumHardpoint1","Item":"hpt_multicannon_fixed_medium","On":true,"Priority":1,"Health":1.0},{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":1,"Health":1.0},{"Slot":"FuelTank","Item":"int_fueltank_size4_class3","On":true,"Priority":1,"Health":1.0},{"Slot":"Slot01_Size5","Item":"int_cargorack_size5_class1","On":true,"Priority":1,"Health":1.0}]}""",
        };

        if (pledged)
        {
            lines.Add(
                """{"timestamp":"2026-08-20T09:00:00Z","event":"Powerplay","Power":"Aisling Duval","Rank":3,"Merits":100,"TimePledged":900}""");
        }

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static Button Row(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == label
                             || button.GetVisualDescendants().OfType<TextBlock>()
                                 .Any(text => text.Text == label));

    private static IReadOnlyList<string> Said(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    private static void OpenTheShip(Surface surface)
    {
        Row(surface.Panel, "Predestinatio (Cobra MkV)")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void BothGaugesAreDrawnAboveTheSlots()
    {
        var surface = Open();

        OpenTheShip(surface);

        var said = Said(surface.Panel);

        Assert.Contains(said, text => text == "Power");
        Assert.Contains(said, text => text == "Jump range");

        // The figures a Commander was alt-tabbing to Coriolis for: megawatts against the plant,
        // and a range rather than a number.
        Assert.Contains(said, text => text.Contains(" MW of ", StringComparison.Ordinal));
        Assert.Contains(said, text => text.Contains(" ly", StringComparison.Ordinal) && text.Contains('–'));

        // And the retracted figure beside the deployed one, which is the whole point of the split.
        Assert.Contains(said, text => text.Contains("retracted", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheGaugesSitAboveTheFirstSlotRatherThanBelowIt()
    {
        var surface = Open();

        OpenTheShip(surface);

        var power = surface.Panel.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Text == "Power");

        var slot = Row(surface.Panel, "Power Plant");

        // "At the head of the slot list" is a position, so it is asserted as one.
        Assert.True(
            power.TranslatePoint(default, surface.Panel)!.Value.Y
            < slot.TranslatePoint(default, surface.Panel)!.Value.Y);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AModelledFigureIsMarkedAndAMeasuredOneIsNot()
    {
        var surface = Open();

        OpenTheShip(surface);

        Assert.DoesNotContain(Said(surface.Panel), text => text.StartsWith("~ ", StringComparison.Ordinal));

        // Plan a roll, and the gauges become figures d47 worked out rather than read.
        var build = surface.Ships.Fleet().First(entry => entry.Build is not null).Build!;

        surface.Ships.Plan(build.Id, new SlotPlan("PowerPlant", "Armoured", 5, "Hera Tani"));
        Dispatcher.UIThread.RunJobs();

        // **The condition on which these gauges may show planned figures at all.** A modelled
        // reading must never look like a measured one.
        Assert.Contains(Said(surface.Panel), text => text.StartsWith("~ ", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AShipD47HasNotBeenInsideSaysSoWhereTheGaugesWouldBe()
    {
        var surface = Open();

        surface.Ships.Intend("Anaconda");
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Anaconda").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Said(surface.Panel), text => text == ShipGauges.Unseen);
        Assert.DoesNotContain(Said(surface.Panel), text => text.Contains(" MW of ", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ThePledgeCoinRendersAsACoinRatherThanAsTofu()
    {
        // **The one failure only an eye catches**, and Phase 37 already recorded it costing time:
        // a glyph the shipped font lacks draws as a box, and a box beside a module name reads as a
        // rendering bug rather than as a gate.
        //
        // Asserted by drawing rather than by asking the font, because drawing is the thing that
        // can go wrong. A codepoint in the private use area is certainly absent from any font, so
        // it rasterises as whatever this font's "I do not have that" looks like — and if the coin
        // were missing too, the two pictures would be identical. They are not.
        var coin = Ink(ShipsMode.Coin);
        var missing = Ink("");

        Assert.NotEmpty(coin);
        Assert.True(coin.Any(lit => lit), "the coin drew no ink at all");
        Assert.NotEqual(missing, coin);
    }

    /// <summary>One short string, rendered at the size a slot row draws it, as a map of lit pixels.</summary>
    private static bool[] Ink(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = Brushes.White,
            Background = Brushes.Black,
            Width = 24,
            Height = 24,
        };

        var size = new PixelSize(24, 24);

        block.Measure(size.ToSize(1));
        block.Arrange(new Rect(size.ToSize(1)));

        using var frame = new Avalonia.Media.Imaging.RenderTargetBitmap(size);

        frame.Render(block);

        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];

        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                frame.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), (IntPtr)buffer, pixels.Length, stride);
            }
        }

        return [.. Enumerable.Range(0, size.Width * size.Height).Select(at => pixels[at * 4] > 128)];
    }

    [AvaloniaFact]
    public void AModuleBehindAPledgeCarriesTheCoinOnItsRow()
    {
        var surface = Open();

        OpenTheShip(surface);

        var build = surface.Ships.Fleet().First(entry => entry.Build is not null).Build!;

        // A Prismatic Shield Generator: one of the nineteen, and the eight sizes of it are the
        // whole of the family that is gated.
        surface.Ships.Plan(
            build.Id,
            new SlotPlan("Slot02_Size4", Module: "Prismatic Shield Generator")
            {
                Variant = "int_shieldgenerator_size4_class5_strong",
            });

        Dispatcher.UIThread.RunJobs();

        var coined = surface.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.Inlines is not null)
            .SelectMany(block => block.Inlines!)
            .OfType<Run>()
            .Any(run => run.Text == $" {ShipsMode.Coin}");

        Assert.True(coined);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheWaitingQuestionIsLeftOnTheTabAsABanner()
    {
        var surface = Open();

        // Opening the ship is what gives it a build to plan against.
        OpenTheShip(surface);

        var build = surface.Ships.Fleet().First(entry => entry.Build is not null).Build!;

        surface.Ships.Plan(build.Id, new SlotPlan("PowerPlant", "Armoured", 5, "Hera Tani"));

        // Boarding is what asks. The question goes out loud at that moment; this is the half that
        // is still there an hour later for a Commander who was busy flying.
        var drift = new ShipDriftWatch(surface.Ships, surface.Checklists);

        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-08-20T09:05:00Z","event":"Loadout","Ship":"cobramkv","ShipID":12,"MaxJumpRange":30.0,"UnladenMass":190.0,"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":1,"Health":1.0}]}""",
            NullLogger.Instance,
            out var boarding));

        Assert.NotNull(drift.Observe([boarding!]));

        OpenTheShip(surface);

        Assert.Contains(Said(surface.Panel), text => text.StartsWith("Set the", StringComparison.Ordinal));

        // Two answers and no third: dismissing a question is saying no while pretending not to.
        var no = surface.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "No, leave both alone"));

        no.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Checklists.Proposals.Pending);
        Assert.Empty(surface.Checklists.Document.Items);
        Assert.DoesNotContain(Said(surface.Panel), text => text == "No, leave both alone");

        surface.Window.Close();
    }

    /// <summary>The gauges at the size the headset renders the panel, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheGaugesRenderToACapture()
    {
        var surface = Open();

        OpenTheShip(surface);

        var build = surface.Ships.Fleet().First(entry => entry.Build is not null).Build!;

        surface.Window.Width = 1024;
        surface.Window.Height = 640;

        Dispatcher.UIThread.RunJobs();

        // Measured first — every figure read off the game — because that is what the two kinds
        // being distinguishable is asserted against by eye.
        surface.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "loadout-gauges-measured.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        // One planned roll and one gated module, so a capture shows both the modelled hue and the
        // coin beside a measured row.
        surface.Ships.Plan(build.Id, new SlotPlan("PowerPlant", "Armoured", 5, "Hera Tani"));

        surface.Ships.Plan(
            build.Id,
            new SlotPlan("Slot02_Size4", Module: "Prismatic Shield Generator")
            {
                Variant = "int_shieldgenerator_size4_class5_strong",
            });

        Dispatcher.UIThread.RunJobs();

        surface.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "loadout-gauges-modelled.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AQuestionAboutSomethingElseDoesNotLandOnTheShipsTab()
    {
        var surface = Open();

        OpenTheShip(surface);

        // An engineer-unlock chain proposes plan items from the same source and scopes them to the
        // universal list. That is the Engineers tab's business: a question about unlocking Felicity
        // Farseer has no place at the head of a Cobra's slots.
        var said = surface.Checklists.ProposePlan(
            ChecklistScope.Universal,
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Universal,
                hull: null,
                [new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer")],
                surface.Checklists.SlotFor),
            ["MainEngines"]);

        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Checklists.Proposals.Pending.Count > 0, said);
        Assert.DoesNotContain(Said(surface.Panel), text => text == "Yes, revise my checklist");

        surface.Window.Close();
    }
}
