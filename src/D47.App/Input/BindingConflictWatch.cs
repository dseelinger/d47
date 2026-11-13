using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;

namespace D47.App.Input;

/// <summary>
/// The certain conflicts between d47's bindings and Elite's, found again on the tick only when the settings, Elite's
/// bindings, the switches or the attached controllers changed. Null while Elite's bindings are not known and until the
/// controllers have settled once; kept while the controllers settle again.
/// </summary>
internal sealed class BindingConflictWatch(
    Func<D47Settings> settings,
    Func<IReadOnlyList<SettingRow>> rows,
    Func<IReadOnlyList<SwitchMapping>> switches,
    Func<EliteBinds> binds,
    Func<bool> controllersSettled,
    Func<IReadOnlyList<HotasReading>> readings)
{
    private IReadOnlyList<BindingConflict>? _current;

    private D47Settings? _settings;
    private IReadOnlyList<SwitchMapping>? _switches;
    private EliteBinds? _binds;
    private (string Id, ushort VendorId, ushort ProductId)[] _devices = [];

    /// <summary>The conflicts as last found, or null until they can be.</summary>
    public IReadOnlyList<BindingConflict>? Current => Volatile.Read(ref _current);

    /// <summary>Finds the conflicts again if an input changed; true when the list did.</summary>
    public bool Poll()
    {
        var known = binds();

        if (!known.IsKnown)
        {
            _binds = null;
            return Interlocked.Exchange(ref _current, null) is not null;
        }

        if (!controllersSettled())
        {
            return false;
        }

        var current = settings();
        var mapped = switches();
        var read = readings();

        if (_current is not null
            && ReferenceEquals(current, _settings)
            && ReferenceEquals(mapped, _switches)
            && ReferenceEquals(known, _binds)
            && SameDevices(read))
        {
            return false;
        }

        _settings = current;
        _switches = mapped;
        _binds = known;
        _devices = [.. read.Select(reading => (reading.Id, reading.VendorId, reading.ProductId))];

        var found = BindingConflicts.Find(current, rows(), mapped, known, read);
        var before = _current;

        Volatile.Write(ref _current, found);

        return before is null || !before.SequenceEqual(found, Same.Instance);
    }

    private bool SameDevices(IReadOnlyList<HotasReading> read)
    {
        if (read.Count != _devices.Length)
        {
            return false;
        }

        for (var index = 0; index < read.Count; index++)
        {
            if (read[index].Id != _devices[index].Id
                || read[index].VendorId != _devices[index].VendorId
                || read[index].ProductId != _devices[index].ProductId)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Two conflicts on the same control and press with the same Elite actions.</summary>
    private sealed class Same : IEqualityComparer<BindingConflict>
    {
        public static readonly Same Instance = new();

        public bool Equals(BindingConflict? x, BindingConflict? y) =>
            ReferenceEquals(x, y)
            || (x is not null && y is not null
                && x.Kind == y.Kind
                && x.Control == y.Control
                && x.RowKey == y.RowKey
                && x.Switch == y.Switch
                && x.Position == y.Position
                && x.Gesture == y.Gesture
                && x.EliteActions.SequenceEqual(y.EliteActions));

        public int GetHashCode(BindingConflict obj) => HashCode.Combine(obj.Kind, obj.Control, obj.Gesture);
    }
}
