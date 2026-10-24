namespace D47.Core.Capabilities.Builtin;

/// <summary>The people you can talk to on comms, kept on Asset Mgmt › Characters; it has no tool and no setting.</summary>
public static class CharactersCapability
{
    public const string Id = "characters";

    public static CapabilityDescriptor Create() => new()
    {
        Id = Id,
        Group = "Ship",
        Name = "Characters",
        Summary = "Name the people you can talk to on comms, give each a voice, a picture and notes, and say where they are.",
        Display = new CapabilityDisplay { PanelTitle = "Characters", Order = 30, ShowOnPanel = false },
        Tools = [],
    };
}
