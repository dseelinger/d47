using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Restores the In Ship comms and chatter filters on start and saves them when they change.</summary>
public sealed class TranscriptFilterMemory
{
    private readonly ViewStateStore _store;
    private readonly PanelViewModel _model;

    public TranscriptFilterMemory(ViewStateStore store, PanelViewModel model)
    {
        _store = store;
        _model = model;

        var state = store.Load();
        model.HideComms = state.HideComms;
        model.HideInventedChatter = state.HideInventedChatter;

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PanelViewModel.HideComms) or nameof(PanelViewModel.HideInventedChatter))
            {
                Save();
            }
        };
    }

    private void Save() =>
        _store.Save(_store.Load() with { HideComms = _model.HideComms, HideInventedChatter = _model.HideInventedChatter });
}
