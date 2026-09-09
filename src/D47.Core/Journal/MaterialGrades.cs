namespace D47.Core.Journal;

/// <summary>
/// How many of a material a Commander can hold (Phase 8, "Call out material-gathering milestones").
/// </summary>
public static partial class MaterialGrades
{
    /// <summary>The storage limit per grade: 300, 250, 200, 150, 100 for grades 1 through 5.</summary>
    public static int? CapacityOfGrade(int grade) => grade switch
    {
        1 => 300,
        2 => 250,
        3 => 200,
        4 => 150,
        5 => 100,

        // Not a grade this rule covers.
        _ => null,
    };

    /// <summary>The material's grade, or null for a name the table does not know.</summary>
    public static int? GradeOf(string? journalName) =>
        journalName is not null && ByName.TryGetValue(journalName, out var grade) ? grade : null;

    /// <summary>How many of this material can be held, or null when d47 does not recognise it.</summary>
    public static int? CapacityOf(string? journalName) =>
        GradeOf(journalName) is { } grade ? CapacityOfGrade(grade) : null;

    /// <summary>How many materials the table knows.</summary>
    public static int Count => ByName.Count;
}
