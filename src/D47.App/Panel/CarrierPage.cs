using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Where the Commander gets their carrier from, and a nudge when it changes.</summary>
public sealed class CarrierSource(Func<CarrierState> own, Func<CarrierState> squadron)
{
    /// <summary>Raised when the journal moved either carrier on.</summary>
    public event Action? Changed;

    /// <summary>The Commander's own.</summary>
    public CarrierState Now => own();

    /// <summary>Their squadron's, if they are in a squadron that has one.</summary>
    public CarrierState Squadron => squadron();

    public void Invalidate() => Changed?.Invoke();
}

/// <summary>The Commander's fleet carrier, on the tab named after it (#230).</summary>
public sealed class CarrierPage : UserControl
{
    private readonly CarrierSource _carrier;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly StackPanel _body = new() { Spacing = 4 };

    public CarrierPage(CarrierSource carrier, Func<DateTimeOffset>? now = null, Func<string, Task<bool>>? copy = null)
    {
        _carrier = carrier;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _copy = copy;

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
            // How much, never what.
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

    /// <summary>The squadron's carrier, under its own heading and only when there is one.</summary>
    private void Squadron()
    {
        var carrier = _carrier.Squadron;

        if (!carrier.Owned)
        {
            return;
        }

        _body.Children.Add(new Border { Height = 18 });
        _body.Children.Add(LoadoutPages.Heading("Your squadron's carrier"));
        _body.Children.Add(Head(carrier));
        _body.Children.Add(Where(carrier));

        if (carrier.FuelLevel is { } fuel)
        {
            _body.Children.Add(Row("Tritium", $"{fuel:N0} t"));
        }

        if (!string.IsNullOrWhiteSpace(carrier.DockingAccess))
        {
            _body.Children.Add(Row("Docking", carrier.DockingAccess));
        }

        var open = carrier.Services.Where(service => service.IsOpen).Select(Named).ToList();

        if (open.Count > 0)
        {
            _body.Children.Add(Row("Services", string.Join(", ", open)));
        }

        // No balance and no space.
        _body.Children.Add(LoadoutPages.Muted(
            "Your squadron's, not yours — shown so you know where it is and whether you can dock."));
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
            return Row("System", carrier.StarSystem ?? "not seen", carrier.StarSystem);
        }

        var parking = string.IsNullOrWhiteSpace(carrier.DestinationBody)
            ? destination
            : $"{destination}, at {carrier.DestinationBody}";

        if (carrier.DepartureTime is not { } departure)
        {
            return Row("Jumping to", parking, destination);
        }

        // Counted against the clock the caller supplies rather than one read here, so a test can stand where
        // the Commander stands.
        var left = departure - _now();

        return Row(
            "Jumping to",
            left > TimeSpan.Zero
                ? $"{parking} — leaves in {Left(left)}"
                : $"{parking} — leaving now",
            destination);
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

        // Bought but switched off is worth saying on its own: it is the state a Commander can undo from the
        // management panel, and the one they are most likely not to have meant.
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

    /// <summary>A label and its value, with a copy glyph beside the value where <paramref name="system"/> names one.</summary>
    private Control Row(string label, string value, string? system = null)
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

        grid.Children.Add(name);

        if (system is { Length: > 0 } target && _copy is { } copy)
        {
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { said, D47.App.Controls.CopyGlyph.For(target, copy) },
            };

            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
        }
        else
        {
            Grid.SetColumn(said, 1);
            grid.Children.Add(said);
        }

        return grid;
    }
}
