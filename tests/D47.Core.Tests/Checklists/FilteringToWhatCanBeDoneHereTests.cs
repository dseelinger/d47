using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>Reading the checklist as "what can I do in this system".</summary>
public class FilteringToWhatCanBeDoneHereTests
{
    /// <summary>Not offered where there is no engineer, which is the overwhelmingly common case.</summary>
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
    /// And an item nobody here can roll is not offered by it either — asked of the same join the spoken
    /// parameter uses, so the page and the voice cannot disagree about what "here" means.
    /// </summary>
    [Fact]
    public void AnItemNoEngineerHereCanRollIsFilteredOut()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");

        var note = Assert.Single(checklists.Document.In(ChecklistScope.Universal));

        // A note is nobody's to roll: EngineersHere only ever offers blueprint and experimental intents,
        // which is the same rule the spoken answer follows.
        Assert.False(checklists.OfferedHere(note));
    }

    /// <summary>
    /// The key is a constant rather than a spelling, because the panel matches filter keys against enum
    /// names and this one is not an enum.
    /// </summary>
    [Fact]
    public void TheKeyIsOneSpelling() => Assert.Equal("here", ChecklistService.HereKey);
}
