namespace D47.Core.Ships;

/// <summary>What a module is for, as far as keeping it powered goes (#467).</summary>
public enum PowerRole
{
    None,
    Core,
    Life,
    Keep,
    Depends,
    Shed,
}

/// <summary>One slot that draws power: what is in it, what it draws and which priority it is in (#467).</summary>
/// <param name="Priority">1 to 5, or null where the slot's group could not be told.</param>
public sealed record PowerModule(
    string Slot, string Name, double Megawatts, bool IsHardpoint, int? Priority, PowerRole Role)
{
    /// <summary>The role for a module type, in <see cref="Knowledge.ModuleSpecification.Type"/>'s vocabulary.</summary>
    public static PowerRole RoleOf(string? type) => type?.ToLowerInvariant() switch
    {
        "cpd" or "cfsd" or "cfsdo" or "cs" or "ct" => PowerRole.Core,
        "cls" => PowerRole.Life,
        "isg" or "iscb" or "usb" or "upd" or "ucl" or "ufsws" => PowerRole.Keep,
        { Length: > 0 } hardpoint when hardpoint[0] == 'h' => PowerRole.Keep,
        "uhsl" or "ifsdb" => PowerRole.Depends,
        "ifs" or "cch" or "iafmu" => PowerRole.Shed,
        _ => PowerRole.None,
    };
}

/// <summary>Draw against output, deployed or retracted.</summary>
public sealed record PowerVerdict(double Draw, double Output)
{
    public bool Fits => Draw <= Output + PowerPriorities.Tolerance;

    /// <summary>Megawatts over the output, or zero for a build that fits.</summary>
    public double Overage => Fits ? 0 : Draw - Output;
}

/// <summary>One priority on the cumulative axis.</summary>
/// <param name="PoweredAt">Whether it keeps power at each of <see cref="PowerPriorities.Levels"/>, in order.</param>
public sealed record PriorityBand(int Priority, double Total, double Start, double Cumulative, IReadOnlyList<bool> PoweredAt);

/// <summary>One output level: its megawatts and the priorities still powered at it.</summary>
/// <param name="Level">The fraction of plant output, from <see cref="PowerPriorities.Levels"/>.</param>
/// <param name="Powered"><c>NONE</c>, <c>P1</c> or <c>P1–n</c>.</param>
public sealed record OutputLine(double Level, double Megawatts, string Powered);

/// <summary>One module's place on the cumulative axis.</summary>
public sealed record ModuleSpan(PowerModule Module, double Start, double End);

/// <summary>An output line that falls inside a priority, and the module it falls inside.</summary>
public sealed record LineCrossing(OutputLine Line, PowerModule Module);

/// <summary>One priority opened up: its modules in order, the lines inside it, and hardpoints left out when retracted.</summary>
public sealed record PriorityDrill(
    PriorityBand Band,
    IReadOnlyList<ModuleSpan> Spans,
    IReadOnlyList<LineCrossing> Crossings,
    IReadOnlyList<PowerModule> Stowed);

public enum CheckTag
{
    AtRisk,
    Off,
    Check,
    Ok,
}

/// <summary>One row of the D47 check.</summary>
/// <param name="MoveTo">The priority to offer a move to, or null for no action.</param>
public sealed record CheckRow(PowerModule Module, CheckTag Tag, string Reason, int? MoveTo)
{
    public string Label => Tag switch
    {
        CheckTag.AtRisk => "AT RISK",
        CheckTag.Off => "OFF",
        CheckTag.Check => "CHECK",
        _ => "OK",
    };

    public bool IsProblem => Tag != CheckTag.Ok;
}

/// <summary>
/// Which priorities keep power at full output and at each damage level, and which modules are in the
/// wrong one. Pure: the page lays this out and computes nothing (#467).
/// </summary>
public sealed class PowerPriorities
{
    internal const double Tolerance = 1e-9;

    /// <summary>Full output, then destroyed, malfunctioning and both, as fractions of plant output.</summary>
    public static IReadOnlyList<double> Levels { get; } = [1, 0.5, 0.4, 0.2];

    public const int Lowest = 5;

    private readonly Dictionary<int, List<PowerModule>> inPriority;

