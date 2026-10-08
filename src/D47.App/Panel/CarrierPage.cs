using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Where the Commander gets their carrier from, and a nudge when it changes.</summary>
public sealed class CarrierSource(
    Func<CarrierState> own,
    Func<CarrierState> squadron,
    Func<int> shipTritium,
    Func<string?>? here = null)
{
    /// <summary>Raised when the journal moved either carrier, or the ship's hold, on.</summary>
    public event Action? Changed;

    /// <summary>The Commander's own.</summary>
    public CarrierState Now => own();

    /// <summary>Their squadron's, if they are in a squadron that has one.</summary>
    public CarrierState Squadron => squadron();

    /// <summary>Tritium in the flown ship's hold, zero when the hold is the SRV's or is empty.</summary>
    public int ShipTritium => shipTritium();

    /// <summary>The system the Commander is in, or null when it is not known.</summary>
    public string? Here => here?.Invoke();

    public void Invalidate() => Changed?.Invoke();
}

/// <summary>The Commander's fleet carrier, on the tab named after it (#230).</summary>
public sealed class CarrierPage : UserControl, IPageChrome
{
    private readonly CarrierSource _carrier;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly Func<string?, bool>? _planRoute;
    private readonly StackPanel _body = new() { Spacing = 4 };
    private IDisposable? _sized;
    private const string CarrierHull = "fleetcarrier";

    private readonly bool _hullPictures;
    private bool _mini;

