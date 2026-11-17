using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class EliteAttachingFirstCarriesTheLayersSetTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void TheLayersSetJoinsElitesAttachAfterOneSuggestionCarryingElitesBindings()
    {
        rig.Point(true);
        rig.EliteSuggests((0xE1A, "/user/hand/right/input/a/click"));

        var result = rig.EliteAttaches(ControllerCalls.ElitesSet);

        Assert.Equal(XrResult.Success, result);
        Assert.Equal(
            [.. ControllerCalls.Prepare, nameof(FakeNext.AttachSessionActionSets), .. ControllerCalls.Spaces],
            rig.Next.ActionCalls);
        var attach = Assert.Single(rig.Next.Attaches);
        Assert.False(attach.IsElitesOwn);
        Assert.Equal([ControllerCalls.ElitesSet, FakeNext.LayerSet], attach.Sets);

        var suggestion = Assert.Single(rig.Next.Suggestions);
        Assert.Equal(rig.Next.Path(LayerRig.TouchProfile), suggestion.Profile);
        Assert.Equal(0xE1Aul, suggestion.Bindings[0].Action);
        Assert.Equal("/user/hand/right/input/a/click", rig.Next.PathName(suggestion.Bindings[0].Binding));
        Assert.Equal(ControllerCalls.Bindings, suggestion.Bindings.Skip(1).Select(b => rig.Next.PathName(b.Binding)));
    }

    [Fact]
    public void ElitesSyncCarriesTheLayersSetAndTheLayerMakesNoSyncOfItsOwn()
    {
        rig.Point(true);
        rig.EliteAttaches(ControllerCalls.ElitesSet);
        rig.Focus();
        rig.Next.Calls.Clear();

        for (var frame = 0; frame < 3; frame++)
        {
            rig.EliteSyncs(ControllerCalls.ElitesSet);
            rig.EndFrame(frame * LayerRig.Millisecond);
        }

        Assert.Equal(3, rig.Next.Syncs.Count);
        Assert.All(rig.Next.Syncs, sync => Assert.Equal([ControllerCalls.ElitesSet, FakeNext.LayerSet], sync.Sets));
        Assert.Equal(
            [.. Frame(), .. Frame(), .. Frame()],
            rig.Next.ActionCalls);

        static string[] Frame() => [nameof(FakeNext.SyncActions), .. ControllerCalls.Reads];
    }

    [Fact]
    public void EachHandIsPublishedWithItsPosesAndValuesLeftFirst()
    {
        rig.Point(true);
        rig.EliteAttaches(ControllerCalls.ElitesSet);
        rig.Next.Values[("trigger", "/user/hand/left")] = 0.25f;
        rig.Next.Values[("squeeze", "/user/hand/right")] = 0.9f;

        rig.EliteSyncs(ControllerCalls.ElitesSet);
        rig.EndFrame(0);

        var hands = rig.Published.Hands;
        Assert.Equal(1u, hands[0].Valid);
        Assert.Equal(1u, hands[1].Valid);
        Assert.Equal((1f, -1f), (hands[0].Aim.PositionX, hands[0].Aim.PositionY));
        Assert.Equal((2f, -1f), (hands[0].Grip.PositionX, hands[0].Grip.PositionY));
        Assert.Equal((1f, 1f), (hands[1].Aim.PositionX, hands[1].Aim.PositionY));
        Assert.Equal((0.25f, 0f), (hands[0].TriggerValue, hands[0].GripValue));
        Assert.Equal((0f, 0.9f), (hands[1].TriggerValue, hands[1].GripValue));
        Assert.Equal(XrControllerRefusal.None, rig.Published.ControllerRefusal);
    }

    [Fact]
    public void DestroyingTheSessionDestroysTheHandSpacesAndTheSet()
    {
        rig.Point(true);
        rig.EliteAttaches(ControllerCalls.ElitesSet);
        rig.EliteSyncs(ControllerCalls.ElitesSet);
        rig.EndFrame(0);
        rig.Next.Calls.Clear();

        rig.Session.Destroy();

        // Four hand spaces, then the VIEW and LOCAL spaces.
        Assert.Equal(6, rig.Next.Calls.Count(c => c == nameof(FakeNext.DestroySpace)));
        Assert.Equal([nameof(FakeNext.DestroyActionSet)], rig.Next.ActionCalls);
        Assert.Equal(0u, rig.Published.Hands[0].Valid);
        Assert.Equal(0u, rig.Published.Hands[1].Valid);
    }

    public void Dispose() => rig.Dispose();
}
