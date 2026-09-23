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
public sealed class CarrierPage : UserControl
{
    private readonly CarrierSource _carrier;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly StackPanel _body = new() { Spacing = 4 };
    private IDisposable? _sized;
    private bool _mini;

    public CarrierPage(
        CarrierSource carrier,
        Func<DateTimeOffset>? now = null,
        Func<string, Task<bool>>? copy = null,

        // The captain and tower's own settings, on the tab they only affect (#218, #305).
        Control? settingsStrip = null)
    {
        _carrier = carrier;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _copy = copy;

        carrier.Changed += OnChanged;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("where is my carrier");

        // Docked first among the bottom children, so it sits below `say` at the very bottom (#340).
        if (settingsStrip is not null)
        {
            DockPanel.SetDock(settingsStrip, Dock.Bottom);
            root.Children.Add(settingsStrip);
            root.CapStripHeight(settingsStrip);
        }

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

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

        _body.Children.Add(Title(carrier));

        if (carrier.PendingDecommission)
        {
            _body.Children.Add(LoadoutPages.Toned("Booked for decommissioning.", ThemeManager.RedKey));
        }

        var tiles = new List<Control> { Where(carrier) };

        if (carrier.JumpRange is { } range)
        {
            tiles.Add(StatTile.Build("Jump range", $"{range:0.#} ly"));
        }

        if (carrier.Capacity is { } capacity && carrier.FreeSpace is { } free)
        {
            tiles.Add(StatTile.Build(
                "Space",
                $"{capacity - free:N0} of {capacity:N0} t used, {free:N0} t free"));
        }

        if (carrier.CargoTonnes is { } cargo)
        {
            // How much, never what.
            tiles.Add(StatTile.Build("Cargo", $"{cargo:N0} t"));
        }

        if (carrier.Balance is { } balance)
        {
            tiles.Add(StatTile.Build("Balance", balance.ToString("N0", CultureInfo.CurrentCulture) + " cr"));
        }

        if (!string.IsNullOrWhiteSpace(carrier.DockingAccess))
        {
            tiles.Add(StatTile.Build("Docking", carrier.DockingAccess));
        }

        Services(carrier, tiles);

        _body.Children.Add(StatTile.Grid(tiles, maxColumns: 3));

        Tritium(carrier);

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
            tiles.Add(StatTile.Build("Tritium", $"{fuel:N0} t"));
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

    /// <summary>The carrier's name as the screen title, over a 1px A rule, under the breadcrumb that is its context line.</summary>
    private Control Title(CarrierState carrier)
    {
        var name = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            _mini ? TypeScale.Heading : TypeScale.Title,
            TitleRank.Screen,
            sentence: true);

        TitleText.Show(name, Named(carrier), sentence: true);

        var title = TitleText.GroupRow(name);
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
            StatTile.Build("In the tank", carrier.FuelLevel is { } fuel ? $"{fuel:N0} t" : "not seen"),
        };

        if (carrier.TritiumInHold is { } hold)
        {
            var note = carrier.TritiumInHoldUncertain
                ? "counted, may be off: a tritium order was open"
                : "counted";

            tiles.Add(StatTile.Build("Carrier's hold", $"{hold:N0} t ({note})"));
        }

        var shipTritium = _carrier.ShipTritium;

        if (shipTritium > 0)
        {
            tiles.Add(StatTile.Build("Your ship's hold", $"{shipTritium:N0} t"));
        }

        var total = (carrier.FuelLevel ?? 0) + (carrier.TritiumInHold ?? 0) + shipTritium;

        tiles.Add(StatTile.Build("Total", $"{total:N0} t"));

        var usedSpace = carrier.Capacity is { } capacity && carrier.FreeSpace is { } free ? capacity - free : 0;
        var (rangeLy, jumps) = CarrierFuel.RoughRange(total, usedSpace, carrier.JumpRange);
        var distance = carrier.JumpRange ?? 500;

        tiles.Add(StatTile.Build(
            "Range, roughly",
            $"about {RoundToTwoSigFigs(rangeLy):N0} ly — {jumps} jump{(jumps == 1 ? "" : "s")} at {distance:0.#} ly"));

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

        var word = CopyWord.For(target, copy);
        word.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(word, 1);

        tile.Child = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 6,
            Children = { figures, word },
        };

        return tile;
    }
}

