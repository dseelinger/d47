using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>The settings, board, routing, store and system surface a galaxy search page test draws on; a subclass sets <see cref="Registry"/>.</summary>
public abstract class GalaxySearchWorld
{
    private const long Ltt7786 = 633608311522;

    private readonly IStarSystemService _systems;

    private readonly bool _suggesting;

    protected GalaxySearchWorld(IStarSystemService systems, bool lookups, bool suggesting = true)
    {
        _systems = systems;
        _suggesting = suggesting;
        Settings = TestSurface.Settings();
        Settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);
    }

    public SettingsService Settings { get; }

    public GalaxySearchBoard Board { get; } = new();

    public PanelRouting Routing { get; } = new(() => false, () => []);

    public SavedSearchStore Store { get; } = new("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

    public CapabilityRegistry Registry { get; protected set; } = null!;

    public StarSystemSurface Surface => new(
        _systems,
        () => Here,
        () => Settings.Current.Knowledge.GalaxySearch,
        () => { })
    {
        Searches = new GalaxySearchSurface(Registry, Board, Store, () => "F1", () => [])
        {
            PrefixNames = _suggesting ? _systems.PrefixNamesAsync : null,
            SuggestionPause = _suggesting ? TimeSpan.Zero : NameSuggestions.DefaultPause,
            SignalSources = SignalSources,
            PledgedPower = Pledged,
            History = HistoryOnSurface,
        },
    };

    protected virtual JournalLocation Here => new("LTT 7786", null, false, null) { SystemAddress = Ltt7786 };

    protected virtual Func<bool>? SignalSources => null;

    protected virtual Func<string?>? Pledged => null;

    protected virtual SearchHistory? HistoryOnSurface => null;
}
