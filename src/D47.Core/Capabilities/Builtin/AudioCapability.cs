using System.Globalization;
using D47.Core.Audio;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The mixer: how loud each category is, whether it is muted, and how far it drops while d47 is
/// speaking (Phase 12, "#96 Ambient audio mixer").
/// <para>
/// <b>No tool surface.</b> Every row here is reachable from the panel and by the model-free
/// keyword router, and none of it is reachable by the model — a model that can turn its own
/// voice down, or an alert's, is a model that can make itself and the danger callouts harder to
/// hear, and there is no request that needs it. That is the same reasoning that keeps the
/// output device and the voice off the tool surface (architecture.md §7).
/// </para>
/// <para>
/// The rows are generated from <see cref="AudioChannel"/> rather than written out one at a time,
/// so a category added later cannot ship without them. That is not tidiness: a channel with no
/// level row is a channel the Commander cannot turn down, and the way that presents is "d47 is
/// too loud and there is no setting for it".
/// </para>
/// </summary>
public static class AudioCapability
{
    public const string Id = "audio";

    /// <summary>Row keys, so a caller naming one does not spell it by hand.</summary>
    public static string LevelKey(AudioChannel channel) => $"audio.{Slug(channel)}.level";

    public static string MuteKey(AudioChannel channel) => $"audio.{Slug(channel)}.mute";

    public static string DuckKey(AudioChannel channel) => $"audio.{Slug(channel)}.duck";

    /// <summary>
    /// What each category is, in the Commander's words rather than the enum's. "Bed" is a
    /// mixing term and "Cue" is an internal one; neither says what the sound is.
    /// </summary>
    private static (string Name, string What) Describe(AudioChannel channel) => channel switch
    {
        AudioChannel.Bed => ("Thinking bed", "the loop that plays underneath a turn while D47 works"),
        AudioChannel.Music => ("Ambient music", "the background layer that follows what you are doing"),
        AudioChannel.Cue => ("Sound cues", "the short markers for listening, thinking and answering"),
        AudioChannel.Speech => ("Speech", "everything D47 says out loud"),
        _ => ("Alerts", "the danger callouts, which are the one thing that cuts in mid-sentence"),
    };

    private static string Slug(AudioChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>The drop-in folder row's key.</summary>
    public const string DropsKey = "audio.drops";

    /// <param name="drops">
    /// What was picked up from <c>data/audio/</c> and what was skipped, in words. Null where
    /// there is no folder to describe — under the designer, and in a test that is not about it.
    /// </param>
    public static CapabilityDescriptor Create(Func<string>? drops = null) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Audio mixer",
        Summary = "How loud each kind of sound is, and how far it drops while D47 is speaking.",
        Display = new CapabilityDisplay
        {
            PanelTitle = "Audio mixer",

            // Stated rather than defaulted (#83). This declared no order at all and so took
            // `CapabilityDisplay.Order`'s default of 100 — which is past Diagnostics, past Privacy
            // and past About, so this card was the bottom of the left nav and nothing anywhere
            // said it should be. It is the only capability that relied on that default, and the
            // default is a sink: anything that forgets an order lands underneath the page's
            // deliberate ending.
            //
            // 96 keeps it exactly where a Commander is used to finding it — below Privacy, above
            // About — while making the position a decision rather than an omission.
            Order = 96,
            StartCollapsed = true,
        },
        Settings =
        [
            .. Enum.GetValues<AudioChannel>().SelectMany(RowsFor),
            .. drops is null ? Array.Empty<SettingRow>() : [DropsRow(drops)],
        ],
    };

    /// <summary>
    /// What d47 found in the Commander's own folder — and, more to the point, what it could not
    /// use. A file that was skipped is silent in exactly the way a file that is missing is, so
    /// without this the only symptom of a wrong sample rate is a cue that never plays.
    /// </summary>
    private static SettingRow DropsRow(Func<string> drops) => new()
    {
        Key = DropsKey,
        Advanced = true,
        Label = "Your own audio",
        Help = "Drop 16-bit mono 48 kHz .wav files into data/audio: cues/<state>.wav replaces a "
               + "sound cue, beds/<name>.wav adds a thinking bed, and music/<situation>/*.wav is "
               + "ambience. They are picked up without a restart.",
        Kind = SettingKind.Info,
        Group = "Your own audio",
        GroupHelp = "What D47 found beside the set it ships with.",
        DocsAnchor = "drop-in-folder",
        Binding = new SettingBinding { Read = _ => drops() },
    };

    private static IEnumerable<SettingRow> RowsFor(AudioChannel channel)
    {
        var (name, what) = Describe(channel);
        var group = name;
        var groupHelp = $"Level and mute for {what}.";

        yield return new SettingRow
        {
            Key = LevelKey(channel),
            Advanced = true,
            Label = "Level",
            Help = "0 is silent and 1 is full. Muting is separate, so turning something off does not "
                   + "cost you the level you had it at.",
            Kind = SettingKind.Number,
            Step = 0.05,
            Minimum = 0,
            Maximum = 1,
            Group = group,
            GroupHelp = groupHelp,
            DocsAnchor = $"{Slug(channel)}-level",
            Binding = Bind(channel, mix => Number(mix.Level), (mix, v) => mix with { Level = Fraction(v, mix.Level) }),
        };

        yield return new SettingRow
        {
            Key = MuteKey(channel),
            Advanced = true,
            Label = "Mute",
            Help = "Off, without losing the level. A level of zero and a mute sound the same and mean "
                   + "different things.",
            Kind = SettingKind.Toggle,
            Group = group,
            GroupHelp = groupHelp,
            DocsAnchor = $"{Slug(channel)}-mute",
            Binding = Bind(
                channel,
                mix => mix.Muted ? "true" : "false",
                (mix, v) => mix with { Muted = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) }),
        };

        // Speech and alerts are what everything else ducks under. A row asking how far speech
        // drops while speech is playing is a row with no answer, and offering it would invite
        // a Commander to set a number that does nothing (Phase 4: a row that does not
        // apply is absent rather than disabled).
        if (!AudioMix.Ducks(channel))
        {
            yield break;
        }

        yield return new SettingRow
        {
            Key = DuckKey(channel),
            Advanced = true,
            Label = "Duck while speaking",
            Help = "What this drops to while D47 is talking, as a fraction of its level. 1 does not duck "
                   + "at all; 0 goes silent until the sentence ends.",
            Kind = SettingKind.Number,
            Step = 0.05,
            Minimum = 0,
            Maximum = 1,
            Group = group,
            GroupHelp = groupHelp,
            DocsAnchor = $"{Slug(channel)}-duck",
            Binding = Bind(
                channel,
                mix => Number(mix.DuckUnderSpeech),
                (mix, v) => mix with { DuckUnderSpeech = Fraction(v, mix.DuckUnderSpeech) }),
        };
    }

    private static SettingBinding Bind(
        AudioChannel channel,
        Func<ChannelMix, string?> read,
        Func<ChannelMix, string?, ChannelMix> write) => new()
        {
            Read = s => read(s.Audio.For(channel)),
            Write = (s, v) => s with { Audio = s.Audio.With(channel, write(s.Audio.For(channel), v)) },
        };

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Clamped rather than refused. Every value here is a fraction, and the only way to arrive
    /// with 1.4 is a hand-edited file — where the Commander plainly meant "as loud as it goes"
    /// and a refusal would leave d47 using whatever was there before.
    /// </summary>
    private static double Fraction(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0, 1)
            : fallback;
}
