using D47.Core.Hotas;
using D47.Core.Input;

namespace D47.App.Settings;

/// <summary>What the Binding conflicts row and page read besides the settings.</summary>
/// <param name="Binds">Elite's bindings as last read.</param>
/// <param name="Readings">The controllers as last sampled.</param>
/// <param name="Switches">The Commander's mapped switches.</param>
/// <param name="Watch">Calls the handler when Elite's bindings are re-read; returns the unsubscribe.</param>
public sealed record BindingConflictEditing(
    Func<EliteBinds> Binds,
    Func<IReadOnlyList<HotasReading>> Readings,
    Func<IReadOnlyList<SwitchMapping>> Switches,
    Func<Action, Action> Watch);
