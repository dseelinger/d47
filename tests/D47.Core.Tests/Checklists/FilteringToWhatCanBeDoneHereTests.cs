using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Reading the checklist as "what can I do in this system" (change-requests.md 32).
/// <para>
/// <b>A filter and not a sort, ruled 2026-08-23.</b> Sorting would have had to say whether it
/// overrules the Commander's own project order or nests inside it; a filter never touches that
/// order.
/// </para>
/// <para>
/// <b>And the spoken half already existed.</b> `get_checklist` has taken a `here` parameter since
/// 2026-08-20 — <i>"only what an engineer in this system could roll today"</i> — so what this adds
/// is the row that puts the same question on the page, which is the half the request was missing.
/// </para>
/// </summary>
public class FilteringToWhatCanBeDoneHereTests
{
    /// <summary>
    /// <b>Not offered where there is no engineer</b>, which is the overwhelmingly common case. A
    /// filter that can show nothing is alarming in a way a re-ordered list is not, so the answer
    /// to "no engineer in this system" is that the choice is absent rather than a blank page after
    /// taking it.
    /// </summary>
    [Fact]
    public void TheRowIsAbsentWhereNoEngineerIsBased()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");

        Assert.DoesNotContain(
            checklists.FilterAxes(),
            filter => filter.Key == ChecklistService.HereKey);
    }

    /// <summary>
    /// And an item nobody here can roll is not offered by it either — asked of the same join the
    /// spoken parameter uses, so the page and the voice cannot disagree about what "here" means.
    /// </summary>
    [Fact]
    public void AnItemNoEngineerHereCanRollIsFilteredOut()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");

        var note = Assert.Single(checklists.Document.In(ChecklistScope.Universal));

        // A note is nobody's to roll: EngineersHere only ever offers blueprint and experimental
        // intents, which is the same rule the spoken answer follows.
        Assert.False(checklists.CanBeDoneHere(note));
    }

    /// <summary>
    /// The key is a constant rather than a spelling, because the panel matches filter keys against
    /// enum names and this one is not an enum. Two spellings of it is how a row that draws
    /// correctly filters nothing.
    /// </summary>
    [Fact]
    public void TheKeyIsOneSpelling() => Assert.Equal("here", ChecklistService.HereKey);
}