    public CarrierPage(
        CarrierSource carrier,
        Func<DateTimeOffset>? now = null,
        Func<string, Task<bool>>? copy = null,

        // The captain and tower's own settings, on the tab they only affect (#218, #305).
        Control? settingsStrip = null,

        // Opens Navigation › Plan's Carrier Route card with From set to the system given.
        Func<string?, bool>? planRoute = null,

        // Whether Hull pictures is on.
        bool hullPictures = false)
    {
        BarTool = PageChrome.ToolOf(settingsStrip);
        _hullPictures = hullPictures;
        _carrier = carrier;
        _planRoute = planRoute;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _copy = copy;

        carrier.Changed += OnChanged;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("where is my carrier");

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

    public Control? BarTool { get; }

    public string? HelpTopic => null;

    private void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _sized = this.GetSelfAndVisualAncestors()
            .OfType<PanelView>()
            .FirstOrDefault()
            ?.GetObservable(PanelView.ModeProperty)
            .Subscribe(new AnonymousObserver<PanelMode>(OnSurface));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _carrier.Changed -= OnChanged;
        _sized?.Dispose();
        _sized = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The surface went mini, or came back.</summary>
    private void OnSurface(PanelMode mode)
    {
        var mini = mode == PanelMode.Mini;

        if (mini != _mini)
        {
            _mini = mini;
            Refresh();
        }
    }

    /// <summary>Redraws against the live state.</summary>
    public void Refresh()
    {
        _body.Children.Clear();

        Own();
        Squadron();
    }

    private void Own()
    {
        var carrier = _carrier.Now;

        if (!carrier.Owned)
        {
            // Said rather than left blank, and it says which of the two it means: d47 has not seen one is a
            // different claim from the Commander not having one, and only the first is something d47 can
            // know.
            _body.Children.Add(LoadoutPages.Muted(
                "No carrier has turned up in the journal yet. If you own one, open its management "
                + "panel in the game and it will appear here."));

            return;
        }

        var upkept = CarrierUpkeep.Now(carrier, _now());

        _body.Children.Add(Title(carrier, Balance(upkept, carrier.Balance)));

        if (carrier.PendingDecommission)
        {
            _body.Children.Add(LoadoutPages.Toned("Booked for decommissioning.", ThemeManager.RedKey));
        }

        var tiles = new List<Control> { Where(carrier) };

        if (carrier.JumpRange is { } range)
        {
            tiles.Add(StatTile.Build("Jump range", $"{range:0.#} ly", StatInk.Number));
        }

        if (carrier.Capacity is { } capacity && carrier.FreeSpace is { } free)
        {
            tiles.Add(StatTile.Build(
                "Space",
                $"{capacity - free:N0} of {capacity:N0} t used, {free:N0} t free",
                StatInk.Number));
        }

        if (carrier.CargoTonnes is { } cargo)
        {
            // How much, never what.
            tiles.Add(StatTile.Build("Cargo", $"{cargo:N0} t", StatInk.Number));
        }

        if (upkept is { Weekly: { } weekly } balance)
        {
            tiles.Add(StatTile.Build("Upkeep", weekly.ToString("N0", CultureInfo.CurrentCulture) + " cr a week", StatInk.Number));
            tiles.Add(StatTile.Build("Covers", $"{balance.WeeksCovered:N0} weeks", StatInk.Number));
        }

        if (!string.IsNullOrWhiteSpace(carrier.DockingAccess))
        {
            tiles.Add(StatTile.Build("Docking", carrier.DockingAccess));
        }

        Services(carrier, tiles);

        var grid = StatTile.Grid(tiles, maxColumns: 3);

        _body.Children.Add(!_mini && _hullPictures ? HullPicture.For(CarrierHull, grid) : grid);

        if (_planRoute is { } planRoute && !carrier.IsSquadron)
        {
            var plan = LoadoutPages.Press("Plan a carrier route", () => planRoute(carrier.StarSystem));
            plan.Margin = new Thickness(0, 8, 0, 0);

            _body.Children.Add(plan);
        }

        Tritium(carrier);

        if (upkept is { Adjusted: true } adjusted)
        {
            var note = LoadoutPages.Muted($"Balance adjusted: {adjusted.Explained} cr.");
            note.Margin = new Thickness(0, 6, 0, 0);

            _body.Children.Add(note);
        }

        if (carrier.StatsSeenAt is { } seen)
        {
            var age = LoadoutPages.Muted(
                $"Figures as of {Ago(seen)}. They only refresh when you open the carrier "
                + "management panel in the game.");
            age.Margin = new Thickness(0, 6, 0, 0);

            _body.Children.Add(age);
        }
    }

    /// <summary>The squadron's carrier, under its own heading and only when there is one.</summary>
    private void Squadron()
    {
        var carrier = _carrier.Squadron;

        if (!carrier.Owned)
        {
            return;
        }

        _body.Children.Add(LoadoutPages.Section("Your squadron's carrier", compact: _mini));
        _body.Children.Add(LoadoutPages.SlotName(Named(carrier)));

        var tiles = new List<Control> { Where(carrier) };

        if (carrier.FuelLevel is { } fuel)
        {
            tiles.Add(StatTile.Build("Tritium", $"{fuel:N0} t", StatInk.Number));
        }

        if (!string.IsNullOrWhiteSpace(carrier.DockingAccess))
        {
            tiles.Add(StatTile.Build("Docking", carrier.DockingAccess));
        }

        var open = carrier.Services.Where(service => service.IsOpen).Select(Named).ToList();

        if (open.Count > 0)
        {
            tiles.Add(StatTile.Build("Services", string.Join(", ", open)));
        }

        _body.Children.Add(StatTile.Grid(tiles, maxColumns: 3));

        // No balance and no space.
        var theirs = LoadoutPages.Muted(
            "Your squadron's, not yours — shown so you know where it is and whether you can dock.");
        theirs.Margin = new Thickness(0, 6, 0, 0);

        _body.Children.Add(theirs);
    }

    private static string Named(CarrierState carrier) =>
        string.IsNullOrWhiteSpace(carrier.Name)
            ? carrier.CallSign ?? "Your carrier"
            : $"{carrier.Name} ({carrier.CallSign})";

    /// <summary>The carrier's balance, "about" when upkeep has been taken from the recorded figure.</summary>
    private static string? Balance(CarrierBalance? upkept, long? recorded) =>
        upkept is { } balance
            ? (balance.Adjusted ? "about " : string.Empty)
              + balance.Balance.ToString("N0", CultureInfo.CurrentCulture) + " cr"
            : recorded is { } known
                ? known.ToString("N0", CultureInfo.CurrentCulture) + " cr"
                : null;

    /// <summary>The carrier's name as the screen title block, with its balance as the figure.</summary>
    private Control Title(CarrierState carrier, string? balance)
    {
        var name = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Heading,
            TitleRank.Screen);

        TitleText.Show(name, Named(carrier));

        var title = TitleText.Block(name, figure: balance is null ? null : TitleText.Figure("Carrier balance", balance));
        title.Margin = new Thickness(0, 0, 0, _mini ? 4 : 10);

        return title;
    }

    /// <summary>Where it is, and where it is going if it has been told to go somewhere.</summary>
    private Control Where(CarrierState carrier)
    {
        if (carrier.DestinationSystem is not { Length: > 0 } destination)
        {
            return System("System", carrier.StarSystem ?? "not seen", carrier.StarSystem);
        }

        var parking = string.IsNullOrWhiteSpace(carrier.DestinationBody)
            ? destination
            : $"{destination}, at {carrier.DestinationBody}";

        if (carrier.DepartureTime is not { } departure)
        {
            return System("Jumping to", parking, destination);
        }

        // Counted against the clock the caller supplies rather than one read here, so a test can stand where
        // the Commander stands.
        var left = departure - _now();

        return System(
            "Jumping to",
            left > TimeSpan.Zero
                ? $"{parking} — leaves in {Left(left)}"
                : $"{parking} — leaving now",
            destination);
    }

