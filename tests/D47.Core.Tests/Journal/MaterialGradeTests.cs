using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The one place d47 carries game data. These tests guard the properties that make carrying it
/// defensible: it covers what the game covers, it is internally consistent, and it answers
/// "unknown" rather than guessing for anything outside it.
/// </summary>
public class MaterialGradeTests
{
    [Fact]
    public void TheTableCoversTheWholeMaterialSet()
    {
        // 137 is what the canonical id list contains. A table that quietly shrank — a
        // regeneration against a truncated download, say — would show up as milestones going
        // silent for materials that used to work, which nothing else would report.
        Assert.Equal(137, MaterialGrades.Count);
    }

    [Theory]
    [InlineData("iron", 1)]
    [InlineData("nickel", 1)]
    [InlineData("tin", 3)]
    [InlineData("antimony", 4)]
    public void KnownMaterialsHaveTheGradeTheGameGivesThem(string name, int grade) =>
        Assert.Equal(grade, MaterialGrades.GradeOf(name));

    [Theory]
    // Raw materials come in categories of four, graded 1 to 4 within each — so no raw material
    // is grade 5. This is the structural check that caught a hand-written expectation: the
    // author's memory said Antimony was grade 5, the derived table said 4, and the progression
    // below is why the table was right. Exactly the error a written-out table would have shipped.
    [InlineData("carbon", "vanadium", "niobium", "yttrium")]
    [InlineData("lead", "zirconium", "boron", "antimony")]
    [InlineData("iron", "zinc", "tin", "selenium")]
    public void RawCategoriesRunOneToFour(string first, string second, string third, string fourth)
    {
        Assert.Equal(1, MaterialGrades.GradeOf(first));
        Assert.Equal(2, MaterialGrades.GradeOf(second));
        Assert.Equal(3, MaterialGrades.GradeOf(third));
        Assert.Equal(4, MaterialGrades.GradeOf(fourth));
    }

    [Fact]
    public void LookupIsCaseInsensitiveBecauseTheJournalIsNot()
    {
        // The Materials snapshot and MaterialCollected do not agree on casing across game
        // versions, and a case-sensitive miss would read as an unknown material.
        Assert.Equal(MaterialGrades.GradeOf("iron"), MaterialGrades.GradeOf("Iron"));
        Assert.Equal(MaterialGrades.GradeOf("iron"), MaterialGrades.GradeOf("IRON"));
    }

    [Theory]
    [InlineData(1, 300)]
    [InlineData(2, 250)]
    [InlineData(3, 200)]
    [InlineData(4, 150)]
    [InlineData(5, 100)]
    public void CapacityFollowsTheFixedGradeRule(int grade, int capacity) =>
        Assert.Equal(capacity, MaterialGrades.CapacityOfGrade(grade));

    [Fact]
    public void AGradeOutsideTheRuleIsUnknownRatherThanClamped()
    {
        // A milestone announced at a made-up threshold is the failure the derived table exists
        // to avoid, and clamping to the nearest grade would reintroduce it here.
        Assert.Null(MaterialGrades.CapacityOfGrade(0));
        Assert.Null(MaterialGrades.CapacityOfGrade(6));
    }

    [Fact]
    public void AMaterialTheTableDoesNotKnowHasNoCapacity()
    {
        // A material added by a game update that this table predates. Silence for it is
        // correct; a fraction of a number nobody checked is not.
        Assert.Null(MaterialGrades.GradeOf("somefuturematerial"));
        Assert.Null(MaterialGrades.CapacityOf("somefuturematerial"));
        Assert.Null(MaterialGrades.CapacityOf(null));
    }

    [Fact]
    public void EveryMaterialInTheTableResolvesToARealCapacity()
    {
        // The table and the rule have to agree: a grade in the generated file that the rule
        // does not cover would be a material whose milestones silently never fire.
        foreach (var name in new[] { "iron", "antimony", "adaptiveencryptors", "basicconductors" })
        {
            Assert.NotNull(MaterialGrades.CapacityOf(name));
        }
    }
}
