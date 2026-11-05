using D47.Core.Audio;

namespace D47.Core.Messages;

/// <summary>
/// A keeping of posted messages outside <c>messages.json</c> and its cap. <see cref="MessageStore"/> calls it after its own
/// write, outside its lock and never on the tick. An implementation does not throw.
/// </summary>
public interface IMessageRecord
{
    void Posted(D47Message message, SpokenClip? spoken);

    /// <summary>A clip for a message posted earlier, whether or not <c>messages.json</c> still holds it.</summary>
    void Attached(string messageKey, SpokenClip spoken);

    void ForgetOwnVoice();

    void ForgetCustomVoice(string voiceId);
}
