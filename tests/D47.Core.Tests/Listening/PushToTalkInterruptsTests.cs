using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Push-to-talk interrupts (<a href="https://github.com/dseelinger/d47/issues/218">#218</a>).
/// <para>
/// The Commander's words: <em>"I don't want to talk while the ship AI is talking. I want it to
/// shut up and listen."</em> So the press edge silences, and the <b>Stop speaking</b> row leaves
/// the surface of anybody who has push-to-talk bound.
/// </para>
/// <para>
/// All of it runs with no microphone, no stick and no audio device: the sources, the gate and the
/// rows are plain Core types, and the ordering guarantee lives inside <see cref="PushToTalkSources"/>
/// rather than in a subscription order at the call site — which is exactly what makes it assertable
/// here rather than only by ear.
/// </para>
/// </summary>
public class PushToTalkInterruptsTests
{
    private const int Rate = 16000;

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    /// <summary>The two sources and the gate, wired as <c>AppHost</c> wires them.</summary>
    private static (PushToTalkSources Sources, ListenGate Gate, List<string> Order) Wired(
        ListenMode mode = ListenMode.PushToTalk)
    {
        var order = new List<string>();
        var gate = new ListenGate(Rate, NullLogger<ListenGate>.Instance) { Mode = mode };

        var sources = new PushToTalkSources { Barge = () => order.Add("silenced") };

        sources.Pressed += () =>
        {
            order.Add("opened");
            gate.KeyDown(Start);
        };

        sources.Released += gate.KeyUp;

        return (sources, gate, order);
    }

    /// <summary>
    /// <b>Silence lands before the gate opens, not after.</b> The order is the whole of the fix: a
    /// press that opened the microphone first would capture d47's own voice in the pre-roll of the
    /// utterance the Commander is about to speak.
    /// </summary>
    [Fact]
    public void APressSilencesBeforeTheGateOpens()
    {
        var (sources, gate, order) = Wired();

        sources.KeyPressed();

        Assert.Equal(["silenced", "opened"], order);
        Assert.True(gate.IsListening);
    }

    /// <summary>
    /// <b>Not on the tap being recognised.</b> There is already a notion of a press too short to be
    /// speech — <see cref="UtteranceEnd.TooShort"/> — and hanging the interrupt there is the obvious
    /// mistake: it is only knowable at <em>release</em>, so a Commander who presses and starts
    /// talking would hear d47 over their first half-sentence, which is the complaint itself.
    /// </summary>
    [Fact]
    public void APressSilencesEvenWhenItIsTooShortToBeSpeech()
    {
        var (sources, gate, order) = Wired();
        UtteranceEnd? ended = null;
        gate.Ended += reason => ended = reason;

        sources.KeyPressed();
        sources.KeyReleased();

        Assert.Equal(UtteranceEnd.TooShort, ended);
        Assert.Equal(["silenced", "opened"], order);
    }

    /// <summary>Both sources, because the interrupt hangs where they are already one thing.</summary>
    [Fact]
    public void TheStickButtonSilencesJustAsTheKeyDoes()
    {
        var (sources, _, order) = Wired();

        sources.ButtonPressed();
        sources.ButtonReleased();

        Assert.Equal(["silenced", "opened"], order);
    }

    /// <summary>
    /// One press, one silence, however many fingers are down. The gate is opened by the first
    /// source and closed by the last release, and the interrupt follows that edge rather than
    /// firing per source.
    /// </summary>
    [Fact]
    public void HoldingBothAtOnceSilencesOnce()
    {
        var (sources, _, order) = Wired();

        sources.KeyPressed();
        sources.ButtonPressed();
        sources.KeyReleased();
        sources.ButtonReleased();

        Assert.Equal(["silenced", "opened"], order);
    }

    /// <summary>
    /// <b>Every press in toggle mode too.</b> A Commander pressing the key while d47 is talking
    /// means the same thing whichever mode they are in, and the second press — the one that closes
    /// the gate — silences nothing, because nothing is speaking by then.
    /// </summary>
    [Fact]
    public void ToggleModeSilencesOnBothPresses()
    {
        var (sources, gate, order) = Wired(ListenMode.Toggle);

        sources.KeyPressed();
        sources.KeyReleased();
        Assert.True(gate.IsListening);

        sources.KeyPressed();
        sources.KeyReleased();

        Assert.False(gate.IsListening);
        Assert.Equal(["silenced", "opened", "silenced", "opened"], order);
    }

    // ---- The row that push-to-talk has taken over ---------------------------------------------

    /// <summary>
    /// Out of the box push-to-talk is <c>RightShift</c>, so the row is gone from the surface of
    /// almost everybody — which is the ask rather than a side effect of it.
    /// </summary>
    [Fact]
    public void StopSpeakingIsOffTheSurfaceWhilePushToTalkIsBound()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var row = surface.Settings.Find(SpeechCapability.ShutUpHotkeyKey);

        Assert.NotNull(row);
        Assert.Equal("RightShift", surface.Settings.Current.Listening.PushToTalkKey);
        Assert.False(row.Applies(surface.Settings.Current));
    }

    /// <summary>
    /// <b>And it comes back when there is no push-to-talk at all.</b> This is the reason the row is
    /// hidden rather than retired: architecture.md §7 says a model must never be able to unbind the
    /// Commander's stop button, and push-to-talk can be cleared deliberately — its own help says
    /// <em>"Clear it and D47 never opens the microphone."</em> A Commander who has done that keeps a
    /// key-driven stop rather than losing every one.
    /// </summary>
    [Fact]
    public void ClearingBothPushToTalkBindingsPutsTheRowBack()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var row = surface.Settings.Find(SpeechCapability.ShutUpHotkeyKey)!;

        surface.Settings.Apply(ListeningCapability.PushToTalkKeyKey, "", SettingsCaller.Panel);
        Assert.True(row.Applies(surface.Settings.Current));

        // A stick button on its own is a push-to-talk too, so it takes the row away again.
        surface.Settings.Apply(
            ListeningCapability.PushToTalkButtonKey, "NonRoamable+Id/One=#10", SettingsCaller.Panel);

        Assert.False(row.Applies(surface.Settings.Current));
    }

    /// <summary>
    /// <b>A binding already set is not dropped by the row leaving the surface.</b> The settings file
    /// is append-only and <c>Speech.ShutUpHotkey</c> stays on the record: a build that discarded it
    /// would silently unbind the Commanders who had one, which is the failure this whole rule exists
    /// to prevent.
    /// </summary>
    [Fact]
    public void AStopSpeakingKeyAlreadySetSurvivesARoundTrip()
    {
        using var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        store.Save(new D47Settings
        {
            Speech = new SpeechSettings { ShutUpHotkey = "Ctrl+Alt+S" },
        });

        Assert.Equal("Ctrl+Alt+S", store.Load().Speech.ShutUpHotkey);
    }
}
