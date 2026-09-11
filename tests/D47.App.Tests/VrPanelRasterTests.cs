using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>What the headset is actually handed.</summary>
public class VrPanelRasterTests
{
    /// <summary>Not blank.</summary>
    [AvaloniaFact]
    public void TheHeadsetIsHandedPixelsRatherThanAnEmptyQuad()
    {
        var (surface, width, height) = Rasterised(out var buffer);
        var opaque = 0;

        unsafe
        {
            var bytes = (byte*)buffer.Address;

            // Every fourth byte is alpha in Bgra8888.
            for (var i = 3; i < width * height * 4; i += 4)
            {
                if (bytes[i] != 0)
                {
                    opaque++;
                }
            }
        }

        Assert.Equal(width * height, opaque);
        Assert.Equal((1024, 640), surface.Size);
    }

    /// <summary>And the pixels are the panel rather than a filled rectangle.</summary>
    [AvaloniaFact]
    public void WhatItDrawsIsThePanelAndNotJustItsBackground()
    {
        var (_, width, height) = Rasterised(out var buffer);
        var distinct = new HashSet<uint>();

        unsafe
        {
            var pixels = (uint*)buffer.Address;

            for (var i = 0; i < width * height; i++)
            {
                distinct.Add(pixels[i]);

                if (distinct.Count > 8)
                {
                    break;
                }
            }
        }

        // A background and nothing else is one or two colours.
        Assert.True(
            distinct.Count > 8,
            $"The submitted buffer has only {distinct.Count} distinct colours, which is a "
            + "blank quad rather than a rendered panel.");
    }

    /// <summary>
    /// The real panel, through the real buffer, arriving in the channel order the runtime reads — the
    /// whole path from a styled widget tree to the bytes <c>SetOverlayRaw</c> takes, with the
    /// conversion in the middle of it.
    /// </summary>
    [AvaloniaFact]
    public void WhatTheRuntimeIsHandedIsThePanelInRgba()
    {
        var (_, width, height) = Rasterised(out var buffer);

        var drawn = new byte[width * height * 4];
        Marshal.Copy(buffer.Address, drawn, 0, drawn.Length);

        buffer.ToRgba();

        var handed = new byte[drawn.Length];
        Marshal.Copy(buffer.Address, handed, 0, handed.Length);

        var accent = 0;

        for (var p = 0; p < width * height; p++)
        {
            var at = p * 4;

            Assert.Equal(drawn[at + 2], handed[at]);
            Assert.Equal(drawn[at + 1], handed[at + 1]);
            Assert.Equal(drawn[at], handed[at + 2]);
            Assert.Equal(drawn[at + 3], handed[at + 3]);

            // Red well clear of blue, in the first byte, where RGBA says red lives.
            if (handed[at] > 160 && handed[at + 2] < 80)
            {
                accent++;
            }
        }

        Assert.True(
            accent > 100,
            $"Only {accent} pixels of the submitted buffer read as the theme's orange accent, "
            + "which is what a panel handed over with red and blue swapped looks like.");
    }

    /// <summary>
    /// A surface starts dirty, or the first frame is shown before anything has ever been drawn into its
    /// texture: the runtime submits only when the source says it has changed.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceHasSomethingToDrawBeforeItIsEverAsked()
    {
        var (settings, _, _) = TestSurface.Create();

        Assert.True(new VrPanelSurface(new PanelViewModel(), settings, _ => null).IsDirty);
    }

    private const string Depot =
        """
        { "timestamp":"2026-08-25T10:00:00Z", "event":"ColonisationConstructionDepot",
          "MarketID":3960809986, "ConstructionProgress":0.25,
          "ConstructionComplete":false, "ConstructionFailed":false,
          "ResourcesRequired":[
            { "Name":"$steel_name;", "Name_Localised":"Steel",
              "RequiredAmount":300, "ProvidedAmount":0, "Payment":5000 } ] }
        """;

