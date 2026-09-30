using D47.Core.Audio;

namespace D47.Core.Conversation;

/// <summary>Who knows about the Commander's current scenario, as widening rings.</summary>
public enum ScenarioAudience
{
    /// <summary>The ship's AI and the crew.</summary>
    Aboard,

    /// <summary>Those aboard, and the carrier's captain and tower.</summary>
    Carrier,

    /// <summary>Anyone who speaks, including comms.</summary>
    Public,
}

public static class ScenarioAudiences
{
    /// <summary>Whether a scenario told to <paramref name="audience"/> reaches a line spoken as <paramref name="speaker"/>.</summary>
    public static bool Reaches(ScenarioAudience audience, VoiceRole speaker) => speaker switch
    {
        VoiceRole.ShipAi or VoiceRole.Crew => true,
        VoiceRole.CarrierCaptain or VoiceRole.TowerControl => audience >= ScenarioAudience.Carrier,
        _ => audience == ScenarioAudience.Public,
    };
}
