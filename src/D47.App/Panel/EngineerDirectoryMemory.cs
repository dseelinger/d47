using D47.Core.Configuration;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Remembers the Engineers tab's two checkbox filters, shared by every surface (#132).</summary>
public sealed class EngineerDirectoryMemory(ViewStateStore store)
{
    private bool? _hideColonia;
    private bool? _hideOnFoot;

    /// <summary>Whether the eight engineers out at Colonia are off the lists.</summary>
    public bool HideColonia => _hideColonia ??= store.Load().EngineersColoniaHidden;

    /// <summary>Whether the on-foot engineers are off the lists.</summary>
    public bool HideOnFoot => _hideOnFoot ??= store.Load().EngineersOnFootHidden;

    /// <summary>Records where the Colonia tick was left.</summary>
    public void RememberColonia(bool hide)
    {
        if (HideColonia == hide)
        {
            return;
        }

        _hideColonia = hide;

        store.Save(store.Load() with { EngineersColoniaHidden = hide });
    }

    /// <summary>Records where the on-foot tick was left.</summary>
    public void RememberOnFoot(bool hide)
    {
        if (HideOnFoot == hide)
        {
            return;
        }

        _hideOnFoot = hide;

        store.Save(store.Load() with { EngineersOnFootHidden = hide });
    }

    /// <summary>Whether a tick would take this engineer off the lists.</summary>
    public bool Hides(Engineer engineer) =>
        (HideColonia && engineer.IsFarFromTheBubble == true) || (HideOnFoot && engineer.IsOnFoot);
}
