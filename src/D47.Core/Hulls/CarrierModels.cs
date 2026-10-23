namespace D47.Core.Hulls;

/// <summary>The four fleet carrier models, which neither the journal nor Status.json reports.</summary>
public static class CarrierModels
{
    public const string Default = "fleetcarrier";

    public static IReadOnlyList<(string Symbol, string Label)> All { get; } =
    [
        (Default, "Drake"),
        ("fleetcarrier_fortune", "Fortune"),
        ("fleetcarrier_nautilus", "Nautilus"),
        ("fleetcarrier_victory", "Victory"),
    ];

    /// <summary>The symbol itself when it names a model, otherwise the Drake.</summary>
    public static string Known(string? symbol) =>
        All.Any(model => string.Equals(model.Symbol, symbol, StringComparison.Ordinal)) ? symbol! : Default;
}
