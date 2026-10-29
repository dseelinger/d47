using D47.Core.Capabilities;
using D47.Core.Coverage;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every tool and drivable settings row on the Features page has steps for exercising it by hand.</summary>
[Trait("Category", "Gate")]
public sealed class EveryFeaturesLineHasTestStepsTests
{
    /// <summary>Settings keys with no steps yet. Tools are never on it.</summary>
    private static readonly HashSet<string> WithoutSteps = new(StringComparer.Ordinal)
    {
        "logging.default",
        "logging.subsystems.app",
        "logging.subsystems.capabilities",
        "logging.subsystems.settings",
        "logging.subsystems.journal",
        "logging.subsystems.llm",
        "logging.subsystems.voice",
        "logging.subsystems.vr",
        "logging.subsystems.input",
        "journal.codexPictures",
        "journal.shareCodexPictures",
        "journal.openNewCodexEntries",
        "journal.codexPublishToken",
        "knowledge.galaxy",
        "knowledge.notablePlaces",
        "knowledge.communityBiology",
        "knowledge.bgsTick",
        "knowledge.galNet",
        "knowledge.serverStatus",
        "knowledge.signalIndex",
        "contribution.eddn",
        "checklists.removeFulfilled",
        "ships.remembered",
        "ships.art",
        "ships.hullgpu",
        "knowledge.colonisationPlanner",
        "knowledge.colonisationProgress",
        "knowledge.ravenColonialKey",
        "adventures.storyDownloads",
        "adventures.storyRatings",
        "adventures.castReactions",
        "adventures.rating.combat",
        "adventures.rating.trading",
        "adventures.rating.mining",
        "adventures.rating.exploration",
        "adventures.rating.exobiology",
        "adventures.rating.passengers",
        "adventures.rating.searchAndRescue",
        "adventures.rating.crime",
        "llm.provider",
        "llm.endpoint",
        "llm.model",
        "llm.backgroundModel",
        "llm.highModel",
        "llm.effortFloor",
        "llm.effortCeiling",
        "models.refreshCatalog",
        "llm.anthropic.apiKey",
        "llm.openai.apiKey",
        "llm.openaiCompatible.apiKey",
        "llm.personality",
        "llm.webSearch",
        "llm.lookAtScreen",
        "llm.characterSheet",
        "llm.aboutMe",
        "llm.scenario",
        "llm.scenarioAudience",
        "persona.id",
        "persona.coreHumor",
        "persona.coreHumorPercent",
        "persona.npcHumor",
        "persona.npcHumorPercent",
        "persona.carrierHumor",
        "persona.carrierHumorPercent",
        "persona.introductions",
        "persona.avatarClips",
        "persona.shipCoreShip",
        "persona.shipCore",
        "audio.bed.level",
        "audio.bed.mute",
        "audio.bed.duck",
        "audio.music.now",
        "audio.music.next",
        "audio.music.level",
        "audio.music.mute",
        "audio.music.duck",
        "audio.cue.level",
        "audio.cue.mute",
        "audio.cue.duck",
        "audio.overheard.level",
        "audio.overheard.mute",
        "audio.speech.level",
        "audio.speech.mute",
        "audio.alert.level",
        "audio.alert.mute",
        "ui.theme",
        "ui.zoom",
        "ui.tabs",
        "hotkeys.openSettings",
        "hotkeys.openSettingsButton",
        "hotkeys.focusAsk",
        "hotkeys.focusAskButton",
        "hotkeys.find",
        "hotkeys.findButton",
        "ui.showEverySetting",
        "ui.overlay.enabled",
        "ui.overlay.scale",
        "ui.overlay.opacity",
        "hotkeys.showOverlay",
        "hotkeys.showOverlayButton",
        "hotkeys.moveOverlay",
        "hotkeys.moveOverlayButton",
        "vr.enabled",
        "vr.mode",
        "vr.opacity",
        "vr.controllers",
        "hotkeys.zoomHeadsetIn",
        "hotkeys.zoomHeadsetInButton",
        "hotkeys.zoomHeadsetOut",
        "hotkeys.zoomHeadsetOutButton",
        "hotkeys.resetHeadsetZoom",
        "hotkeys.resetHeadsetZoomButton",
        "hotkeys.resizeHeadsetPanel",
        "hotkeys.resizeHeadsetPanelButton",
        "vr.current.lock",
        "vr.current.distance",
        "vr.current.size",
        "vr.current.curve",
        "vr.current.scale",
        "vr.current.resolution",
        "vr.panel.lock",
        "vr.panel.distance",
        "vr.panel.size",
        "vr.panel.curve",
        "vr.panel.scale",
        "vr.panel.resolution",
        "vr.mini.lock",
        "vr.mini.distance",
        "vr.mini.size",
        "vr.mini.curve",
        "vr.mini.scale",
        "vr.mini.resolution",
        "vr.captions.enabled",
        "vr.captions.lock",
        "vr.captions.size",
        "vr.captions.background",
        "vr.captions.speed",
        "actions.keyboard",
        "actions.takeUsOut",
        "actions.separateAndEngage",
        "actions.separateAndSupercruise",
        "actions.requestDocking",
        "actions.honkOnArrival",
        "actions.autoPlot",
        "actions.chat",
        "hotkeys.commsChannelUp",
        "hotkeys.commsChannelUpButton",
        "hotkeys.commsChannelDown",
        "hotkeys.commsChannelDownButton",
        "hotkeys.commsSubChannelUp",
        "hotkeys.commsSubChannelUpButton",
        "hotkeys.commsSubChannelDown",
        "hotkeys.commsSubChannelDownButton",
        "actions.switches",
        "actions.headsetView",
        "actions.switchChords",
        "memory.enabled",
        "memory.expiryDays",
        "debrief.enabled",
        "logbook.voice",
        "logbook.range",
        "logbook.length",
        "updates.checkOnStartup",
        "privacy.usageCounts",
        "screenshots.convert",
        "about.version",
        "about.dataFolder",
        "about.changelog",
        "about.changelogOnline",
        "about.community",
        "about.setUpKeys",
        "callouts.lore",
        "callouts.loreCooldownDays",
        "callouts.weekBoundaryDay",
        "callouts.weekBoundaryHourUtc",
    };

