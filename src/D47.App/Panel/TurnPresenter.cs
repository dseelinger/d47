using D47.Core.Conversation;

namespace D47.App.Panel;

/// <summary>
/// Writes one turn's events to the panel: its text, what it is doing in the microphone row while it runs, and
/// its provenance line when it completes. Created after the Commander's own words are appended.
/// </summary>
public sealed class TurnPresenter(PanelViewModel model)
{
    private readonly int _start = model.RunCount;

    /// <summary>The speaker the reply is drawn under, once Addressed names someone other than D47.</summary>
    private string? _speaker;

    public void On(TurnEvent turnEvent)
    {
        switch (turnEvent)
        {
            case TurnEvent.Addressed addressed:
                _speaker = addressed.Name;
                break;

            case TurnEvent.Routed routed:
                model.TurnStatus = routed.Effort is { } effort
                    ? $"routed: {routed.Route}, effort {effort}"
                    : $"routed: {routed.Route}";
                break;

            case TurnEvent.TextDelta text:
                model.Append(text.Text, speaker: _speaker);
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
