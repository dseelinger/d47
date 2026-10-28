namespace D47.Core.Journal;

/// <summary>One wingmate, with when they joined and when they were last heard from.</summary>
public sealed record WingMember(string Name, DateTimeOffset JoinedAt, DateTimeOffset? LastHeard);

/// <summary>
/// The Commander's wing. Elite writes no event when another member leaves, so a member stays until
/// <c>WingLeave</c> empties the roster.
/// </summary>
public sealed record WingRoster
{
    public static readonly WingRoster Empty = new();

    public IReadOnlyList<WingMember> Members { get; init; } = [];

    public bool IsEmpty => Members.Count == 0;

    /// <summary>Whether both rosters name the same members, whatever was heard from them.</summary>
    public bool SameMembers(WingRoster other) =>
        Members.Select(member => member.Name).SequenceEqual(other.Members.Select(member => member.Name), StringComparer.Ordinal);

    public WingRoster Apply(JournalEvent journalEvent, string? commander)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        switch (journalEvent.Kind)
        {
            case "WingJoin":
                var others = journalEvent.Items("Others")
                    .Select(other => other.ValueKind == System.Text.Json.JsonValueKind.String ? other.GetString() : null)
                    .OfType<string>();

                return Empty.AddAll(others, journalEvent.Timestamp, commander);

            case "WingAdd" when journalEvent.String("Name") is { } name:
                return AddAll([name], journalEvent.Timestamp, commander);

            case "WingLeave":
                return Empty;

            case "ReceiveText" when journalEvent.String("Channel") is "player" or "wing"
                && journalEvent.String("From") is { } from:
                return Heard(from, journalEvent.Timestamp);

            case "ShipTargeted" when journalEvent.String("PilotName") is { } pilot
                && CommanderLog.PilotNameOf(pilot) is { } targeted:
                return Heard(targeted, journalEvent.Timestamp);

            default:
                return this;
        }
    }

    private WingRoster AddAll(IEnumerable<string> names, DateTimeOffset at, string? commander)
    {
        var members = Members.ToList();

        foreach (var name in names)
        {
            var key = CommanderLog.KeyOf(name);

            if (key.Length == 0
                || (commander is not null && key == CommanderLog.KeyOf(commander))
                || members.Any(member => CommanderLog.KeyOf(member.Name) == key))
            {
                continue;
            }

            members.Add(new WingMember(name.Trim(), at, null));
        }

        return members.Count == Members.Count ? this : this with { Members = members };
    }

    private WingRoster Heard(string from, DateTimeOffset at)
    {
        var key = CommanderLog.KeyOf(from);

        if (!Members.Any(member => CommanderLog.KeyOf(member.Name) == key))
        {
            return this;
        }

        return this with
        {
            Members = [.. Members.Select(member =>
                CommanderLog.KeyOf(member.Name) == key ? member with { LastHeard = at } : member)],
        };
    }
}
