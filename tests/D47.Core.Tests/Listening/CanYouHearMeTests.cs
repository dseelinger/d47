using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// "Can you hear me?" is a question, and the answer to a question is an answer.
/// <para>
/// Reported from a running build: asking it produced a four-line inventory — the microphone,
/// the key and its mode, the Elite binding table, the loaded model — with no yes or no in it,
/// and produced it identically whether or not anything was wrong. Everything was working, and
/// the Commander had just been transcribed saying the words, which is the strongest possible
/// evidence and was the one thing not mentioned.
/// </para>
/// <para>
/// The inventory is not gone; it moved to <c>DescribeInDetail</c>, where a reader has asked
/// for the state of things that are fine. What a fault does is the opposite of being trimmed:
/// every broken part is named, with the thing to do about it.
/// </para>
/// </summary>
public class CanYouHearMeTests
{
    private static ListeningCapability.ListeningSurface Working(
        TimeSpan? sinceHeard = null,
        string? defaultDevice = null) => new()
    {
        InputDevices = () => ["mic-1"],
        DeviceLabel = id => id == "mic-1" ? "Microphone (ROG DELTA II)" : id,
        DefaultDeviceName = () => defaultDevice,
        SinceHeard = () => sinceHeard,
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

    private static D47Settings Settings(string? device = "mic-1") => new()
    {
        Listening = new ListeningSettings { PushToTalkKey = "Oem4", InputDevice = device },
    };

    /// <summary>
    /// Heard a moment ago, so the answer is the demonstration. No inventory, and above all no
    /// binding table — being told which Elite keys are free is not an answer to this question.
    /// </summary>
    [Fact]
    public void HavingJustHeardThemIsTheAnswer()
    {
        var text = ListeningCapability.Describe(Settings(), Working(sinceHeard: TimeSpan.FromSeconds(3)));

        Assert.StartsWith("Yes", text, StringComparison.Ordinal);
        Assert.Contains("just heard you", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Elite binding", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Transcription:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Push-to-talk:", text, StringComparison.Ordinal);
    }

    /// <summary>Typed, or after a long silence: still yes, with what to do to prove it.</summary>
    [Fact]
    public void NeverHavingHeardThemIsStillYesWhenEverythingIsReady()
    {
        var text = ListeningCapability.Describe(Settings(), Working(sinceHeard: null));

        Assert.StartsWith("Yes", text, StringComparison.Ordinal);
        Assert.Contains("Hold [", text, StringComparison.Ordinal);
        Assert.Contains("tiny.en", text, StringComparison.Ordinal);
    }

    /// <summary>An hour-old transcription is not evidence about now.</summary>
    [Fact]
    public void HavingHeardThemLongAgoIsNotADemonstration()
    {
        var text = ListeningCapability.Describe(Settings(), Working(sinceHeard: TimeSpan.FromHours(1)));

        Assert.DoesNotContain("just heard you", text, StringComparison.Ordinal);
        Assert.StartsWith("Yes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ATranscriberThatIsNotReadyIsAClearNoWithTheReason()
    {
        var surface = Working() with
        {
            TranscriberState = () => (false, null, "No speech model is selected."),
        };

        var text = ListeningCapability.Describe(Settings(), surface);

        Assert.StartsWith("No", text, StringComparison.Ordinal);
        Assert.Contains("No speech model is selected.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AMicrophoneThatIsNotCapturingIsAClearNoWithTheReason()
    {
        var surface = Working() with
        {
            CaptureState = () => (false, "The selected microphone is not available."),
        };

        var text = ListeningCapability.Describe(Settings(), surface);

        Assert.StartsWith("No", text, StringComparison.Ordinal);
        Assert.Contains("The selected microphone is not available.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NoKeyBoundIsAClearNoAndSaysWhatToDo()
    {
        var text = ListeningCapability.Describe(
            new D47Settings { Listening = new ListeningSettings { PushToTalkKey = null } },
            Working());

        Assert.StartsWith("No", text, StringComparison.Ordinal);
        Assert.Contains("push-to-talk key", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A double-bound key has no symptom other than one of the two silently not working, so it
    /// is worth interrupting for even when everything else is healthy — unlike the all-clear.
    /// </summary>
    [Fact]
    public void ACollidingKeyIsSaidEvenThoughEverythingElseWorks()
    {
        var surface = Working(sinceHeard: TimeSpan.FromSeconds(2)) with
        {
            Binds = () => new D47.Core.Input.EliteBinds
            {
                PresetName = "KeyboardMouseOnly",
                SourceFile = "KeyboardMouseOnly.binds",
                Bindings = [new D47.Core.Input.EliteBinding("UIFocus", "Primary", "Keyboard", "Oem4")],
            },
        };

        var text = ListeningCapability.Describe(Settings(), surface);

        Assert.Contains("also bound in Elite", text, StringComparison.Ordinal);
        Assert.Contains("UIFocus", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The default is the one choice made without seeing what was chosen, and on this machine
    /// it resolved to a virtual endpoint that delivers silence. Naming it is the difference
    /// between an answer that can be acted on and one that cannot.
    /// </summary>
    [Fact]
    public void TheSystemDefaultIsNamedRatherThanLeftAsAPhrase()
    {
        var text = ListeningCapability.Describe(
            Settings(device: null),
            Working(sinceHeard: TimeSpan.FromSeconds(2), defaultDevice: "Microphone (Virtual Desktop Audio)"));

        Assert.Contains("the system default (Microphone (Virtual Desktop Audio))", text, StringComparison.Ordinal);
    }

    /// <summary>Nothing to resolve it with degrades to the old phrasing rather than to a lie.</summary>
    [Fact]
    public void AnUnresolvableDefaultStillReadsSensibly()
    {
        var text = ListeningCapability.Describe(
            Settings(device: null),
            Working(sinceHeard: TimeSpan.FromSeconds(2), defaultDevice: null));

        Assert.Contains("the system default", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(", text[text.IndexOf("system default", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }
}
