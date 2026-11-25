using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// The ship materials the open engineering lines need, netted across every list and within each one, read
/// once from one document and one game state.
/// </summary>
public sealed class PlanMaterialNeeds
{
    private readonly ChecklistDocument _document;
    private readonly CommanderGameState _state;
    private readonly MaterialsInventory _materials;
    private readonly Dictionary<string, IReadOnlyDictionary<string, PlanIngredient>> _byList = new(StringComparer.Ordinal);

    internal PlanMaterialNeeds(ChecklistDocument document, CommanderGameState state)
    {
        _document = document;
        _state = state;
        _materials = state.Materials;
        All = PlanMaterials.Needs(document.Items, state);
    }

    /// <summary>Every live plan's need, by material symbol.</summary>
    public IReadOnlyDictionary<string, PlanIngredient> All { get; }

    /// <summary>Whether this reading is of this document and of the materials this state holds now.</summary>
    public bool Reads(ChecklistDocument document, CommanderGameState state) =>
        ReferenceEquals(document, _document) && ReferenceEquals(state, _state) && ReferenceEquals(state.Materials, _materials);

    /// <summary>One list's need, by material symbol.</summary>
    public IReadOnlyDictionary<string, PlanIngredient> For(ChecklistScope scope)
    {
        var key = ChecklistOrdering.Key(scope);

        lock (_byList)
        {
            if (!_byList.TryGetValue(key, out var needs))
            {
                needs = PlanMaterials.Needs(_document.In(scope), _state);
                _byList[key] = needs;
            }

            return needs;
        }
    }
}

/// <summary>
/// The "Have 12 Arsenic" lines a ship's plan writes for each ship material it leaves the Commander short of, when
/// the plan is added or whenever a material becomes short later.
/// A line ticks once what is held covers every live plan's need, stays ticked, and goes when no open
/// engineering line in its list needs the material.
/// </summary>
public static class PlanMaterials
{
    /// <summary>Whether the line is a material line.</summary>
    public static bool Is(ChecklistItem item) => item.Intent?.Kind == ChecklistIntentKind.Material;

    /// <summary>The ship materials the open Blueprint and Experimental lines among these need, by symbol.</summary>
    public static IReadOnlyDictionary<string, PlanIngredient> Needs(IEnumerable<ChecklistItem> items, CommanderGameState? state) =>
        EngineeringPlan.Cost(items, state).Ingredients
            .Where(ingredient => ingredient.Material.Ledger == MaterialLedger.Material)
            .ToDictionary(ingredient => ingredient.Material.Symbol, StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the needs of one document against one game state.</summary>
    public static PlanMaterialNeeds Read(ChecklistDocument document, CommanderGameState state)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(state);

        return new PlanMaterialNeeds(document, state);
    }

