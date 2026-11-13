using System.Text.Json;

namespace D47.Core.Journal;

public static class FirstDiscovery
{
    /// <summary>Whether a <c>Scan</c> is of a body nobody had discovered: <c>WasDiscovered</c> false on an AutoScan or Detailed scan.</summary>
    public static bool Is(JsonElement scan) =>
        scan.TryGetProperty("WasDiscovered", out var discovered)
        && discovered.ValueKind == JsonValueKind.False
        && scan.String("ScanType") is "AutoScan" or "Detailed";
}
