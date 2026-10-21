using System.Text.Encodings.Web;
using System.Text.Json;
using D47.Core.Journal;

namespace D47.Core.Ships;

/// <summary>Writes a <see cref="ShipLoadout"/> as SLEF text for an outfitting website's import.</summary>
public static class SlefWriter
{
    public const string AppName = "d47";

    public const string AppUrl = "https://github.com/dseelinger/d47";

    private static readonly JsonWriterOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A one-element SLEF array whose <c>data</c> has the shape of the journal's <c>Loadout</c> event.</summary>
    public static string Write(ShipLoadout loadout, string appVersion)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream, Options))
        {
            json.WriteStartArray();
            json.WriteStartObject();

            json.WriteStartObject("header");
            json.WriteString("appName", AppName);
            json.WriteString("appVersion", appVersion);
            json.WriteString("appURL", AppUrl);
            json.WriteEndObject();

            json.WriteStartObject("data");
            Data(json, loadout);
            json.WriteEndObject();

            json.WriteEndObject();
            json.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Data(Utf8JsonWriter json, ShipLoadout loadout)
    {
        json.WriteString("event", "Loadout");
        Text(json, "Ship", loadout.Type);

        if (loadout.TypeName is { } localised && !string.Equals(localised, loadout.Type, StringComparison.Ordinal))
        {
            json.WriteString("Ship_Localised", localised);
        }

        Number(json, "ShipID", loadout.ShipId);
        Text(json, "ShipName", loadout.Name);
        Text(json, "ShipIdent", loadout.Ident);
        Number(json, "HullValue", loadout.HullValue);
        Number(json, "ModulesValue", loadout.ModulesValue);
        Number(json, "HullHealth", Fraction(loadout.HullHealth));
        Number(json, "UnladenMass", loadout.UnladenMass);
        Number(json, "CargoCapacity", loadout.CargoCapacity);

        if (loadout.FuelCapacity is not null || loadout.ReserveCapacity is not null)
        {
            json.WriteStartObject("FuelCapacity");
            Number(json, "Main", loadout.FuelCapacity);
            Number(json, "Reserve", loadout.ReserveCapacity);
            json.WriteEndObject();
        }

        Number(json, "Rebuy", loadout.Rebuy);
        Number(json, "MaxJumpRange", loadout.MaxJumpRange);

        json.WriteStartArray("Modules");

        foreach (var module in loadout.Modules)
        {
            Module(json, module);
        }

        json.WriteEndArray();
    }

    private static void Module(Utf8JsonWriter json, ShipModule module)
    {
        json.WriteStartObject();
        json.WriteString("Slot", module.Slot);
        json.WriteString("Item", module.Item);
        json.WriteBoolean("On", module.Powered);
        Number(json, "Priority", module.Priority - 1);
        Number(json, "Health", Fraction(module.Health));
        Number(json, "Value", module.Value);

        if (module.IsEngineered)
        {
            Engineering(json, module);
        }

        json.WriteEndObject();
    }

    private static void Engineering(Utf8JsonWriter json, ShipModule module)
    {
        json.WriteStartObject("Engineering");
        Text(json, "BlueprintName", module.Blueprint);
        Number(json, "Level", module.BlueprintLevel);
        Number(json, "Quality", module.Quality);

        var symbol = module.ExperimentalSymbol ?? module.Experimental;
        Text(json, "ExperimentalEffect", symbol);

        if (module.Experimental is { } name && !string.Equals(name, symbol, StringComparison.Ordinal))
        {
            json.WriteString("ExperimentalEffect_Localised", name);
        }

        Text(json, "Engineer", module.Engineer);
        Number(json, "EngineerID", module.EngineerId);

        json.WriteStartArray("Modifiers");

        foreach (var modifier in module.Modifiers)
        {
            json.WriteStartObject();
            json.WriteString("Label", modifier.Label);
            Number(json, "Value", modifier.Value);
            Number(json, "OriginalValue", modifier.OriginalValue);
            json.WriteNumber("LessIsGood", modifier.LessIsGood ? 1 : 0);
            Text(json, "ValueStr", modifier.Text);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static double? Fraction(int? percent) => percent is { } value ? value / 100.0 : null;

    private static void Text(Utf8JsonWriter json, string name, string? value)
    {
        if (value is not null)
        {
            json.WriteString(name, value);
        }
    }

    private static void Number(Utf8JsonWriter json, string name, long? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(name, number);
        }
    }

    private static void Number(Utf8JsonWriter json, string name, double? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(name, number);
        }
    }
}
