using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Seats;

namespace D47.App.Panel;

/// <summary>What the seats section reads from the app: the store, the names already taken and the voices on offer.</summary>
/// <param name="Voices">The Aboard provider's voices, as id and label.</param>
/// <param name="Provider">The id of the Aboard provider.</param>
/// <param name="Reserved">The ship AI's and the carrier captain's names.</param>
public sealed record CrewSeatsHost(
    Func<CrewSeatStore> Store,
    Func<IReadOnlyList<(string Id, string Label)>> Voices,
    Func<string> Provider,
    Func<IReadOnlyList<string>> Reserved);

/// <summary>Fleet › Crew: the seats on the ship flown, one row per seat the hull offers.</summary>
public sealed class CrewSeatsSection
{
    public const string SameAsRole = "Same as the role";

    public const string OfferName = "CrewSeatsOffer";

    public const string SaveName = "CrewSeatSave";

    public const string ClearName = "CrewSeatClear";

    public const string NameBoxName = "CrewSeatName";

    public const string TitleBoxName = "CrewSeatTitle";

    public const string ReasonName = "CrewSeatReason";

    private const string NoVoice = "";

    private readonly CrewSeatsHost _host;
    private readonly Func<CommanderGameState?> _state;
    private readonly Action _redraw;

    public CrewSeatsSection(CrewSeatsHost host, Func<CommanderGameState?> state, Action redraw)
    {
        _host = host;
        _state = state;
        _redraw = redraw;
    }

    /// <summary>Changes when the ship flown or its stored seats do.</summary>
    public object? Stamp()
    {
        var ship = _state()?.FlownShip;

        return (ship?.ShipId, ship?.Type, _host.Store().Ships);
    }

    public Control Build()
    {
        var state = _state();
        var ship = state?.FlownShip;
        var body = new StackPanel { Spacing = 2, Margin = new Thickness(0, 28, 0, 0) };

        body.Children.Add(TitleText.GroupRow(TitleText.Build(
            ship?.Name is { Length: > 0 } name ? $"Seats aboard {name}" : "Seats aboard",
            TypeScale.Secondary,
            TitleRank.Group)));

        if (ship is not { IsKnown: true, ShipId: { } shipId })
        {
            body.Children.Add(Note("d47 has not seen which ship you are flying."));
            return body;
        }

        var said = ship.TypeSaid ?? ship.Type!;

        if (CrewSeats.CountFor(ship.Type) is not { } count)
        {
            body.Children.Add(Note(CrewSeatRules.UnknownSeats(said)));
            return body;
        }

        if (count == 0)
        {
            body.Children.Add(Note(CrewSeatRules.NoSeats(said)));
            return body;
        }

        var fid = state!.Identity.FrontierId ?? string.Empty;
        var held = _host.Store().For(fid, shipId)?.Seats ?? [];
        var reserved = Reserved(state);
        var offer = Button("OFFER THE DEFAULTS", OfferName);
        offer.IsEnabled = held.Count < count;
        offer.HorizontalAlignment = HorizontalAlignment.Left;
        offer.Margin = new Thickness(0, 8, 0, 8);
        offer.Click += (_, _) =>
        {
            Write(fid, shipId, ship.Type, CrewSeatRules.Offered(ship.Type, shipId, held, reserved));
        };
        body.Children.Add(offer);

        for (var slot = 0; slot < count; slot++)
        {
            body.Children.Add(new Row(this, fid, shipId, ship.Type!, slot, held, reserved).Control);
        }

        return body;
    }

    private IReadOnlyList<string> Reserved(CommanderGameState state) =>
        [.. state.Crew.Members.Select(member => member.Name), .. _host.Reserved()];

    private void Write(string fid, int shipId, string? hull, IReadOnlyList<CrewSeat> seats)
    {
        _host.Store().Set(new ShipSeats(fid, shipId, hull, seats));
        _redraw();
    }

    private static TextBlock Note(string text)
    {
        var note = LoadoutPages.Muted(text);
        note.Margin = new Thickness(0, 10, 0, 0);
        return note;
    }

