using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The planner's plans as the colonisation tools read them: fetched through <see cref="ColonisationPlans.GetAsync"/>,
/// and reduced to figures and short labels. Notes, links and Commander names are not in <see cref="PlannedSite"/>.
/// </summary>
internal sealed class PlanReading
{
    public const int LabelLimit = 60;

    public static readonly PlanReading None = new(false, false, new Dictionary<string, SystemPlan>(), []);

    private readonly IReadOnlyDictionary<string, SystemPlan> _plans;

    private PlanReading(bool composed, bool switchedOff, IReadOnlyDictionary<string, SystemPlan> plans, IReadOnlyList<string> missing)
    {
        Composed = composed;
        SwitchedOff = switchedOff;
        _plans = plans;
        Missing = missing;
    }

    /// <summary>Whether a plan holder exists; without one, answers say nothing about plans.</summary>
    public bool Composed { get; }

    public bool SwitchedOff { get; }

    /// <summary>Systems asked for that returned no plan.</summary>
    public IReadOnlyList<string> Missing { get; }

    public IEnumerable<SystemPlan> Systems => _plans.Values;

    public SystemPlan? Of(string? system) =>
        system is not null && _plans.TryGetValue(system, out var plan) ? plan : null;

    public PlannedSite? For(ConstructionSite site) =>
        Of(site.StarSystem)?.Sites.FirstOrDefault(planned => planned.MarketId == site.MarketId);

    public static async Task<PlanReading> ReadAsync(
        ColonisationPlans? plans,
        bool switchedOff,
        IEnumerable<string?> systems,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (plans is null)
        {
            return None;
        }

        var wanted = systems
            .OfType<string>()
            .Where(system => system.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (switchedOff)
        {
            return new PlanReading(true, true, new Dictionary<string, SystemPlan>(), wanted);
        }

        var read = await Task.WhenAll(wanted.Select(async system =>
                (system, plan: await plans.GetAsync(system, now, cancellationToken).ConfigureAwait(false))))
            .ConfigureAwait(false);

        return new PlanReading(
            true,
            false,
            read.Where(pair => pair.plan is not null)
                .ToDictionary(pair => pair.system, pair => pair.plan!, StringComparer.OrdinalIgnoreCase),
            [.. read.Where(pair => pair.plan is null).Select(pair => pair.system)]);
    }

    /// <summary>The systems the journal knows a colonisation in: claims and every site's.</summary>
    public static IEnumerable<string?> SystemsOf(ColonisationSites sites) =>
        sites.Claims.Select(claim => claim.StarSystem).Concat(sites.All.Select(site => site.StarSystem));

    /// <summary>One line saying why part of an answer comes from the journal alone, or null when nothing is missing.</summary>
    public string? Note() =>
        !Composed ? null
        : SwitchedOff ? "Reading colonisation plans is switched off, so this is from your journal alone."
        : Missing.Count > 0
            ? $"The plan could not be read from the colonisation planner for {string.Join(", ", Missing)}, so that part is "
                + "from your journal alone."
        : null;

    public static string Clip(string? text)
    {
        var tidy = string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return tidy.Length <= LabelLimit ? tidy : tidy[..LabelLimit].TrimEnd();
    }

    /// <summary>The planner's build id spoken title-cased: "coriolis" is "Coriolis".</summary>
    public static string BuildType(string? id)
    {
        var words = Clip(id?.Replace('_', ' ').Replace('-', ' '));

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words.ToLowerInvariant());
    }

    public static string Body(PlannedSite site) =>
        Clip(site.BodyName) is { Length: > 0 } name ? name
        : site.BodyNum is { } number ? $"body {number}"
        : string.Empty;

    public static string Status(PlannedSite site) => site.Status == PlanStatus.Build ? "building" : "planned";

    /// <summary>"Coriolis at A 1, planned, due 2026-12-01".</summary>
    public static string Describe(PlannedSite site)
    {
        var parts = new StringBuilder();
        var type = BuildType(site.BuildType);
        var body = Body(site);

        parts.Append(type.Length > 0 ? type : "Unknown build");

        if (body.Length > 0)
        {
            parts.Append(" at ").Append(body);
        }

        parts.Append(", ").Append(Status(site));

        if (site.Due is { } due)
        {
            parts.Append(", due ").Append(due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return parts.ToString();
    }

    /// <summary>Whether a spoken site name is this planned site's name or build type.</summary>
    public static bool Matches(PlannedSite site, string spoken) =>
        string.Equals(Clip(site.Name), Clip(spoken), StringComparison.OrdinalIgnoreCase)
        || string.Equals(BuildType(site.BuildType), BuildType(spoken), StringComparison.OrdinalIgnoreCase);

    public static bool Live(PlannedSite site) => site.Status is PlanStatus.Plan or PlanStatus.Build;
}
