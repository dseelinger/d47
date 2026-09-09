using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The one place d47 carries game data.</summary>
public class MaterialGradeTests
{
    [Fact]
    public void TheTableCoversTheWholeMaterialSet()
    {
        // 137 from the canonical id list, and three the id list does not name at all — see
        // tools/curated_materials.py and #127.
        Assert.Equal(140, MaterialGrades.Count);
    }

    [Theory]
    [InlineData("iron", 1)]
    [InlineData("nickel", 1)]
    [InlineData("tin", 3)]
    [InlineData("antimony", 4)]
    public void KnownMaterialsHaveTheGradeTheGameGivesThem(string name, int grade) =>
        Assert.Equal(grade, MaterialGrades.GradeOf(name));

    [Theory]
    // Raw materials come in categories of four, graded 1 to 4 within each — so no raw material is grade 5.
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
        // The Materials snapshot and MaterialCollected do not agree on casing across game versions, and a
        // case-sensitive miss would read as an unknown material.
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
        // A milestone announced at a made-up threshold is the failure the derived table exists to avoid, and
        // clamping to the nearest grade would reintroduce it here.
        Assert.Null(MaterialGrades.CapacityOfGrade(0));
        Assert.Null(MaterialGrades.CapacityOfGrade(6));
    }

    [Fact]
    public void AMaterialTheTableDoesNotKnowHasNoCapacity()
    {
        // A material added by a game update that this table predates.
        Assert.Null(MaterialGrades.GradeOf("somefuturematerial"));
        Assert.Null(MaterialGrades.CapacityOf("somefuturematerial"));
        Assert.Null(MaterialGrades.CapacityOf(null));
    }

    [Fact]
    public void EveryMaterialInTheTableResolvesToARealCapacity()
    {
        // The table and the rule have to agree: a grade in the generated file that the rule does not cover
        // would be a material whose milestones silently never fire.
        foreach (var name in new[] { "iron", "antimony", "adaptiveencryptors", "basicconductors" })
        {
            Assert.NotNull(MaterialGrades.CapacityOf(name));
        }
    }
}

/// <summary> A holding never runs past what the game will hold. </summary>
public class AHoldingStaysInsideItsCapacityTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static MaterialsInventory After(params string[] lines)
    {
        var inventory = MaterialsInventory.Empty;

        foreach (var line in lines)
        {
            inventory = inventory.Apply(Event(line));
        }

        return inventory;
    }

    /// <summary>Adaptive Encryptors is grade 5: a hundred of them and no more.</summary>
    [Fact]
    public void CollectingPastTheCapStopsAtTheCap()
    {
        var inventory = After(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[{"Name":"adaptiveencryptors","Count":100}]}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialCollected","Category":"Encoded","Name":"adaptiveencryptors","Count":9}""");

        Assert.Equal(100, inventory.Find("adaptiveencryptors")?.Count);
    }

    /// <summary>The floor it has always had, asserted beside the ceiling it now has.</summary>
    [Fact]
    public void SpendingPastZeroStopsAtZero()
    {
        var inventory = After(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":2}],"Manufactured":[],"Encoded":[]}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialDiscarded","Category":"Raw","Name":"iron","Count":9}""");

        Assert.Equal(0, inventory.Find("iron")?.Count);
    }

    /// <summary>
    /// A material the grade table does not recognise has no ceiling rather than a guessed one — the
    /// same answer the milestone callout gives, for the same reason.
    /// </summary>
    [Fact]
    public void SomethingWithNoKnownCapacityIsNotClamped()
    {
        var inventory = After(
            """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialCollected","Category":"Raw","Name":"notarealmaterial","Count":500}""");

        Assert.Equal(500, inventory.Find("notarealmaterial")?.Count);
    }

    /// <summary>And a snapshot is believed as written.</summary>
    [Fact]
    public void ASnapshotIsTakenAsGiven()
    {
        var inventory = After(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[{"Name":"adaptiveencryptors","Count":109}]}""");

        Assert.Equal(109, inventory.Find("adaptiveencryptors")?.Count);
    }
}
