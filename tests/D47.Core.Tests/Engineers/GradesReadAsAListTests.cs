using D47.Core.Engineers;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>What an engineer grades, one entry at a time.</summary>
public class GradesReadAsAListTests
{
    private static EngineerEntry Entry(string name) =>
        new()
        {
            Engineer = EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}"),
            Reach = EngineerReach.Unlocked,
        };

    /// <summary>The reported engineer, and the reported shape.</summary>
    [Fact]
    public void EachSpecialityIsItsOwnEntry()
    {
        var entry = Entry("Felicity Farseer");

        Assert.Equal(entry.Engineer.Specialities.Count, entry.SpecialityLines.Count);
        Assert.All(entry.SpecialityLines, line => Assert.DoesNotContain(",", line, StringComparison.Ordinal));
    }

    /// <summary>The grade travels with the entry, in the form the Commander asked for.</summary>
    [Fact]
    public void AGradedSpecialityCarriesItsGrade()
    {
        var entry = Entry("Felicity Farseer");

        var graded = entry.Engineer.Specialities.First(speciality => speciality.IsGraded);

        Assert.Contains(
            $"{graded.Kind} (G{graded.MaxGrade})",
            entry.SpecialityLines);
    }

    /// <summary>The spoken form is untouched.</summary>
    [Fact]
    public void TheSpokenSentenceIsStillASentence()
    {
        var entry = Entry("Felicity Farseer");

        Assert.Contains(", ", entry.Specialities, StringComparison.Ordinal);
        Assert.Contains(" to ", entry.Specialities, StringComparison.Ordinal);
    }

    /// <summary>
    /// An engineer the table has no specialities for says so in the sentence and offers no lines, which
    /// is what keeps the panel's fallback reachable.
    /// </summary>
    [Fact]
    public void NothingToGradeIsAnEmptyListAndASentenceSayingSo()
    {
        var bare = new EngineerEntry
        {
            Engineer = new Engineer { Id = 0, Name = "Nobody", Specialities = [] },
            Reach = EngineerReach.WithinReach,
        };

        Assert.Empty(bare.SpecialityLines);
        Assert.Equal("nothing I have a record of", bare.Specialities);
    }
}
