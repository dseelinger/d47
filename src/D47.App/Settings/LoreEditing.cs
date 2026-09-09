using D47.Core.Capabilities.Builtin;
using D47.Core.Lore;

namespace D47.App.Settings;

/// <summary>Everything the notes window needs, gathered where the app can supply it (Phase 23).</summary>
/// <param name="CanSearch">Whether a lookup is possible at all.</param>
/// <param name="LookUp">Runs one web search and returns what it found, or null for nothing.</param>
public sealed record LoreEditing(
    LoreBook Book,
    Func<LoreCapability.LorePlace?> Here,
    Func<bool> CanSearch,
    Func<string, CancellationToken, Task<string?>> LookUp,
    Func<DateTimeOffset> Now);
