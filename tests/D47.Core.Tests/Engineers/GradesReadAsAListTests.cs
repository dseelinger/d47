using D47.Core.Engineers;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// What an engineer grades, one entry at a time (remediation.md 16, item 6).
/// <para>
/// Reported against the Engineers tab with a picture: nine specialities set as running prose,
/// wrapping into a paragraph a Commander has to read through to find out whether the one they
/// came for is in it — and the commas separating the entries looking exactly like the commas
/// inside them.
/// </para>
/// </summary>
public class GradesReadAsAListTests
{
    private static EngineerEntry Entry(string name) =>
        new()
        {
            Engineer = EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}"),
            Reach = EngineerReach.Unlocked,
        };

    /// <summary>
    /// The reported engineer, and the reported shape. Every speciality is its own entry, so a
    /// panel can put each on its own line.
    /// </summary>
    [Fact]
    public void EachSpecialityIsItsOwnEntry()
    {
        var entry = Entry("Felicity Farseer");

        Assert.Equal(entry.Engineer.Specialities.Count, entry.SpecialityLines.Count);
        Assert.All(entry.SpecialityLines, line => Assert.DoesNotContain(",", line, StringComparison.Ordinal));
    }

    /// <summary>
    /// The grade travels with the entry, in the form the Commander asked for. A list without the
    /// grade would be a list of things they might be able to do.
    /// </summary>
    [Fact]
    public void AGradedSpecialityCarriesItsGrade()
    {
        var entry = Entry("Felicity Farseer");

        var graded = entry.Engineer.Specialities.First(speciality => speciality.IsGraded);

        Assert.Contains(
            $"{graded.Kind} (G{graded.MaxGrade})",
            entry.SpecialityLines);
    }

    /// <summary>
    /// <b>The spoken form is untouched.</b> A list is scanned and a sentence is heard, and the two
    /// are allowed to differ — but the sentence is what <c>get_engineer</c> says out loud, and
    /// changing it here would change what d47 sounds like as a side effect of a panel fix.
    /// </summary>
    [Fact]
    public void TheSpokenSentenceIsStillASentence()
    {
        var entry = Entry("Felicity Farseer");

        Assert.Contains(", ", entry.Specialities, StringComparison.Ordinal);
        Assert.Contains(" to ", entry.Specialities, StringComparison.Ordinal);
    }

    /// <summary>
    /// An engineer the table has no specialities for says so in the sentence and offers no lines,
    /// which is what keeps the panel's fallback reachable.
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
