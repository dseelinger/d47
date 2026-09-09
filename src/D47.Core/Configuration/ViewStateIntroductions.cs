using D47.Core.Persona;

namespace D47.Core.Configuration;

/// <summary>
/// Keeps the introduced cores in <c>view-state.json</c>, beside the collapsed cards and the window
/// position (docs/plans/change-requests.md item 7).
/// </summary>
public sealed class ViewStateIntroductions(ViewStateStore store) : IIntroductionMemory
{
    public IReadOnlyCollection<string> Load() => store.Load().IntroducedCores;

    /// <summary>Read, amend, write — rather than holding a <see cref="ViewState"/> of its own.</summary>
    public void Save(IReadOnlyCollection<string> introduced) =>
        store.Save(store.Load() with { IntroducedCores = [.. introduced] });
}