    private static Button Button(string text, string name) => new()
    {
        Name = name,
        Content = text,
        Height = TypeScale.MinimumTarget,
        MinWidth = 76,
        FontSize = TypeScale.Control,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private sealed class Row
    {
        private readonly CrewSeatsSection _owner;
        private readonly string _fid;
        private readonly int _shipId;
        private readonly string _hull;
        private readonly int _slot;
        private readonly IReadOnlyList<CrewSeat> _held;
        private readonly IReadOnlyList<string> _reserved;
        private readonly CrewSeat? _seat;
        private readonly TextBox _name = new() { Name = NameBoxName, Width = 170, PlaceholderText = "Name" };
        private readonly TextBox _title = new() { Name = TitleBoxName, Width = 130, PlaceholderText = "Title" };
        private readonly InlinePicker _role = new();
        private readonly InlinePicker _voice = new();
        private readonly TextBlock _reason;
        private CrewRole _roleNow;
        private string _voiceNow = NoVoice;

        public Row(
            CrewSeatsSection owner, string fid, int shipId, string hull, int slot,
            IReadOnlyList<CrewSeat> held, IReadOnlyList<string> reserved)
        {
            _owner = owner;
            _fid = fid;
            _shipId = shipId;
            _hull = hull;
            _slot = slot;
            _held = held;
            _reserved = reserved;
            _seat = slot < held.Count ? held[slot] : null;

            _roleNow = _seat?.Role
                ?? CrewSeatRules.Roles
                    .Where(role => role != CrewRole.Custom && held.All(seat => seat.Role != role))
                    .Skip(slot - held.Count)
                    .FirstOrDefault(CrewRole.Custom);
            _name.Text = _seat?.Name;
            _title.Text = _seat?.Title;
            _title.IsVisible = _roleNow == CrewRole.Custom;
            _voiceNow = _seat?.Voice is { } voice
                && string.Equals(voice.Provider, owner._host.Provider(), StringComparison.OrdinalIgnoreCase)
                ? voice.VoiceId
                : NoVoice;

            _reason = new TextBlock
            {
                Name = ReasonName,
                FontSize = TypeScale.Small,
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false,
                Margin = new Thickness(0, 4, 0, 0),
            };
            LoadoutPages.Themed(_reason, TextBlock.ForegroundProperty, ThemeManager.RedKey);

            _role.Picked += (_, id) =>
            {
                _roleNow = Enum.Parse<CrewRole>(id);
                _title.IsVisible = _roleNow == CrewRole.Custom;
                ShowRole();
            };
            _voice.Picked += (_, id) =>
            {
                _voiceNow = id;
                ShowVoice();

                if (_seat is not null)
                {
                    Save();
                }
            };

            ShowRole();
            ShowVoice();
        }

        public Control Control
        {
            get
            {
                var save = Button("SAVE", SaveName);
                save.Click += (_, _) => Save();

                var clear = Button("CLEAR", ClearName);
                clear.Click += (_, _) => Clear();

                var fields = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
                fields.Children.Add(Sized(_role, 190));
                fields.Children.Add(_title);
                fields.Children.Add(_name);
                fields.Children.Add(Sized(_voice, 250));
                fields.Children.Add(save);
                fields.Children.Add(clear);

                var card = new Border
                {
                    Padding = new Thickness(14, 8),
                    Child = new StackPanel { Children = { fields, _reason } },
                };
                LoadoutPages.Themed(card, Border.BackgroundProperty, ThemeManager.SlabKey);

                return card;
            }
        }

        private static Control Sized(Control control, double width)
        {
            control.Width = width;
            return control;
        }

        private IReadOnlyList<CrewSeat> Others => [.. _held.Where(seat => seat.Id != _seat?.Id)];

        private void ShowRole() =>
            _role.Show(
                [.. CrewSeatRules.Roles.Select(role => new InlinePickerOption(
                    role.ToString(), CrewSeatRules.RoleLabel(role), ThemeManager.WhiteKey))],
                _roleNow.ToString(),
                CrewSeatRules.RoleLabel(_roleNow),
                ThemeManager.WhiteKey,
                null);

        private void ShowVoice()
        {
            var voices = _owner._host.Voices();
            var current = voices.FirstOrDefault(voice => voice.Id == _voiceNow);

            _voice.Show(
                [
                    new InlinePickerOption(NoVoice, SameAsRole, ThemeManager.WhiteKey),
                    .. voices.Select(voice => new InlinePickerOption(voice.Id, voice.Label, ThemeManager.WhiteKey)),
                ],
                _voiceNow,
                current.Label ?? SameAsRole,
                current.Label is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
                null);
        }

        private void Save()
        {
            var seat = new CrewSeat(
                _seat?.Id ?? CrewSeat.NewId(),
                _roleNow,
                _roleNow == CrewRole.Custom ? _title.Text?.Trim() : null,
                _name.Text?.Trim() ?? string.Empty,
                _voiceNow.Length > 0 ? new CrewSeatVoice(_owner._host.Provider(), _voiceNow) : null);

            if (CrewSeatRules.Refusal(seat, Others, _reserved) is { } reason)
            {
                _reason.Text = reason;
                _reason.IsVisible = true;
                return;
            }

            var seats = _held.ToList();

            if (_seat is null)
            {
                seats.Add(seat);
            }
            else
            {
                seats[_slot] = seat;
            }

            _owner.Write(_fid, _shipId, _hull, seats);
        }

        private void Clear()
        {
            if (_seat is null)
            {
                _name.Text = null;
                _title.Text = null;
                _voiceNow = NoVoice;
                _reason.IsVisible = false;
                ShowVoice();
                return;
            }

            _owner.Write(_fid, _shipId, _hull, [.. _held.Where(seat => seat.Id != _seat.Id)]);
        }
    }
}
