namespace D47.Core.Conversation;

/// <summary>A redirected mission's new hand-in, offered for plotting until replaced or withdrawn (#663).</summary>
public sealed class HandInOffer
{
    public long? MissionId { get; private set; }

    public string? System { get; private set; }

    public bool IsStanding => System is { Length: > 0 };

    /// <summary>Stands the offer for the mission, replacing any earlier one.</summary>
    public void Open(long missionId, string system)
    {
        MissionId = missionId;
        System = system;
    }

    public void Withdraw()
    {
        MissionId = null;
        System = null;
    }

    /// <summary>The phrases that plot the hand-in; none while no offer stands.</summary>
    public IEnumerable<DynamicCommand> Phrases()
    {
        if (System is not { Length: > 0 } system)
        {
            yield break;
        }

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system };

        foreach (var phrase in Taking)
        {
            yield return new DynamicCommand(
                phrase, Capabilities.Builtin.NavigationCapability.Id, "plot_course", arguments);
        }
    }

    /// <summary>The phrases this can claim, whether or not an offer is standing.</summary>
    public static IReadOnlyList<string> EveryPhrase => Taking;

    private static readonly string[] Taking = ["plot it", "plot the hand-in", "set course for the hand-in"];
}
