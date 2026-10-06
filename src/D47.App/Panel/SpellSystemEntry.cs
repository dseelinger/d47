using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The keyboard entry that spells a system name for plotting.</summary>
public static class SpellSystemEntry
{
    public const string Key = "spell-system";

    public static EntryRequest Request(string initial) => new(
        Key,
        "Spell",
        "System to plot",
        Context: null,
        initial,
        EntrySurface.Voice,
        value => value.Trim().Length == 0 ? EntryVerdict.No("Spell a system name first.") : EntryVerdict.Ok)
    {
        Spelled = true,
    };
}
