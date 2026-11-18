namespace D47.App.Input;

/// <summary>One input button capability of a HID collection; a single usage has equal bounds.</summary>
internal sealed record HidButtonCaps(ushort UsagePage, byte ReportId, bool IsAlias, ushort UsageMin, ushort UsageMax);

/// <summary>One input value capability of a HID collection; a single usage has equal bounds.</summary>
internal sealed record HidValueCaps(
    ushort UsagePage,
    byte ReportId,
    bool IsAlias,
    ushort LinkCollection,
    ushort UsageMin,
    ushort UsageMax,
    int LogicalMin,
    int LogicalMax,
    ushort BitSize = 0);

/// <summary>One hat switch: where it is reported and the range of its values.</summary>
internal sealed record HidHat(byte ReportId, ushort LinkCollection, int LogicalMin, int LogicalMax);

/// <summary>
/// One axis: where it is reported, its usage and the range of its values. A descriptor whose logical range is
/// empty is given the field's unsigned range.
/// </summary>
internal sealed record HidAxis(
    byte ReportId, ushort LinkCollection, ushort Usage, int LogicalMin, int LogicalMax, ushort BitSize = 0);

/// <summary>What a HID collection reports, counted the way <see cref="D47.Core.Hotas.HotasReading"/> carries it.</summary>
internal sealed class HidLayout
{
    public const ushort GenericDesktop = 0x01;
    public const ushort ButtonPage = 0x09;
    public const ushort HatSwitch = 0x39;

    private HidLayout(
        int buttonCount,
        IReadOnlyList<HidHat> hats,
        IReadOnlyList<HidAxis> axes,
        IReadOnlySet<byte> buttonReports,
        int reportLength)
    {
        ReportLength = reportLength;
        ButtonCount = buttonCount;
        Hats = hats;
        Axes = axes;
        ButtonReports = buttonReports;
        Needed = new HashSet<byte>([.. buttonReports, .. hats.Select(hat => hat.ReportId)]);
    }

    /// <summary>The page-9 usage count.</summary>
    public int ButtonCount { get; }

    public IReadOnlyList<HidHat> Hats { get; }

    /// <summary>Page-1 value usages other than the hat switch, each usage in a range listed.</summary>
    public IReadOnlyList<HidAxis> Axes { get; }

    public int AxisCount => Axes.Count;

    /// <summary>The report ids that carry buttons.</summary>
    public IReadOnlySet<byte> ButtonReports { get; }

    /// <summary>The report ids that carry a button or a hat; a reading needs every one of them.</summary>
    public IReadOnlySet<byte> Needed { get; }

    /// <summary>The input report length in bytes, report id included.</summary>
    public int ReportLength { get; }

    public static HidLayout From(
        IEnumerable<HidButtonCaps> buttons, IEnumerable<HidValueCaps> values, int reportLength = 0)
    {
        var buttonCount = 0;
        var buttonReports = new HashSet<byte>();

        foreach (var caps in buttons.Where(caps => caps.UsagePage == ButtonPage && !caps.IsAlias))
        {
            buttonCount += Math.Max(0, caps.UsageMax - caps.UsageMin + 1);
            buttonReports.Add(caps.ReportId);
        }

        var hats = new List<HidHat>();
        var axes = new List<HidAxis>();

        foreach (var caps in values.Where(caps => caps.UsagePage == GenericDesktop && !caps.IsAlias))
        {
            for (var usage = caps.UsageMin; usage <= caps.UsageMax; usage++)
            {
                if (usage == HatSwitch)
                {
                    hats.Add(new HidHat(caps.ReportId, caps.LinkCollection, caps.LogicalMin, caps.LogicalMax));
                }
                else
                {
                    axes.Add(Axis(caps, usage));
                }
            }
        }

        return new HidLayout(buttonCount, hats, axes, buttonReports, reportLength);
    }

