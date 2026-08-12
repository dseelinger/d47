using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// Reading fields out of journal JSON, in one vocabulary shared by every folder in this
/// namespace.
/// <para>
/// Everything here returns null rather than throwing or defaulting. That is the load-bearing
/// property: journal content is untrusted (architecture.md §7) and its schema moves between
/// game updates, so a field that is missing, renamed, or the wrong type has to be
/// indistinguishable from a field that was never asked for. A helper that returned 0 for a
/// missing number would put an invented value into game state and then into the model's
/// context, which is the failure this whole namespace exists to avoid.
/// </para>
/// </summary>
public static class JournalJson
{
    public static string? String(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// False for a missing flag, because every boolean the journal writes is one Elite omits
    /// when it is false — Docked, OnFoot, SRV. A nullable here would be a distinction without
    /// a difference at every call site.
    /// </summary>
    public static bool Bool(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.True;

    public static int? Int(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    public static long? Long(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : null;

    public static double? Double(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;

    public static JsonElement? Object(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    /// <summary>
    /// The elements of an array property, or empty when it is absent or is not an array. Empty
    /// rather than null so a caller can iterate without asking first — an absent list and an
    /// empty one mean the same thing everywhere the journal uses them.
    /// </summary>
    public static IEnumerable<JsonElement> Items(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    /// <summary>
    /// The player-facing name where Elite supplies one, falling back to the internal symbol.
    /// <para>
    /// Localised names are the ones worth speaking and the ones a Commander would say back, but
    /// they are absent from older journals and from some events entirely. Every name that
    /// reaches a person should come through here rather than reading either field directly.
    /// </para>
    /// </summary>
    public static string? Named(this JsonElement element, string property) =>
        element.String(property + "_Localised") ?? element.String(property);
}
