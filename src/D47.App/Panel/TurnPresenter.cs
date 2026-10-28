using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;

namespace D47.App.Panel;

/// <summary>
/// Writes one turn's events to the panel: its text, what it is doing in the microphone row while it runs, and
/// its provenance line when it completes. Created after the Commander's own words are appended, with the picture
/// name of the core aboard when the turn starts and how to find an addressed speaker's.
/// </summary>
public sealed class TurnPresenter(PanelViewModel model, string? shipPicture = null, Func<TurnEvent.Addressed, string?>? addressedPicture = null)
{
    private readonly int _start = model.RunCount;

    /// <summary>The speaker the reply is drawn under, once Addressed names someone other than the ship's AI.</summary>
    private string? _speaker;

    /// <summary>The picture name the reply is drawn with.</summary>
    private string? _picture = shipPicture;

    /// <summary>The English for the reply, held until the text it translates arrives.</summary>
    private string? _translation;

    public void On(TurnEvent turnEvent)
    {
        switch (turnEvent)
        {
            case TurnEvent.Addressed addressed:
                // A speaker with a picture of their own is somebody the Commander made, even over the radio.
                var invented = addressed.Role == VoiceRole.Comms && addressed.Picture is null;

                _speaker = invented ? NpcChatter.Invented(addressed.Name) : addressed.Name;
                _picture = invented ? null : addressed.Picture ?? addressedPicture?.Invoke(addressed);
                break;

            case TurnEvent.Routed routed:
                model.TurnStatus = routed.Effort is { } effort
                    ? $"routed: {routed.Route}, effort {effort}"
                    : $"routed: {routed.Route}";
                break;

            case TurnEvent.TextDelta text:
                model.Append(text.Text, speaker: _speaker, picture: _picture, translation: _translation);
                _translation = null;
                break;

            case TurnEvent.Translation translation:
                _translation = translation.Text;
                break;

            case TurnEvent.Retrying retry:
                model.TurnStatus =
                    $"retrying ({retry.Attempt}/{retry.Of}) in {retry.Wait.TotalSeconds:0.#}s — {retry.Because}";
                break;

            case TurnEvent.Completed completed:
                model.AttachProvenance(_start, TurnProvenance.For(completed.Result));
                model.TurnStatus = string.Empty;
                break;
        }
    }
}