    /// <summary>
    /// A raw value read from <paramref name="axis"/>'s field, sign-extended when its logical range goes below zero.
    /// </summary>
    public static long Signed(uint raw, HidAxis axis)
    {
        if (axis.LogicalMin >= 0)
        {
            return raw;
        }

        if (axis.BitSize is 0 or >= 32)
        {
            return unchecked((int)raw);
        }

        var sign = 1u << (axis.BitSize - 1);

        return (raw & sign) != 0 ? (long)raw - (1L << axis.BitSize) : raw;
    }

    private static HidAxis Axis(HidValueCaps caps, ushort usage)
    {
        var (min, max) = caps.LogicalMax > caps.LogicalMin || caps.BitSize is 0 or >= 32
            ? (caps.LogicalMin, caps.LogicalMax)
            : (0, (int)((1L << caps.BitSize) - 1));

        return new HidAxis(caps.ReportId, caps.LinkCollection, usage, min, max, caps.BitSize);
    }

    /// <summary>The held buttons for the page-9 usages a report lists; button index is usage − 1.</summary>
    public static void Hold(bool[] buttons, ReadOnlySpan<ushort> usages)
    {
        foreach (var usage in usages)
        {
            var index = usage - 1;

            if (index >= 0 && index < buttons.Length)
            {
                buttons[index] = true;
            }
        }
    }

    /// <summary>
    /// A hat value as a <c>GameControllerSwitchPosition</c> number: centre 0, up 1, then clockwise to up-left 8.
    /// A null or out-of-range value is centre.
    /// </summary>
    public static int Position(long value, HidHat hat)
    {
        var positions = (long)hat.LogicalMax - hat.LogicalMin + 1;

        if (positions <= 0 || value < hat.LogicalMin || value > hat.LogicalMax)
        {
            return 0;
        }

        return (int)((value - hat.LogicalMin) * 8 / positions) + 1;
    }
}

/// <summary>
/// A running count of axis moves for one collection, kept on its reader thread so that no axis value leaves it.
/// An axis moves when its value differs from the last value that counted by more than
/// 1/<see cref="RangeFraction"/> of its logical range; its first value sets the position and is not a move.
/// </summary>
internal sealed class AxisMoves(IReadOnlyList<HidAxis> axes)
{
    public const int RangeFraction = 32;

    private readonly long?[] _counted = new long?[axes.Count];

    /// <summary>Never reset.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Reads every axis from the latest report with its id, through <paramref name="value"/>, which gives null
    /// when the field cannot be read; returns <see cref="Count"/>.
    /// </summary>
    public int Read(HidReports reports, Func<HidAxis, byte[], uint?> value)
    {
        for (var index = 0; index < axes.Count; index++)
        {
            var axis = axes[index];

            if (reports.For(axis.ReportId) is { } report && value(axis, report) is { } raw)
            {
                Take(index, HidLayout.Signed(raw, axis));
            }
        }

        return Count;
    }

    /// <summary>Takes one axis's value; a value outside its logical range is ignored.</summary>
    public void Take(int index, long value)
    {
        var axis = axes[index];
        var range = (long)axis.LogicalMax - axis.LogicalMin;

        if (range <= 0 || value < axis.LogicalMin || value > axis.LogicalMax)
        {
            return;
        }

        if (_counted[index] is { } last)
        {
            if (Math.Abs(value - last) * RangeFraction <= range)
            {
                return;
            }

            Count++;
        }

        _counted[index] = value;
    }
}

/// <summary>The latest input report for each report id of one collection.</summary>
internal sealed class HidReports(IReadOnlySet<byte> needed)
{
    private readonly Dictionary<byte, byte[]> _latest = new();

    /// <summary>Keeps a report by its first byte, its report id; true once every needed id has one.</summary>
    public bool Store(byte[] report)
    {
        if (report.Length == 0)
        {
            return false;
        }

        _latest[report[0]] = report;

        return needed.Count == 0 || needed.All(_latest.ContainsKey);
    }

    public byte[]? For(byte reportId) => _latest.GetValueOrDefault(reportId);
}
