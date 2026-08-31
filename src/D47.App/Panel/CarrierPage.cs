using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// Where the Commander gets their carrier from, and a nudge when it changes.
/// <para>
/// A delegate rather than the state itself, for the reason every other source on this tab is one:
/// the state is replaced rather than mutated, so a copy taken at construction is the carrier as it
/// was when the page was built.
/// </para>
/// </summary>
public sealed class CarrierSource(Func<CarrierState> read)
{
    /// <summary>Raised when the journal moved the carrier on. The page redraws; nothing else cares.</summary>
    public event Action? Changed;

    public CarrierState Now => read();

    public void Invalidate() => Changed?.Invoke();
}

/// <summary>
/// The Commander's fleet carrier, on the tab named after it
/// (<a href="https://github.com/dseelinger/d47/issues/230">#230</a>).
/// <para>
/// <b>Their own, and deliberately not a squadron's.</b> Elite writes both to the same journal
/// seconds apart, and <see cref="CarrierState"/> exists in the shape it does because reading the
/// last one to arrive told a Commander their carrier was wherever their squadron's happened to be
/// — reported 2026-08-21, settled against 920 journals. Showing a squadron's carrier here would
/// need that discrimination extended rather than assumed, so this page shows the one d47 can
/// vouch for and says nothing about the other.
/// </para>
/// <para>
/// <b>Every figure carries its age.</b> The stats only refresh when the Commander opens the
/// carrier management panel, so a balance from three days ago is the freshest thing d47 has and
/// saying it flat would be saying it is current.
/// </para>
/// </summary>
public sealed class CarrierPage : UserControl
{
    private readonly CarrierSource _carrier;
    private readonly Func<DateTimeOffset> _now;
    private readonly StackPanel _body = new() { Spacing = 4 };

    public CarrierPage(CarrierSource carrier, Func<DateTimeOffset>? now = null)
    {
        _carrier = carrier;
        _now = now ?? (() => DateTimeOffset.UtcNow);

        carrier.Changed += OnChanged;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("where is my carrier");

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

    private void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _carrier.Changed -= OnChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Redraws against the live state. The tab calls this when it is shown.</summary>
    public void Refresh()
    {
        _body.Children.Clear();

        var carrier = _carrier.Now;

        if (!carrier.Owned)
        {
            // Said rather than left blank, and it says which of the two it means: d47 has not seen
            // one is a different claim from the Commander not having one, and only the first is
            // something d47 can know.
            _body.Children.Add(LoadoutPages.Muted(
                "No carrier has turned up in the journal yet. If you own one, open its management "
                + "panel in the game and it will appear here."));

            return;
        }

        _body.Children.Add(Head(carrier));

        if (carrier.PendingDecommission)
        {
            _body.Children.Add(LoadoutPages.Muted("Booked for decommissioning."));
        }

        _body.Children.Add(Where(carrier));
        _body.Children.Add(Row("Tritium", carrier.FuelLevel is { } fuel ? $"{fuel:N0} t" : "not seen"));

        if (carrier.JumpRange is { } range)
        {
            _body.Children.Add(Row("Jump range", $"{range:0.#} ly"));
        }

        if (carrier.Capacity is { } capacity && carrier.FreeSpace is { } free)
        {
            _body.Children.Add(Row(
                "Space",
                $"{capacity - free:N0} of {capacity:N0} t used, {free:N0} t free"));
        }

        if (carrier.CargoTonnes is { } cargo)
        {
            // How much, never what. Nothing Elite writes says what those tonnes are, and deriving
            // it from CargoTransfer was measured wrong 679 times against right 347.
            _body.Children.Add(Row("Cargo", $"{cargo:N0} t"));
        }

        if (carrier.Balance is { } balance)
        {
            _body.Children.Add(Row("Balance", balance.ToString("N0", CultureInfo.CurrentCulture) + " cr"));
        }

        if (!string.IsNullOrWhiteSpace(carrier.DockingAccess))
        {
            _body.Children.Add(Row("Docking", carrier.DockingAccess));
        }

        Services(carrier);

        if (carrier.StatsSeenAt is { } seen)
        {
            _body.Children.Add(LoadoutPages.Muted(
                $"Figures as of {Ago(seen)}. They only refresh when you open the carrier "
                + "management panel in the game."));
        }
    }

    private Control Head(CarrierState carrier) =>
        LoadoutPages.Heading(
            string.IsNullOrWhiteSpace(carrier.Name)
                ? carrier.CallSign ?? "Your carrier"
                : $"{carrier.Name} ({carrier.CallSign})");

    /// <summary>Where it is, and where it is going if it has been told to go somewhere.</summary>
    private Control Where(CarrierState carrier)
    {
        if (carrier.DestinationSystem is not { Length: > 0 } destination)
        {
            return Row("System", carrier.StarSystem ?? "not seen");
        }

        var parking = string.IsNullOrWhiteSpace(carrier.DestinationBody)
            ? destination
            : $"{destination}, at {carrier.DestinationBody}";

        if (carrier.DepartureTime is not { } departure)
        {
            return Row("Jumping to", parking);
        }

        // Counted against the clock the caller supplies rather than one read here, so a test can
        // stand where the Commander stands. A departure already past is said as such rather than
        // as a negative countdown: Elite leaves the booking in place through the jump itself.
        var left = departure - _now();

        return Row(
            "Jumping to",
            left > TimeSpan.Zero
                ? $"{parking} — leaves in {Left(left)}"
                : $"{parking} — leaving now");
    }

    private void Services(CarrierState carrier)
    {
        if (carrier.Services.Count == 0)
        {
            return;
        }

        var open = carrier.Services.Where(service => service.IsOpen).Select(Named).ToList();

        _body.Children.Add(Row(
            "Services",
            open.Count > 0 ? string.Join(", ", open) : "none switched on"));

        // Bought but switched off is worth saying on its own: it is the state a Commander can undo
        // from the management panel, and the one they are most likely not to have meant.
        var idle = carrier.Services
            .Where(service => service.Activated && !service.Enabled)
            .Select(Named)
            .ToList();

        if (idle.Count > 0)
        {
            _body.Children.Add(Row("Switched off", string.Join(", ", idle)));
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

    private static Control Row(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            Margin = new Thickness(0, 2, 0, 2),
        };

        var name = LoadoutPages.Muted(label);
        name.VerticalAlignment = VerticalAlignment.Top;

        var said = new SelectableTextBlock
        {
            Text = value,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = Theming.TypeScale.Body,
        };

        Grid.SetColumn(said, 1);

        grid.Children.Add(name);
        grid.Children.Add(said);

        return grid;
    }
}