    private PowerPriorities(
        IReadOnlyList<PowerModule> modules, double output, bool retracted, IReadOnlyList<string>? order)
    {
        if (modules.Any(module => module.Priority is not (>= 1 and <= Lowest)))
        {
            throw new ArgumentException("Every module needs a priority from 1 to 5.", nameof(modules));
        }

        Output = output;
        IsRetracted = retracted;

        var rank = (order ?? [])
            .Select((slot, index) => (slot, index))
            .GroupBy(pair => pair.slot, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

        inPriority = Enumerable.Range(1, Lowest).ToDictionary(
            priority => priority,
            priority => modules
                .Where(module => module.Priority == priority)
                .OrderBy(module => rank.TryGetValue(module.Slot, out var index) ? index : int.MaxValue)
                .ToList());

        Deployed = new PowerVerdict(modules.Sum(module => module.Megawatts), output);
        Retracted = new PowerVerdict(modules.Where(module => !module.IsHardpoint).Sum(module => module.Megawatts), output);

        Bands = BandsOf(retracted);
        Lines = Levels.Select((level, index) => Line(level, index, Bands)).ToList();

        var full = Lines[0].Megawatts;
        DefaultSelected = Bands.FirstOrDefault(band => Inside(full, band))?.Priority ?? Lowest;

        (Checks, CheckHead) = Check(BandsOf(retracted: false));
    }

    /// <param name="order">Slots in the order wanted within each priority; slots not named keep theirs, after it.</param>
    public static PowerPriorities Of(
        IReadOnlyList<PowerModule> modules, double output, bool retracted = false, IReadOnlyList<string>? order = null) =>
        new(modules, output, retracted, order);

    public double Output { get; }

    public bool IsRetracted { get; }

    public PowerVerdict Deployed { get; }

    public PowerVerdict Retracted { get; }

    /// <summary>P1 to P5, counting hardpoints only when deployed.</summary>
    public IReadOnlyList<PriorityBand> Bands { get; }

    /// <summary>One per entry in <see cref="Levels"/>.</summary>
    public IReadOnlyList<OutputLine> Lines { get; }

    /// <summary>The priority the full-output line falls inside, or P5.</summary>
    public int DefaultSelected { get; }

    /// <summary>Problems first, then OK rows. Always judges the deployed build.</summary>
    public IReadOnlyList<CheckRow> Checks { get; }

    public string CheckHead { get; }

    /// <summary>One priority's modules on the cumulative axis, in the requested order.</summary>
    public PriorityDrill Drill(int priority)
    {
        var band = Bands[priority - 1];
        var spans = new List<ModuleSpan>();
        var at = band.Start;

        foreach (var module in inPriority[priority].Where(Counts))
        {
            spans.Add(new ModuleSpan(module, at, at + module.Megawatts));
            at += module.Megawatts;
        }

        var crossings = Lines
            .Where(line => Inside(line.Megawatts, band))
            .Select(line => spans.FirstOrDefault(span =>
                line.Megawatts >= span.Start - Tolerance && line.Megawatts <= span.End + Tolerance) is { } span
                ? new LineCrossing(line, span.Module)
                : null)
            .OfType<LineCrossing>()
            .ToList();

        var stowed = inPriority[priority].Where(module => !Counts(module)).ToList();

        return new PriorityDrill(band, spans, crossings, stowed);
    }

    private bool Counts(PowerModule module) => !(IsRetracted && module.IsHardpoint);

    private static bool Inside(double megawatts, PriorityBand band) =>
        megawatts > band.Start + Tolerance && megawatts < band.Cumulative - Tolerance;

    private List<PriorityBand> BandsOf(bool retracted)
    {
        var bands = new List<PriorityBand>();
        var start = 0.0;

        for (var priority = 1; priority <= Lowest; priority++)
        {
            var total = inPriority[priority]
                .Where(module => !(retracted && module.IsHardpoint))
                .Sum(module => module.Megawatts);
            var cumulative = start + total;

            bands.Add(new PriorityBand(
                priority, total, start, cumulative,
                Levels.Select(level => cumulative <= Output * level + Tolerance).ToList()));

            start = cumulative;
        }

        return bands;
    }

    private OutputLine Line(double level, int index, IReadOnlyList<PriorityBand> bands)
    {
        var powered = bands.TakeWhile(band => band.PoweredAt[index]).Count();

        return new OutputLine(level, Output * level, powered switch
        {
            0 => "NONE",
            1 => "P1",
            _ => $"P1–{powered}",
        });
    }

    private (List<CheckRow> Rows, string Head) Check(List<PriorityBand> deployed)
    {
        var lastPowered = deployed.Count(band => band.PoweredAt[0]);
        var problems = new List<CheckRow>();
        var fine = new List<CheckRow>();

        int? Offer(PowerModule module, int to) => module.Priority == to ? null : to;

        foreach (var module in inPriority.Values.SelectMany(modules => modules).OrderBy(module => module.Priority))
        {
            var band = deployed[module.Priority!.Value - 1];
            var powered = band.PoweredAt[0];

            switch (module.Role)
            {
                case PowerRole.Life when !band.PoweredAt[^1]:
                    problems.Add(new CheckRow(module, CheckTag.AtRisk, "Off if the plant is damaged at all.", Offer(module, 1)));
                    break;
                case PowerRole.Core when !powered:
                    problems.Add(new CheckRow(module, CheckTag.Off, "Core module. Unpowered when deployed.", Offer(module, Math.Max(lastPowered, 1))));
                    break;
                case PowerRole.Keep when !powered:
                    problems.Add(new CheckRow(module, CheckTag.Off, "Needed in a fight. Unpowered when deployed.", Offer(module, Math.Max(lastPowered, 1))));
                    break;
                case PowerRole.Depends when !powered:
                    problems.Add(new CheckRow(module, CheckTag.Check, "Unpowered when deployed. Depends on the build.", null));
                    break;
                case PowerRole.Shed:
                    fine.Add(new CheckRow(module, CheckTag.Ok, "Not needed in a fight. Shed first.", null));
                    break;
            }
        }

        var head = lastPowered switch
        {
            Lowest => "DEPLOYED · EVERYTHING POWERED",
            Lowest - 1 => "DEPLOYED · P5 UNPOWERED",
            _ => $"DEPLOYED · P{lastPowered + 1}–5 UNPOWERED",
        };

        return ([.. problems, .. fine], head);
    }
}
