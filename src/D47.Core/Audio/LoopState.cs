namespace D47.Core.Audio;

/// <summary>Where the conversation loop is.</summary>
public enum LoopState
{
    /// <summary>Nothing in flight.</summary>
    Idle,

    /// <summary>The microphone is open.</summary>
    Listening,

    /// <summary>Captured audio is being turned into text.</summary>
    Transcribing,

    /// <summary>A turn is running — the model, a tool, or the keyword router.</summary>
    Thinking,

    /// <summary>An answer is being spoken.</summary>
    Speaking,

    /// <summary>The turn produced an answer.</summary>
    Answered,

    /// <summary>The turn produced an explicit "unsure".</summary>
    Unsure,

    /// <summary>The turn could not be completed.</summary>
    Failed,
}
