using D47.Core.Capabilities.Builtin;
using D47.Core.Lore;

namespace D47.App.Settings;

/// <summary>
/// Everything the notes window needs, gathered where the app can supply it (Phase 23).
/// <para>
/// A surface rather than four parameters threaded through <c>Attach</c>, matching
/// <see cref="SwitchEditing"/> beside it: the settings view knows how to open a window and
/// nothing about where a system address or a web search comes from.
/// </para>
/// </summary>
/// <param name="CanSearch">
/// Whether a lookup is possible at all. Asked when the window opens rather than captured when the
/// app started: the Commander can change the setting or the provider in between, and this is what
/// the window says before anything is typed rather than after. Both halves are the app's to know —
/// the setting is the Commander's and the endpoint's capability is the provider's.
/// </param>
/// <param name="LookUp">Runs one web search and returns what it found, or null for nothing.</param>
public sealed record LoreEditing(
    LoreBook Book,
    Func<LoreCapability.LorePlace?> Here,
    Func<bool> CanSearch,
    Func<string, CancellationToken, Task<string?>> LookUp,
    Func<DateTimeOffset> Now);
