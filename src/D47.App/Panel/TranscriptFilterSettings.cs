using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Keeps the model's In Ship filters equal to the two Interface settings.</summary>
public static class TranscriptFilterSettings
{
    /// <summary>Migrates the old view-state boxes, applies the settings now, and applies each change through <paramref name="post"/>.</summary>
    public static void Bind(ViewStateStore store, SettingsService settings, PanelViewModel model, Action<Action> post)
    {
        TranscriptFilterMigration.Run(store, settings);
        Apply(settings.Current, model);

        settings.Changed += change =>
        {
            if (change.Key is InterfaceCapability.HideCommsKey or InterfaceCapability.HideInventedChatterKey)
            {
                post(() => Apply(change.Settings, model));
            }
        };
    }

    private static void Apply(D47Settings settings, PanelViewModel model)
    {
        model.HideComms = settings.Ui.HideComms;
        model.HideInventedChatter = settings.Ui.HideInventedChatter;
    }
}
