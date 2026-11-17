using D47.App.Headset;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public sealed class TheOpenXrLayerValueFollowsTheHeadsetSettingsTests
{
    private const string Mine = @"C:\Users\x\AppData\Local\Programs\d47\runtimes\openxr\XR_APILAYER_D47_panel.json";
    private const string Copy = @"C:\dev\d47\src\D47.App\bin\Debug\runtimes\openxr\XR_APILAYER_D47_panel.json";
    private const string Other = @"C:\Program Files\Other\XR_APILAYER_OTHER.json";

    private static readonly VrSettings On = new() { Enabled = true, OpenXrLayer = true };

    private sealed class StandInStore : IImplicitLayerStore
    {
        public List<string> Values { get; } = [];

        public IReadOnlyList<string> ValueNames() => [.. Values];

        public void Add(string manifestPath)
        {
            if (!Values.Contains(manifestPath, StringComparer.OrdinalIgnoreCase))
            {
                Values.Add(manifestPath);
            }
        }

        public void Remove(string manifestPath) =>
            Values.RemoveAll(v => string.Equals(v, manifestPath, StringComparison.OrdinalIgnoreCase));
    }

    private static OpenXrLayerRegistration Over(StandInStore store) =>
        new(store, Mine, NullLogger<OpenXrLayerRegistration>.Instance);

    [Fact]
    public void BothSettingsOnAddTheValue()
    {
        var store = new StandInStore();
        Over(store).Apply(On);
        Assert.Equal([Mine], store.Values);
    }

    [Fact]
    public void TheLayerSettingOffRemovesTheValue()
    {
        var store = new StandInStore();
        store.Values.Add(Mine);
        Over(store).Apply(On with { OpenXrLayer = false });
        Assert.Empty(store.Values);
    }

    [Fact]
    public void ShowingInTheHeadsetOffRemovesTheValue()
    {
        var store = new StandInStore();
        store.Values.Add(Mine);
        Over(store).Apply(On with { Enabled = false });
        Assert.Empty(store.Values);
    }

    [Fact]
    public void AValueLeftByAnotherCopyIsReplaced()
    {
        var store = new StandInStore();
        store.Values.AddRange([Copy, Other]);
        Over(store).Apply(On);
        Assert.Equal([Other, Mine], store.Values);
    }

    [Fact]
    public void ExitRemovesTheValue()
    {
        var store = new StandInStore();
        var registration = Over(store);
        registration.Apply(On);
        registration.RemoveAtExit();
        Assert.Empty(store.Values);
    }

    [Fact]
    public void AValueLeftByARunThatDidNotExitIsRemovedAtStartWithTheSettingOff()
    {
        var store = new StandInStore();
        store.Values.AddRange([Mine, Copy, Other]);
        Over(store).Apply(new VrSettings { Enabled = true, OpenXrLayer = false });
        Assert.Equal([Other], store.Values);
    }

    [Fact]
    public void AnUpdateHandedOverLeavesTheSuccessorsValueAtExit()
    {
        var store = new StandInStore();
        var registration = Over(store);
        registration.Apply(On);
        registration.HandOver();
        registration.RemoveAtExit();
        Assert.Equal([Mine], store.Values);
    }

    [Fact]
    public void TheManifestIsBesideTheExeUnderRuntimesOpenxr() =>
        Assert.Equal(
            Path.Combine(@"C:\app", "runtimes", "openxr", "XR_APILAYER_D47_panel.json"),
            OpenXrLayerRegistration.ManifestPathBeside(@"C:\app"));
}
