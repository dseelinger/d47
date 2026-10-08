using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// Everything a mini-capable <see cref="PanelView"/> is furnished from. The headset and the overlay
/// both call <see cref="Furnish"/>, so neither can carry a page the other lacks.
/// </summary>
public sealed record MiniPanelServices
{
    public Func<Avalonia.Controls.Control>? SettingsPage { get; init; }

    public D47.Core.Capabilities.CapabilityRegistry? Capabilities { get; init; }

    public D47.Core.Conversation.LearnedPhrasesStore? LearnedPhrases { get; init; }

    public D47.Core.Checklists.ChecklistService? Checklists { get; init; }

    public D47.Core.Goals.GoalBook? Goals { get; init; }

    public Action? BackfillGoals { get; init; }

    public D47.Core.Activities.ActivityLedger? Activities { get; init; }

    public D47.Core.Engineers.EngineerPlanService? Unlocks { get; init; }

    public D47.Core.Ships.ShipPlanService? Ships { get; init; }

    public Func<D47.Core.Journal.CommanderGameState?>? GameState { get; init; }

    public D47.Core.Loadout.OnFootPlanService? OnFoot { get; init; }

    public EngineerDirectoryMemory? EngineersMemory { get; init; }

    public ConstructionSurface? Construction { get; init; }

    public AdventureSurface? Adventures { get; init; }

    public RoutingSurface? Routing { get; init; }

    public Func<D47.Core.Journal.ModulePower>? ModulePower { get; init; }

    public Func<bool>? HullArt { get; init; }

    public D47.Core.Capabilities.Builtin.IClipboard? Clipboard { get; init; }

    public D47.Core.Knowledge.SystemsInPlay? Known { get; init; }

    public Func<string, Avalonia.Controls.Control?>? BuildSettingsStrip { get; init; }

    public Func<D47.Core.Knowledge.IGalaxyService?>? Galaxy { get; init; }

    public StarSystemSurface? StarSystem { get; init; }

    public CommanderRoster? Commanders { get; init; }

    public SpeakerPortraits? Portraits { get; init; }

    /// <summary>Enables every page the services allow, in the order the headset lists them.</summary>
    public void Furnish(PanelView view)
    {
        var gameState = GameState;

        if (Clipboard is not null)
        {
            view.EnableCopy(Clipboard);
        }

        if (Known is not null)
        {
            view.EnableSystemNames(Known, gameState is null ? null : () => gameState()?.Location.StarSystem);
        }

        if (gameState is not null)
        {
            view.EnableCommanderName(() => gameState()?.Identity.Name);
        }

        if (Portraits is not null)
        {
            view.EnableSpeakerPictures(Portraits);
        }

        if (SettingsPage is not null)
        {
            Func<PhrasesPage>? phrases = Capabilities is not null && LearnedPhrases is not null
                ? () => new PhrasesPage(Capabilities, LearnedPhrases, gameState ?? (() => null))
                : null;

            view.EnableSettings(SettingsPage, phrases: phrases);
        }

        if (Checklists is not null)
        {
            view.EnableChecklist(Checklists, Goals, BackfillGoals, Activities);
        }

        if (gameState is not null)
        {
            view.EnableMissions(
                gameState,
                () => DateTimeOffset.Now,
                Capabilities,
                Clipboard is null ? null : text => Clipboard.SetTextAsync(text));
            view.EnableStanding(gameState);
            view.EnableStatistics(gameState);
            view.EnableSession(gameState);
        }

        if (Commanders is not null)
        {
            view.EnableCommanders(Commanders);
        }

        view.EnableRawJournal();

        view.EnableLog(BuildSettingsStrip is null ? null : () => BuildSettingsStrip(PanelView.LogRoot));

        if (Routing is not null)
        {
            view.EnableRouting(Routing with { OpenSettings = () => view.Tab = PanelTab.Settings });
        }

        if (Adventures is not null)
        {
            view.EnableAdventures(
                Adventures,
                settingsStrip: BuildSettingsStrip is null
                    ? null
                    : () => BuildSettingsStrip(AdventuresPage.RootKey));
        }

        if (Ships is not null && Checklists is not null && gameState is not null)
        {
            view.EnableLoadout(
                Ships,
                Checklists,
                gameState,
                OnFoot,
                ModulePower,
                HullArt,
                settingsStrip: BuildSettingsStrip is null
                    ? null
                    : () => BuildSettingsStrip(LoadoutPages.FleetRoot),
                carrierSettingsStrip: BuildSettingsStrip is null
                    ? null
                    : () => BuildSettingsStrip(LoadoutPages.CarrierRoot),
                galaxy: Galaxy,
                status: Routing?.Status,
                construction: Construction);
        }

        if (StarSystem is not null)
        {
            view.EnableStarSystem(StarSystem with { OpenSettings = () => view.Tab = PanelTab.Settings });
        }

        if (Unlocks is not null && Ships is not null && gameState is not null)
        {
            view.EnableEngineers(Unlocks, Ships, gameState, OnFoot, EngineersMemory, Checklists);
        }
    }
}
