using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>Every stick button is one slot in <see cref="ButtonSlots"/>, and the nine action buttons are polled together (#1549).</summary>
public sealed class TheStickButtonTableTests
{
    private const string Old = "hid:old";
    private const string New = "hid:new";

    private static readonly FoundDevice Found = new(Old, New, "stick", "GEAR", Fills: false);

    private static HotasReading Reading(string id, params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = id, VendorId = 1, ProductId = 2, Buttons = buttons };
    }

    private static D47Settings Bound(ButtonSlot slot, string? value) => slot.Write(new D47Settings(), value);

    [Fact]
    public void ASettingsFileWithoutTheNewPropertiesLoadsThemAllNull()
    {
        var settings = JsonSerializer.Deserialize<D47Settings>("{}", SettingsStore.Json)!;

        Assert.All(ButtonSlots.All, slot => Assert.Null(slot.Read(settings)));
    }

    [Fact]
    public void EverySlotRoundTripsThroughTheSettingsFile()
    {
        var settings = new D47Settings();

        foreach (var slot in ButtonSlots.All)
        {
            settings = slot.Write(settings, $"{Old}#{ButtonSlots.All.ToList().IndexOf(slot)}");
        }

        var read = JsonSerializer.Deserialize<D47Settings>(
            JsonSerializer.Serialize(settings, SettingsStore.Json), SettingsStore.Json)!;

        Assert.All(ButtonSlots.All, slot => Assert.Equal(slot.Read(settings), slot.Read(read)));
        Assert.All(ButtonSlots.All, slot => Assert.NotNull(slot.Read(read)));
    }

    [Fact]
    public void ASlotWritesOnlyItsOwnButton()
    {
        foreach (var slot in ButtonSlots.All)
        {
            var settings = Bound(slot, $"{Old}#3");

            Assert.Equal([slot.Key], ButtonSlots.All.Where(other => other.Read(settings) is not null).Select(other => other.Key));
        }
    }

    [Fact]
    public void EachSlotHasItsOwnKeyAndName()
    {
        Assert.Equal(ButtonSlots.All.Count, ButtonSlots.All.Select(slot => slot.Key).Distinct().Count());
        Assert.Equal(ButtonSlots.All.Count, ButtonSlots.All.Select(slot => slot.Named).Distinct().Count());
        Assert.Equal(15, ButtonSlots.All.Count);
    }

    [Fact]
    public void ARepointMovesEveryButtonOnTheOldIdAndNamesEach()
    {
        var settings = new D47Settings();

        foreach (var slot in ButtonSlots.All)
        {
            settings = slot.Write(settings, $"{Old}#5");
        }

        var next = Found.RepointButtons(settings, out var moved);

        Assert.Equal(ButtonSlots.All.Select(slot => slot.Named), moved);
        Assert.All(ButtonSlots.All, slot => Assert.Equal($"{New}#5", slot.Read(next)));
    }

    [Fact]
    public void ARepointLeavesAButtonOnAnotherDeviceAlone()
    {
        var slot = ButtonSlots.All[^1];
        var settings = Bound(slot, "hid:other#2");

        var next = Found.RepointButtons(settings, out var moved);

        Assert.Equal(settings, next);
        Assert.Empty(moved);
    }

    [Fact]
    public void ALegacyIdHeldByAnyButtonIsCarriedOver()
    {
        foreach (var slot in ButtonSlots.All)
        {
            var legacy = StoredIdCarryOver.LegacyPrefix + "stick}";

            Assert.Equal([legacy], StoredIdCarryOver.LegacyIds([], Bound(slot, $"{legacy}#1")));
        }
    }

    [Fact]
    public void EveryHotasButtonRowInTheRegistryHasASlot()
    {
        var surface = TestSurface.For(new MemoryInstall(), everyOptionalSurface: true);
        var slots = ButtonSlots.All.Select(slot => slot.Key).ToHashSet();

        var missing = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.Kind == SettingKind.HotasButton && !slots.Contains(row.Key))
            .Select(row => row.Key)
            .ToList();

        Assert.True(missing.Count == 0, $"Stick button rows without a ButtonSlots entry: {string.Join(", ", missing)}");
    }

    [Fact]
    public void APressRaisesOnceWithTheSlotsKeyOnTheFirstPollThatSeesItDown()
    {
        var slot = ButtonSlots.All.Single(s => s.Named == "find");
        var buttons = new ActionButtons();
        var pressed = new List<string>();
        buttons.Pressed += pressed.Add;
        buttons.Bind(Bound(slot, $"{Old}#4"));

        buttons.Poll([Reading(Old)]);
        buttons.Poll([Reading(Old, 4)]);
        buttons.Poll([Reading(Old, 4)]);

        Assert.Equal([slot.Key], pressed);

        buttons.Poll([Reading(Old)]);
        buttons.Poll([Reading(Old, 4)]);

        Assert.Equal([slot.Key, slot.Key], pressed);
    }

    [Fact]
    public void AnUnboundSlotNeverRaises()
    {
        var buttons = new ActionButtons();
        var pressed = new List<string>();
        buttons.Pressed += pressed.Add;
        buttons.Bind(new D47Settings());

        buttons.Poll([Reading(Old, Enumerable.Range(0, 32).ToArray())]);

        Assert.Empty(pressed);
    }

    [Fact]
    public void PushToTalkAndCancelAreNotRaisedByTheActionPoller()
    {
        var buttons = new ActionButtons();
        var pressed = new List<string>();
        buttons.Pressed += pressed.Add;
        var settings = ButtonSlots.All.Take(2).Aggregate(new D47Settings(), (s, slot) => slot.Write(s, $"{Old}#1"));
        buttons.Bind(settings);

        buttons.Poll([Reading(Old, 1)]);

        Assert.Empty(pressed);
    }

    [Fact]
    public void ARebindTakesEffectOnTheNextPoll()
    {
        var first = ButtonSlots.All.Single(s => s.Named == "find");
        var second = ButtonSlots.All.Single(s => s.Named == "show overlay");
        var buttons = new ActionButtons();
        var pressed = new List<string>();
        buttons.Pressed += pressed.Add;
        buttons.Bind(Bound(first, $"{Old}#4"));
        buttons.Poll([Reading(Old, 7)]);

        buttons.Bind(Bound(second, $"{Old}#7"));
        buttons.Poll([Reading(Old, 7)]);

        Assert.Equal([second.Key], pressed);
    }
}