    /// <summary>In the tank, in the carrier's hold, in the ship's hold, a total, and a rough range (#307).</summary>
    private void Tritium(CarrierState carrier)
    {
        _body.Children.Add(LoadoutPages.Section("Tritium", compact: _mini));

        var tiles = new List<Control>
        {
            StatTile.Build("In the tank", carrier.FuelLevel is { } fuel ? $"{fuel:N0} t" : "not seen", StatInk.Number),
        };

        if (carrier.TritiumInHold is { } hold)
        {
            var note = carrier.Hold.OrderOpen("tritium") ? "counted, may be off: a tritium order was open"
                : carrier.TritiumInHoldUncertain ? "counted, may be off: the count did not match the hold"
                : "counted";

            tiles.Add(StatTile.Build("Carrier's hold", $"{hold:N0} t ({note})", StatInk.Number));
        }

        var shipTritium = _carrier.ShipTritium;

        if (shipTritium > 0)
        {
            tiles.Add(StatTile.Build("Your ship's hold", $"{shipTritium:N0} t", StatInk.Number));
        }

        var total = (carrier.FuelLevel ?? 0) + (carrier.TritiumInHold ?? 0) + shipTritium;

        tiles.Add(StatTile.Build("Total", $"{total:N0} t", StatInk.Number));

        var usedSpace = carrier.Capacity is { } capacity && carrier.FreeSpace is { } free ? capacity - free : 0;
        var (rangeLy, jumps) = CarrierFuel.RoughRange(total, usedSpace, carrier.JumpRange);
        var distance = carrier.JumpRange ?? 500;

        tiles.Add(StatTile.Build(
            "Range, roughly",
            $"about {RoundToTwoSigFigs(rangeLy):N0} ly — {jumps} jump{(jumps == 1 ? "" : "s")} at {distance:0.#} ly",
            StatInk.Number));

        _body.Children.Add(StatTile.Grid(tiles, maxColumns: 3));
    }

    /// <summary>Two significant figures, the precision this estimate is worth.</summary>
    private static double RoundToTwoSigFigs(double value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)) - 1);

        return Math.Round(value / magnitude) * magnitude;
    }

    private static void Services(CarrierState carrier, List<Control> tiles)
    {
        if (carrier.Services.Count == 0)
        {
            return;
        }

        var open = carrier.Services.Where(service => service.IsOpen).Select(Named).ToList();

        tiles.Add(StatTile.Build(
            "Services",
            open.Count > 0 ? string.Join(", ", open) : "none switched on"));

        // Bought but switched off is worth saying on its own: it is the state a Commander can undo from the
        // management panel, and the one they are most likely not to have meant.
        var idle = carrier.Services
            .Where(service => service.Activated && !service.Enabled)
            .Select(Named)
            .ToList();

        if (idle.Count > 0)
        {
            tiles.Add(StatTile.Build("Switched off", string.Join(", ", idle)));
        }
    }

    /// <summary>Elite's role names, spaced out where they are run together.</summary>
    private static string Named(CarrierService service) => service.Role switch
    {
        "BlackMarket" => "Black market",
        "VoucherRedemption" => "Redemption office",
        "Shipyard" => "Shipyard",
        "Commodities" => "Commodities",
        _ => service.Role,
    };

    private static string Left(TimeSpan span) =>
        span.TotalHours >= 1
            ? $"{(int)span.TotalHours} h {span.Minutes} min"
            : $"{Math.Max(1, (int)span.TotalMinutes)} min";

    private string Ago(DateTimeOffset seen)
    {
        var span = _now() - seen;

        return span.TotalMinutes < 90
            ? $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago"
            : span.TotalHours < 36
                ? $"{(int)span.TotalHours} hours ago"
                : $"{(int)span.TotalDays} days ago";
    }

    /// <summary>
    /// A stat tile naming a system, in Cyan where the Commander is in it, with a copy glyph beside the
    /// value where <paramref name="system"/> names one.
    /// </summary>
    private Control System(string label, string value, string? system)
    {
        var here = system is { Length: > 0 }
                   && string.Equals(system, _carrier.Here, StringComparison.OrdinalIgnoreCase);

        var tile = StatTile.Build(label, value, here ? StatInk.Here : StatInk.Value);

        if (system is not { Length: > 0 } target || _copy is not { } copy || tile.Child is not { } figures)
        {
            return tile;
        }

        tile.Child = null;

        var glyph = CopyGlyph.For(target, copy);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(glyph, 1);

        tile.Child = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 6,
            Children = { figures, glyph },
        };

        return tile;
    }
}

