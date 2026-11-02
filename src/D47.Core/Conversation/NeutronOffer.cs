namespace D47.Core.Conversation;

/// <summary>A neutron route's first waypoint, offered for plotting until replaced or withdrawn.</summary>
public sealed class NeutronOffer
{
    public const string FirstPhrase = "plot the first waypoint";

    public string? System { get; private set; }

    public bool IsStanding => System is { Length: > 0 };

    /// <summary>Stands the offer for the system, replacing any earlier one.</summary>
    public void Open(string system) => System = system;

    public void Withdraw() => System = null;

    /// <summary>The phrases that plot the first waypoint; none while no offer stands.</summary>
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

    private static readonly string[] Taking = [FirstPhrase, "set course for the first waypoint"];
}