    [Fact]
    public void EveryFeaturesLineHasStepsOrIsOnTheList()
    {
        var (_, _, _, registry, _) = TestSurface.CreateFull();
        var items = CoverageInventory.Of(registry);

        var tools = registry.All
            .SelectMany(capability => capability.Descriptor.Tools.Select(tool => (capability.Descriptor.Id, tool)))
            .ToDictionary(pair => (pair.Id, pair.tool.Name));
        var rows = registry.All
            .SelectMany(capability => capability.Descriptor.Settings.Select(row => (capability.Descriptor.Id, row)))
            .ToLookup(pair => (pair.Id, pair.row.Key));

        bool HasSteps(CoverageItem item) => item.Kind == CoverageKind.Tool
            ? !string.IsNullOrWhiteSpace(tools[(item.CapabilityId, item.Id)].tool.Exercise)
            : rows[(item.CapabilityId, item.Id)].Any(pair => !string.IsNullOrWhiteSpace(pair.row.Exercise));

        var toolsWithout = items.Where(item => item.Kind == CoverageKind.Tool && !HasSteps(item)).Select(item => item.Id).ToList();
        var settings = items.Where(item => item.Kind == CoverageKind.Setting).ToList();
        var missing = settings.Where(item => !HasSteps(item) && !WithoutSteps.Contains(item.Id)).Select(item => item.Id).ToList();
        var stale = settings.Where(item => HasSteps(item) && WithoutSteps.Contains(item.Id)).Select(item => item.Id).ToList();
        var known = settings.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var unknown = WithoutSteps.Where(key => !known.Contains(key)).ToList();

        Assert.True(toolsWithout.Count == 0, "Tools with no Exercise: " + string.Join(", ", toolsWithout));
        Assert.True(missing.Count == 0, "Settings with no Exercise, not on the list: " + string.Join(", ", missing));
        Assert.True(stale.Count == 0, "Now have steps, remove from the list: " + string.Join(", ", stale));
        Assert.True(unknown.Count == 0, "On the list but not a Features line: " + string.Join(", ", unknown));
    }
}
