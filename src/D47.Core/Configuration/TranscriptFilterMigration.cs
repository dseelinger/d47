using System.Text.Json;

namespace D47.Core.Configuration;

/// <summary>Moves the In Ship filters that <c>view-state.json</c> once held into settings.</summary>
public static class TranscriptFilterMigration
{
    private const string CommsName = "hideComms";
    private const string ChatterName = "hideInventedChatter";

    /// <summary>Turns on each setting whose old box was checked, then drops both old keys from the view state.</summary>
    public static void Run(ViewStateStore store, SettingsService settings)
    {
        var state = store.Load();

        if (state.Extra is not { } extra
            || (!Has(extra, CommsName) && !Has(extra, ChatterName)))
        {
            return;
        }

        var comms = Checked(extra, CommsName);
        var chatter = Checked(extra, ChatterName);

        if (comms || chatter)
        {
            settings.Replace(
                "the In Ship filters from the view state",
                s => s with
                {
                    Ui = s.Ui with
                    {
                        HideComms = s.Ui.HideComms || comms,
                        HideInventedChatter = s.Ui.HideInventedChatter || chatter,
                    },
                });
        }

        var rest = extra
            .Where(pair => !Named(pair.Key, CommsName) && !Named(pair.Key, ChatterName))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        store.Save(state with { Extra = rest.Count == 0 ? null : rest });
    }

    private static bool Named(string key, string name) => key.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool Has(Dictionary<string, JsonElement> extra, string name) => extra.Keys.Any(key => Named(key, name));

    private static bool Checked(Dictionary<string, JsonElement> extra, string name) =>
        extra.Any(pair => Named(pair.Key, name) && pair.Value.ValueKind == JsonValueKind.True);
}