    /// <summary>Everything a headset copy needs to carry Sourcing: a registry, a board, and a build to show.</summary>
    private static (
        VrPanelSurface Surface,
        CarrierManifest Carrier,
        D47.Core.Journal.CommanderGameState? GameState) WithSourcing()
    {
        var (settings, _, paths) = TestSurface.Create();
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-25T09:30:00Z","event":"Docked","StationName":"Ratraii Construction Site","StarSystem":"Ratraii","MarketID":3960809986}""",
                     Depot.ReplaceLineEndings(" "),
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active;
        var board = new SourcingBoard();
        var carrier = new CarrierManifest(
            Path.Combine(paths.Data, "carrier.json"), NullLogger<CarrierManifest>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            ColonisationCapability.Create(
                () => live,
                null,
                settings,
                new NoopTrade(),
                carrier,
                board,
                () => new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)),
        ]);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var model = new PanelViewModel();

        var surface = new VrPanelSurface(
            model,
            settings,
            _ => null,
            checklists: checklists,
            gameState: () => live,
            capabilities: registry,
            sourcingBoard: board,
            carrier: carrier);

        return (surface, carrier, live);
    }

    private sealed class NoopTrade : D47.Core.Knowledge.ITradePlanService
    {
        public Task<D47.Core.Knowledge.TradeRoute?> PlanAsync(
            D47.Core.Knowledge.TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<D47.Core.Knowledge.TradeRoute?>(null);

        public Task<D47.Core.Knowledge.CommodityAnswer> FindCommodityAsync(
            D47.Core.Knowledge.CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(D47.Core.Knowledge.CommodityAnswer.Empty);

        public Task<D47.Core.Knowledge.SourcingAnswer> SourceConstructionAsync(
            D47.Core.Knowledge.SourcingSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(D47.Core.Knowledge.SourcingAnswer.Empty);

        public Task<D47.Core.Knowledge.StationQuote?> QuoteAsync(
            long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<D47.Core.Knowledge.StationQuote?>(null);
    }

    /// <summary>The root reaches the headset the same way it reaches the window (#54).</summary>
    [AvaloniaFact]
    public void SourcingIsTheChecklistTabsSecondRootOnTheHeadset()
    {
        var (surface, _, _) = WithSourcing();

        Assert.Contains(
            surface.Nav.Roots(D47.Core.Interface.PanelTab.Checklist),
            root => root.Key == SourcingPage.RootKey && root.Word == "Sourcing");
    }

    /// <summary>
    /// A carrier figure already told, drawn into the headset's own copy of the page — the half of #54
    /// that never touches a keyboard at all.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetRastersTheSourcingPageWithACarrierFigureTyped()
    {
        var (surface, carrier, gameState) = WithSourcing();

        carrier.Set(gameState?.Identity.FrontierId, "Steel", 150, DateTimeOffset.Now);

        Assert.True(surface.Nav.SelectRoot(D47.Core.Interface.PanelTab.Checklist, SourcingPage.RootKey));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var (width, height) = surface.Size;
        var buffer = new VrPixels(width, height);
        surface.Draw(buffer.Address, buffer.RowBytes);

        var distinct = new HashSet<uint>();

        unsafe
        {
            var pixels = (uint*)buffer.Address;

            for (var i = 0; i < width * height; i++)
            {
                distinct.Add(pixels[i]);

                if (distinct.Count > 8)
                {
                    break;
                }
            }
        }

        Assert.True(
            distinct.Count > 8,
            $"The submitted buffer has only {distinct.Count} distinct colours, which is a blank quad "
            + "rather than a rendered Sourcing page.");
    }

    /// <summary>
    /// The board a ray press on a plain text box opens, with a value spelled onto it rather than
    /// rayed key by key (#51).
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetRastersTheDrawnBoardWithASpelledValueShowing()
    {
        var (surface, _, _) = WithSourcing();

        Assert.True(surface.Nav.SelectRoot(D47.Core.Interface.PanelTab.Checklist, SourcingPage.RootKey));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var (width, height) = surface.Size;
        var buffer = new VrPixels(width, height);

        // Laid out, so the box has a place on the quad to be pointed at.
        surface.Draw(buffer.Address, buffer.RowBytes);

        var box = surface.Board.View.GetVisualDescendants().OfType<TextBox>().First(text => !text.IsReadOnly);
        var centre = box.TranslatePoint(new Point(box.Bounds.Width / 2, box.Bounds.Height / 2), surface.Board.View);

        Assert.NotNull(centre);
        Assert.True(surface.Press((float)(centre!.Value.X / width), (float)(centre.Value.Y / height)));
        Assert.True(surface.Board.IsListening);

        surface.Board.Hear(new D47.Core.Interface.Heard("alpha bravo seven", 1, Final: true));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            "ab7",
            surface.Board.View.GetVisualDescendants().OfType<TextBox>().First(text => text.IsReadOnly).Text);

        surface.Invalidate();
        surface.Draw(buffer.Address, buffer.RowBytes);

        var distinct = new HashSet<uint>();

        unsafe
        {
            var pixels = (uint*)buffer.Address;

            for (var i = 0; i < width * height; i++)
            {
                distinct.Add(pixels[i]);

                if (distinct.Count > 8)
                {
                    break;
                }
            }
        }

        Assert.True(
            distinct.Count > 8,
            $"The submitted buffer has only {distinct.Count} distinct colours, which is a blank quad "
            + "rather than a rendered keyboard.");
    }

    /// <summary>
    /// Rasterises through the same call the runtime makes, into the same buffer the runtime hands
    /// OpenVR, so nothing about the real path is stubbed out.
    /// </summary>
    private static (VrPanelSurface Surface, int Width, int Height) Rasterised(out VrPixels buffer)
    {
        var (settings, _, _) = TestSurface.Create();

        // Full, said out loud.
        settings.Apply(
            D47.Core.Capabilities.Builtin.VrCapability.ModeKey,
            "full",
            D47.Core.Configuration.SettingsCaller.Panel);

        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(D47.Core.Interface.ThemeCatalog.Elite);

        var model = new PanelViewModel();
        model.Append("Holding in normal space over HIP 12099 1 b.");
        model.TurnLine = "routed: model";

        var surface = new VrPanelSurface(model, settings, _ => null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var (width, height) = surface.Size;

        buffer = new VrPixels(width, height);
        surface.Draw(buffer.Address, buffer.RowBytes);

        return (surface, width, height);
    }
}
