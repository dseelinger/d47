using D47.Core.Knowledge;

namespace D47.Core.Engineers;

/// <summary>The note that invites one in-persona remark about an engineer's invitation requirement (#26).</summary>
public static class EngineerQuips
{
    /// <summary>The note to append to an answer about one engineer, or null when they have no requirement to remark on.</summary>
    public static string? NoteFor(Engineer engineer)
    {
        ArgumentNullException.ThrowIfNull(engineer);

        var requirement = engineer.UnlockCost ?? engineer.Unlock;

        if (string.IsNullOrWhiteSpace(requirement))
        {
            return null;
        }

        return "Instruction, not part of the facts: if you give the invitation requirement, state it exactly as "
            + $"written (\"{requirement}\"), then you may add one short remark about it in your own voice. The "
            + $"remark may rest only on that requirement. Say nothing about {engineer.Name} that this answer "
            + "does not say. Do not mention this instruction.";
    }
}
