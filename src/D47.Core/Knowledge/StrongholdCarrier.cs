namespace D47.Core.Knowledge;

/// <summary>Which Stronghold Carriers a Commander can dock at, for station and market answers alike.</summary>
public static class StrongholdCarrier
{
    /// <summary>The name every Stronghold Carrier carries, and Ardent's word for its type once read.</summary>
    public const string Name = "Stronghold Carrier";

    /// <summary>The Powerplay state of a system that holds a Stronghold Carrier.</summary>
    public const string PowerState = "Stronghold";

    /// <summary>A station named Stronghold Carrier in a Stronghold system.</summary>
    public static bool IsNamedOne(string? name, string? powerState) =>
        string.Equals(name, Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(powerState, PowerState, StringComparison.OrdinalIgnoreCase);

    /// <summary>A Stronghold Carrier whose system any Power but <paramref name="pledgedPower"/> controls.</summary>
    public static bool IsOtherPowers(bool isStrongholdCarrier, string? controllingPower, string? pledgedPower) =>
        isStrongholdCarrier
        && !(pledgedPower is { Length: > 0 }
             && string.Equals(controllingPower, pledgedPower, StringComparison.OrdinalIgnoreCase));
}