    /// <summary>
    /// A promoted plan's lines with the material lines added: every material line the list already has, and a
    /// new one for each ship material this list's open engineering needs and every live plan is short of once
    /// the plan is on the list. With no <c>Materials</c> snapshot nothing is known to be short, and none is added.
    /// </summary>
    /// <param name="wanted">The plan's own lines, the whole plan for the scope.</param>
    public static IReadOnlyList<ChecklistItem> With(
        ChecklistDocument document,
        ChecklistScope scope,
        IReadOnlyList<ChecklistItem> wanted,
        CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(wanted);

        var kept = document.In(scope).Where(Is).ToList();

        if (state is not { Materials.SnapshotSeen: true })
        {
            return [.. wanted, .. kept];
        }

        // The document as it will be once the plan lands, so a line the plan keeps keeps its state.
        var after = document.Revise(scope, ChecklistSource.EngineeringPlan, wanted).Document;
        var all = Needs(after.Items, state);
        var mine = Needs(after.In(scope), state);

        var hull = wanted.Select(item => item.Hull).FirstOrDefault(named => named is not null);
        var buildName = wanted.Select(item => item.BuildName).FirstOrDefault(named => named is not null);

        var added = mine.Values
            .Where(ingredient => all.TryGetValue(ingredient.Material.Symbol, out var every) && every.Short > 0)
            .Select(ingredient => Line(scope, ingredient.Material, all[ingredient.Material.Symbol].Needed, hull, buildName))
            .Where(line => !kept.Any(existing => string.Equals(existing.Key, line.Key, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(line => line.Text, StringComparer.Ordinal);

        return [.. wanted, .. kept, .. added];
    }

    /// <summary>A new material line: "Have 12 Arsenic".</summary>
    public static ChecklistItem Line(ChecklistScope scope, MaterialEntry material, int need, string? hull, string? buildName)
    {
        ArgumentNullException.ThrowIfNull(material);

        var intent = new ChecklistIntent(ChecklistIntentKind.Material, material.Symbol) { Quantity = need };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = scope,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = Text(material, need),
            Intent = intent,
            Hull = hull,
            BuildName = buildName,
            Provenance = ChecklistProvenance.Asserted,
        };
    }

    private static string Text(MaterialEntry material, int need) =>
        $"Have {need.ToString(CultureInfo.InvariantCulture)} {material.Name}";

    /// <summary>Whether the document has anything <see cref="Reconcile"/> reads: a material line or an open plan line.</summary>
    public static bool Applies(ChecklistDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Items.Any(item => Is(item) || IsOpenPlanLine(item));
    }

    private static bool IsOpenPlanLine(ChecklistItem item) =>
        !item.IsComplete
        && item.Source == ChecklistSource.EngineeringPlan
        && item.Intent?.Kind is ChecklistIntentKind.Blueprint or ChecklistIntentKind.Experimental;

    /// <summary>
    /// What the material lines need changing: a line no open engineering line in its list needs is removed;
    /// an open line whose material is covered across every plan is ticked, or removed when
    /// <paramref name="removeFulfilled"/>; an open line whose need moved is reworded; and a list with an open
    /// plan line gains a line for each ship material its engineering needs, every live plan is short of, and it
    /// has no line for.
    /// </summary>
    public static (IReadOnlyList<ChecklistItem> Moved, IReadOnlyList<ChecklistItem> Removed, IReadOnlyList<ChecklistItem> Added) Reconcile(
        PlanMaterialNeeds needs,
        ChecklistDocument document,
        CommanderGameState state,
        bool removeFulfilled)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(state);

        var moved = new List<ChecklistItem>();
        var removed = new List<ChecklistItem>();

        foreach (var line in document.Items.Where(Is))
        {
            if (MaterialCatalogue.Find(line.Intent!.Subject) is not { } material)
            {
                continue;
            }

            if (!needs.For(line.Scope).ContainsKey(material.Symbol))
            {
                removed.Add(line);
                continue;
            }

            if (line.IsComplete || !needs.All.TryGetValue(material.Symbol, out var every))
            {
                continue;
            }

            var reworded = line with
            {
                Text = Text(material, every.Needed),
                Intent = line.Intent with { Quantity = every.Needed },
            };

            if (state.Materials.SnapshotSeen && every.Short == 0)
            {
                if (removeFulfilled)
                {
                    removed.Add(line);
                }
                else
                {
                    moved.Add(reworded with { State = ChecklistState.Done });
                }

                continue;
            }

            if (reworded != line)
            {
                moved.Add(reworded);
            }
        }

        return (moved, removed, Missing(needs, document, state));
    }

    private static IReadOnlyList<ChecklistItem> Missing(PlanMaterialNeeds needs, ChecklistDocument document, CommanderGameState state)
    {
        if (!state.Materials.SnapshotSeen)
        {
            return [];
        }

        var added = new List<ChecklistItem>();

        foreach (var plan in document.Items.Where(IsOpenPlanLine).GroupBy(item => ChecklistOrdering.Key(item.Scope), StringComparer.Ordinal))
        {
            var scope = plan.First().Scope;
            var list = document.In(scope);
            var hull = list.Select(item => item.Hull).FirstOrDefault(named => named is not null);
            var buildName = list.Select(item => item.BuildName).FirstOrDefault(named => named is not null);

            added.AddRange(needs.For(scope).Values
                .Where(ingredient => needs.All.TryGetValue(ingredient.Material.Symbol, out var every) && every.Short > 0)
                .Select(ingredient => Line(scope, ingredient.Material, needs.All[ingredient.Material.Symbol].Needed, hull, buildName))
                .Where(line => !list.Any(existing => string.Equals(existing.Key, line.Key, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(line => line.Text, StringComparer.Ordinal));
        }

        return added;
    }

    /// <summary>
    /// What a material line's detail says — this list's share, and what is held of every plan's need — or null
    /// where nothing can be read.
    /// </summary>
    public static ChecklistVerdict? Verdict(ChecklistItem line, PlanMaterialNeeds needs, CommanderGameState state)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(state);

        if (!Is(line) || MaterialCatalogue.Find(line.Intent!.Subject) is not { } material)
        {
            return null;
        }

        var held = state.Materials.CountOf(material.Symbol);
        var heldSaid = held.ToString(CultureInfo.InvariantCulture);

        if (line.IsComplete)
        {
            return new ChecklistVerdict(
                ChecklistState.Done,
                $"You held enough for every plan when this ticked · {heldSaid} held now");
        }

        if (!needs.For(line.Scope).TryGetValue(material.Symbol, out var mine)
            || !needs.All.TryGetValue(material.Symbol, out var every))
        {
            return null;
        }

        var said = $"This list needs {mine.Needed.ToString(CultureInfo.InvariantCulture)} · {heldSaid} of "
                   + $"{every.Needed.ToString(CultureInfo.InvariantCulture)} so far";

        if (every.ExceedsCapacity)
        {
            said += " · " + ShortfallTrip.Storage(material.Name, every.Needed, every.Capacity!.Value).Sentence;
        }

        return new ChecklistVerdict(ChecklistState.Open, said);
    }
}
