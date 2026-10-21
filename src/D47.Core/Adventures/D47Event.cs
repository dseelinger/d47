using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>Something d47 saw the Commander do that the journal does not record, named and shaped as the step event table spells it.</summary>
public sealed record D47Event(string Name, IReadOnlyList<EventField> Fields, DateTimeOffset At)
{
    /// <summary>A tool's handler ran for the Commander and returned no error.</summary>
    public const string ToolRun = "ToolRun";

    /// <summary>A line landed on a checklist.</summary>
    public const string ChecklistLineAdded = "ChecklistLineAdded";

    /// <summary>The <see cref="ToolRun"/> a tool call makes, or null when the model called it or the handler did not run and return no error.</summary>
    public static D47Event? Ran(ToolInvocation invocation, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        return invocation is { Caller: ToolCaller.Commander, Ran: true }
            ? new D47Event(ToolRun, [new EventField("Tool", invocation.Tool)], at)
            : null;
    }

    /// <summary>The <see cref="ChecklistLineAdded"/> for a line added to <paramref name="scope"/>, its group spelled as <c>add_to_checklist</c> spells it.</summary>
    public static D47Event LineAdded(ChecklistScope scope, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return new D47Event(ChecklistLineAdded, [new EventField("Group", ChecklistScope.Word(scope.Group))], at);
    }

    /// <summary>The event as the fold reads a journal event: its name as the kind, its fields as string properties.</summary>
    public JournalEvent AsJournalEvent()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in Fields)
        {
            raw[field.Field] = field.Value;
        }

        return new JournalEvent(At, Name, JsonSerializer.SerializeToElement(raw));
    }
}
