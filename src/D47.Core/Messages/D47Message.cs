namespace D47.Core.Messages;

/// <summary>One written message from a speaker to the Commander.</summary>
public sealed record D47Message
{
    public string Key { get; init; } = string.Empty;

    public DateTimeOffset Sent { get; init; }

    /// <summary>A persona id, an NPC name, or <see cref="MessageStore.Narrator"/>.</summary>
    public string From { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    /// <summary>The adventure this message belongs to, when it is a beat.</summary>
    public string? AdventureKey { get; init; }

    public bool Read { get; init; }

    /// <summary>The speaker's picture name, <c>&lt;story-id&gt;.&lt;cast-id&gt;</c> with a version suffix where the member has two; read through <see cref="Interface.SpeakerPictures"/>.</summary>
    public string? Picture { get; init; }

    /// <summary>The story cast member who sent it, by its <see cref="Configuration.D47Settings.StoryVoices"/> key; null for anyone else.</summary>
    public string? Cast { get; init; }

    /// <summary>The answers the Commander may give, when the message asks for one.</summary>
    public IReadOnlyList<MessageAnswer> Answers { get; init; } = [];

    /// <summary>The file name of the clip that was played, under <see cref="MessageClips.Folder"/>, when the message was spoken.</summary>
    public string? Clip { get; init; }

    /// <summary>The provider and voice that spoke the clip.</summary>
    public MessageVoice? Voice { get; init; }
}

/// <summary>One answer a message offers.</summary>
public sealed record MessageAnswer(string Id, string Label);

/// <summary>The provider id and voice id a message was spoken in.</summary>
public sealed record MessageVoice(string Provider, string? Id);
