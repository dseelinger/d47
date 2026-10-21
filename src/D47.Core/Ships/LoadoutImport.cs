using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>
/// A pasted SLEF loadout read as slot plans for one hull. <see cref="Refusal"/> is a sentence when the
/// text could not be used, and the other members are then empty.
/// </summary>
public sealed record LoadoutImport(
    string? Hull,
    IReadOnlyList<SlotPlan> Slots,
    IReadOnlyList<string> Unread,
    string? Refusal = null)
{
    public bool IsRefused => Refusal is not null;

    /// <summary>
    /// Reads a SLEF array holding one loadout, a bare <c>{ header, data }</c> object or a bare
    /// <c>Loadout</c> event.
    /// </summary>
    public static LoadoutImport Read(string? text)
    {
        if (!TryFindLoadout(text, out var data, out var refusal))
        {
            return Refused(refusal!);
        }

        var loadout = ShipLoadout.Unknown.Apply(new JournalEvent(DateTimeOffset.MinValue, "Loadout", data));

        if (EliteSpecifications.Ship(loadout.Type) is not { } ship)
        {
            return Refused(loadout.Type is { Length: > 0 } type
                ? $"I do not know a hull called {type}, so I cannot plan from that loadout."
                : "That loadout does not name a hull.");
        }

        var slots = new List<SlotPlan>();
        var unread = new List<string>();

        foreach (var module in loadout.Modules)
        {
            if (EliteSpecifications.Slot(ship.Symbol, module.Slot) is not { } slot)
            {
                continue;
            }

            if (EliteSpecifications.Module(module.Item) is not { } specification)
            {
                unread.Add($"{module.Slot}: I do not know the module {module.Item}");
                continue;
            }

            slots.Add(Plan(slot.Name, module, specification, unread));
        }

        return new LoadoutImport(ship.Symbol, slots, unread);
    }

    private static SlotPlan Plan(string slot, ShipModule module, ModuleSpecification specification, List<string> unread)
    {
        string? blueprint = null;
        string? experimental = null;

        if (module.Blueprint is { Length: > 0 } symbol)
        {
            blueprint = BlueprintCatalogue.NameOf(symbol);

            if (blueprint is null)
            {
                unread.Add($"{slot}: I do not know the blueprint {symbol}");
            }
        }

        if (module.ExperimentalSymbol is { Length: > 0 } effect)
        {
            experimental = BlueprintCatalogue.NameOf(effect) ?? BlueprintCatalogue.NameOf(module.Experimental);

            if (experimental is null)
            {
                unread.Add($"{slot}: I do not know the experimental effect {effect}");
            }
        }

        return new SlotPlan(
            slot,
            blueprint,
            blueprint is null ? 0 : module.BlueprintLevel ?? 0,
            experimental,
            specification.Name)
        {
            Variant = module.Item,
            Priority = Math.Clamp(module.Priority ?? 1, 1, 5),
        };
    }

    private static bool TryFindLoadout(string? text, out JsonElement data, out string? refusal)
    {
        data = default;
        refusal = NotALoadout;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() > 1)
                {
                    refusal = "That holds more than one loadout. Copy one build at a time.";
                    return false;
                }

                if (root.GetArrayLength() == 0)
                {
                    return false;
                }

                root = root[0];
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var candidate = root.TryGetProperty("data", out var inner) ? inner : root;

            if (candidate.ValueKind != JsonValueKind.Object
                || !candidate.TryGetProperty("Modules", out var modules)
                || modules.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            data = candidate.Clone();
            refusal = null;
            return true;
        }
    }

    private const string NotALoadout = "That text is not a loadout.";

    private static LoadoutImport Refused(string refusal) => new(null, [], [], refusal);
}
