using D47.Core.Configuration;
using D47.Core.Hulls;

namespace D47.App.Panel;

/// <summary>Remembers which fleet carrier model the Commander picked for each carrier, by callsign.</summary>
public sealed class CarrierHullMemory(ViewStateStore store)
{
    /// <summary>The model symbol for a carrier; the Drake when none was picked.</summary>
    public string Of(string callSign) =>
        store.Load().CarrierHulls.TryGetValue(callSign, out var symbol) ? CarrierModels.Known(symbol) : CarrierModels.Default;

    public void Remember(string callSign, string symbol)
    {
        var state = store.Load();
        var picked = CarrierModels.Known(symbol);

        if (Of(callSign) == picked)
        {
            return;
        }

        var hulls = new Dictionary<string, string>(state.CarrierHulls, StringComparer.Ordinal) { [callSign] = picked };

        store.Save(state with { CarrierHulls = hulls });
    }
}
