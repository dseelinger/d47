using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// A stick button bound to push-to-talk is a way of being heard, and every sentence about the listening
/// path has to know it.
/// </summary>
public class AButtonIsAWayToBeHeardTests
{
    /// <summary>Zero-based in the store, one-based when spoken: this is "button 7".</summary>
    private const string Button = "WGqHNn6b6VE=#6";

    private static ListeningCapability.ListeningSurface Working() => new()
    {
        InputDevices = () => ["mic-1"],
        DeviceLabel = id => id == "mic-1" ? "Microphone (ROG DELTA II)" : id,
        SinceHeard = () => null,
        CaptureState = () => (true, null),
        TranscriberState = () => (true, "tiny.en", null),
        Binds = () => new D47.Core.Input.EliteBinds
        {
            PresetName = "KeyboardMouseOnly",
            SourceFile = "KeyboardMouseOnly.binds",
        },
        InstalledModels = () => ["tiny.en"],
        KeyLabel = key => key == "Oem4" ? "[" : key,
    };

    private static D47Settings With(string? key = null, string? button = null, string mode = "hold") => new()
    {
        Listening = new ListeningSettings
        {
            PushToTalkKey = key,
            PushToTalkButton = button,
            InputDevice = "mic-1",
            Mode = mode,
        },
    };

    /// <summary>The reported defect, in one assertion.</summary>
    [Fact]
    public void AButtonAloneIsNotNothingBound()
    {
        var text = ListeningCapability.Describe(With(button: Button), Working());

        Assert.StartsWith("Yes", text, StringComparison.Ordinal);
        Assert.DoesNotContain("No push-to-talk", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AButtonAloneSaysWhatToHold()
    {
        var text = ListeningCapability.Describe(With(button: Button), Working());

        Assert.Contains("Hold button 7 on your stick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BothBoundNamesBoth()
    {
        var text = ListeningCapability.Describe(With(key: "Oem4", button: Button), Working());

        Assert.Contains("Hold [ or button 7 on your stick", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The fault is still reported when there is genuinely nothing, and its wording now covers both — a
    /// Commander told to set a key would not learn that a button would do.
    /// </summary>
    [Fact]
    public void NeitherBoundIsStillAFaultAndNamesBothRemedies()
    {
        var text = ListeningCapability.Describe(With(), Working());

        Assert.StartsWith("No", text, StringComparison.Ordinal);
        Assert.Contains("No push-to-talk key or button is set", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Toggle is the gate policy rather than a property of what opens it, so it reads the same way for
    /// a button.
    /// </summary>
    [Fact]
    public void ToggleReadsTheSameWayForAButton()
    {
        var text = ListeningCapability.Describe(With(button: Button, mode: "toggle"), Working());

        Assert.Contains("Press button 7 on your stick to start", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInventoryNamesTheButton()
    {
        var text = ListeningCapability.DescribeInDetail(With(button: Button), Working());

        Assert.Contains("Push-to-talk: button 7 on your stick (hold).", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Push-to-talk: not set", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The pre-roll row applies to a button exactly as it does to a key — it covers the polling delay
    /// on whichever opened the gate, and both are polled on the same tick.
    /// </summary>
    [Fact]
    public void ThePreRollRowAppliesToAButton()
    {
        var row = Rows().Single(r => r.Key == ListeningCapability.PreRollKey);

        Assert.NotNull(row.AppliesWhen);
        Assert.True(row.AppliesWhen(With(button: Button)));
        Assert.True(row.AppliesWhen(With(key: "Oem4")));
        Assert.False(row.AppliesWhen(With()));
    }

    /// <summary>What the panel reads, which is the same answer the spoken path reads.</summary>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Oem4", null, "[")]
    [InlineData(null, Button, "button 7 on your stick")]
    [InlineData("Oem4", Button, "[ or button 7 on your stick")]
    public void ThePanelReadsTheSameGesture(string? key, string? button, string? expected)
    {
        Assert.Equal(
            expected,
            ListeningCapability.PushToTalkGesture(
                With(key, button).Listening,
                k => k == "Oem4" ? "[" : k));
    }

    /// <summary> A prompt does not name the button's number, because a number is not a thing a Commander can
    /// find. </summary>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Oem4", null, "[")]
    [InlineData(null, Button, "your push-to-talk button")]
    [InlineData("Oem4", Button, "[ or your push-to-talk button")]
    public void APromptDoesNotNameTheButtonsNumber(string? key, string? button, string? expected)
    {
        Assert.Equal(
            expected,
            ListeningCapability.PushToTalkGesture(
                With(key, button).Listening,
                k => k == "Oem4" ? "[" : k,
                nameTheButton: false));
    }

    [Fact]
    public void TheWaitingPromptStillSaysAGestureIsNeeded()
    {
        var gesture = ListeningCapability.PushToTalkGesture(
            With(button: Button).Listening,
            keyLabel: null,
            nameTheButton: false);

        Assert.Equal(
            "Hold your push-to-talk button and say it.",
            MicrophoneNarration.Prompt(ListeningCapability.HoldMode, [], gesture));
    }

    /// <summary>
    /// A stored value a hand-edited file could contain is an unbound button rather than a crash, and —
    /// the part worth asserting — rather than a bound one.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-hash")]
    [InlineData("trailing#")]
    [InlineData("negative#-1")]
    public void AnUnreadableBindingIsNotABoundOne(string stored)
    {
        var settings = With(button: stored);

        Assert.Null(ListeningCapability.PushToTalkGesture(settings.Listening, k => k));
        Assert.Contains("No push-to-talk key or button is set", ListeningCapability.Describe(settings, Working()), StringComparison.Ordinal);
    }

    private static IReadOnlyList<D47.Core.Capabilities.SettingRow> Rows()
    {
        using var install = new TempInstall();

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        return ListeningCapability.Create(settings, Working()).Settings;
    }
}
