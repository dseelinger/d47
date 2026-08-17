using D47.Core.Persona;

namespace D47.Core.Configuration;

/// <summary>
/// Keeps the introduced cores in <c>view-state.json</c>, beside the collapsed cards and the
/// window position (docs/plans/change-requests.md item 7).
/// <para>
/// It goes there rather than in <c>settings.json</c> because it is not a setting: nothing here
/// is chosen, there is no default worth documenting, and settings are append-only — a property
/// added there can never be removed, which is a heavy promise for a list of ids. View state is
/// the established home for "how things were left".
/// </para>
/// <para>
/// The adapter lives on this side of the seam rather than in <see cref="PersonaHost"/>, so the
/// host keeps depending on a port it can be handed nothing for. That is what lets the replay
/// harness and the persona tests run with no file anywhere.
/// </para>
/// </summary>
public sealed class ViewStateIntroductions(ViewStateStore store) : IIntroductionMemory
{
    public IReadOnlyCollection<string> Load() => store.Load().IntroducedCores;

    /// <summary>
    /// Read, amend, write — rather than holding a <see cref="ViewState"/> of its own. The record
    /// carries every other surface's state too, and a copy taken at startup and written back
    /// later would silently revert whatever the settings page or the headset had saved in the
    /// meantime. The same read-modify-write the VR anchors use.
    /// </summary>
    public void Save(IReadOnlyCollection<string> introduced) =>
        store.Save(store.Load() with { IntroducedCores = [.. introduced] });
}
