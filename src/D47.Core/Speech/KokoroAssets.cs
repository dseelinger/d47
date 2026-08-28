namespace D47.Core.Speech;

/// <summary>One file the local voice needs, and what it should be when it lands.</summary>
/// <param name="Path">Where it sits under the models folder, and its path in the repository.</param>
/// <param name="Repository">The Hugging Face repository it comes from.</param>
/// <param name="Bytes">Its size, so a Commander is told what they are agreeing to.</param>
/// <param name="Sha256">
/// The hash it is expected to have, pinned here rather than taken from whatever the host says on
/// the day — the rule <see cref="Listening.WhisperModels"/> already follows, and for the same
/// reason: the hash and the bytes came from the same server, so anything able to serve different
/// bytes could serve the hash for them. Read on 2026-08-28.
/// </param>
public sealed record KokoroAsset(string Path, string Repository, long Bytes, string Sha256)
{
    public string Url => $"https://huggingface.co/{Repository}/resolve/main/{Path}";

    public double Megabytes => Bytes / 1024.0 / 1024.0;
}

/// <summary>
/// What has to be on this machine before d47 can speak without the internet (#101, Phase 59).
/// <para>
/// <b>Downloaded rather than shipped, which is what makes fp32 affordable.</b> The spike framed the
/// choice as multiplying a 70 MB installer by five; it does not have to be in the installer at all.
/// d47 already downloads a speech-to-text model, verifies it against a pinned hash and discards one
/// that does not match, so this is a second thing that road carries. The Commander chose fp32 on
/// 2026-08-28: it is the fastest at ×9 realtime, and the smallest variant is four times slower than
/// the largest, which is backwards from the usual assumption.
/// </para>
/// <para>
/// <b>The neural phonemiser is deliberately not here.</b> Its repository ships a 61 MB
/// grapheme-to-phoneme model that measured 0.0% exact on words from its own training set, and
/// <see cref="Phonemiser"/> replaces it with rules. Only the dictionary is taken.
/// </para>
/// </summary>
public static class KokoroAssets
{
    public const string ModelRepository = "onnx-community/Kokoro-82M-v1.0-ONNX";

    public const string DictionaryRepository = "lookbe/open-phonemizer-onnx";

    /// <summary>
    /// The model itself, in the fp32 build the Commander chose.
    /// </summary>
    public static readonly KokoroAsset Model = new(
        "onnx/model.onnx",
        ModelRepository,
        325_532_232,
        "8fbea51ea711f2af382e88c833d9e288c6dc82ce5e98421ea61c058ce21a34cb");

    /// <summary>
    /// The phoneme vocabulary. Read rather than transcribed into the source: a hand-copied
    /// vocabulary is a silent mismatch waiting to happen, and it is 3 kB.
    /// </summary>
    public static readonly KokoroAsset Tokenizer = new(
        "tokenizer.json",
        ModelRepository,
        3_497,
        string.Empty);

    /// <summary>
    /// The 274,927-entry pronunciation dictionary, which is the top rung of the ladder and the
    /// reason ordinary English comes out right rather than by rule.
    /// </summary>
    public static readonly KokoroAsset Dictionary = new(
        "phoneme_dict.json",
        DictionaryRepository,
        10_596_227,
        "10929ae8e27bac10853c88ce39867349be0793ae53e4514e7003626e45bde3e5");

    /// <summary>
    /// The English voices, which is all of them d47 offers.
    /// <para>
    /// <b>The prefix carries the accent and that is load-bearing rather than trivia</b>:
    /// <c>af</c>/<c>am</c> are American and <c>bf</c>/<c>bm</c> British, which is how
    /// <see cref="SpokenLetters.AccentOf"/> knows whether a spelled <c>Z</c> is <em>zee</em> or
    /// <em>zed</em>.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<KokoroAsset> Voices =
    [
        new("voices/af_alloy.bin", ModelRepository, 522240, "c4a6b876047fd7fb472edf4ebd63cfac7c3b958a7cae7c106e8f038ca6308c45"),
        new("voices/af_aoede.bin", ModelRepository, 522240, "4a004c33430762e2461eedb2013fad808ef4ab3121f5300f554476caf58d8361"),
        new("voices/af_bella.bin", ModelRepository, 522240, "f69d836209b78eb8c66e75e3cda491e26ea838a3674257e9d4e5703cbaf55c8b"),
        new("voices/af_heart.bin", ModelRepository, 522240, "d583ccff3cdca2f7fae535cb998ac07e9fcb90f09737b9a41fa2734ec44a8f0b"),
        new("voices/af_jessica.bin", ModelRepository, 522240, "a240a5e3c15b43563d6e923bdca8ef5613a23471d9b77653694012435df23bd8"),
        new("voices/af_kore.bin", ModelRepository, 522240, "9be5221b6a941c04b561959b8ff0b06e809444dcc4ab7e75a7b23606f691819e"),
        new("voices/af_nicole.bin", ModelRepository, 522240, "cd2191ab31b914ed7b318416b0e4440fdf392ddad9106a060819aa600a64f59a"),
        new("voices/af_nova.bin", ModelRepository, 522240, "18778272caa0d0eebaea251c35fd635f038434f9eee5e691d02a174bd328414f"),
        new("voices/af_river.bin", ModelRepository, 522240, "00a2bcf82b1d86e8f19902ede58c65ccf6c0e43b44b7d74fad54e5d8933c9c30"),
        new("voices/af_sarah.bin", ModelRepository, 522240, "4409fbc125afabacc615d94db5398d847006a737b0247d6892b7a9a0007a2f0a"),
        new("voices/af_sky.bin", ModelRepository, 522240, "4435255c9744f3f31659e0d714ab7689bf65d9e77ec1cce060f083912614f0b9"),
        new("voices/am_adam.bin", ModelRepository, 522240, "162b035ed91cfc48b6046982184c645f72edcdd1b82843347f605d7bf7b15716"),
        new("voices/am_echo.bin", ModelRepository, 522240, "3968b92c3c4cd1c4416dbded36c13eaa388a90d5788d02a13e4d781f5f8cf3c3"),
        new("voices/am_eric.bin", ModelRepository, 522240, "e8b5be17edd1e3636901ce7598baafe2dc8dd8ff707a0c23bf9e461add7e2832"),
        new("voices/am_fenrir.bin", ModelRepository, 522240, "c27989f741f7ee34d273a39d8a595cc0837d35f5ced9a29b7cc162614616df43"),
        new("voices/am_liam.bin", ModelRepository, 522240, "52403be32fd047c6a44517cb0bcd6b134f2a18baa73e70ef41651e0eab921ade"),
        new("voices/am_michael.bin", ModelRepository, 522240, "1d1f21dd8da39c30705cd4c75d039d265e9bc4a2a93ed09bc9e1b1225eb95ba1"),
        new("voices/am_onyx.bin", ModelRepository, 522240, "da5d135b424164916d75a68ffb4c2abce3d7d5ccc82dd1ee6cf447ce286145e6"),
        new("voices/am_puck.bin", ModelRepository, 522240, "fcf73c989033e9233e0b98713eca600c8c74dcc1614b37009d5450ff4a2274a0"),
        new("voices/am_santa.bin", ModelRepository, 522240, "61150cf726ab6c5ed7a99f90a304f91f5a72c00c592e89ec94e5df11c319227a"),
        new("voices/bf_alice.bin", ModelRepository, 522240, "08afa6ba24da61ea5e8efa139e5aadc938d83f0a6da5a900adaf763ac1da5573"),
        new("voices/bf_emma.bin", ModelRepository, 522240, "669fe0647f9dd04fcab92f1439a40eeb4c8b4ab1f82e4996fe3d918ce4a63b73"),
        new("voices/bf_isabella.bin", ModelRepository, 522240, "3754352c4aaa46d17f27654ab7518d65b62ad6163a0f55a5f4330c2da2c4e94f"),
        new("voices/bf_lily.bin", ModelRepository, 522240, "5e0ee32ebe64a467124976b14e69590746f1c4ce41a12b587a50c862edfea335"),
        new("voices/bm_daniel.bin", ModelRepository, 522240, "6b3194bbceffb746733cbc22c8f593dd44e401a71d53895a2dca891bc595a1e8"),
        new("voices/bm_fable.bin", ModelRepository, 522240, "f889083196807b4adb15e9204252165f503b8d33d3982e681c52443c49d798f1"),
        new("voices/bm_george.bin", ModelRepository, 522240, "c4b235a4c1f2cd3b939fed08b899ce9385638b763f7b73a59616c4fc9bd6c9bc"),
        new("voices/bm_lewis.bin", ModelRepository, 522240, "b8f671cef828c30e66fdf0b0756a76bba58f6bb3398cbbf27058642acbcedb97"),
    ];

    /// <summary>Just the ids, which is what a voice picker and a settings file deal in.</summary>
    public static IReadOnlyList<string> VoiceIds { get; } =
        [.. Voices.Select(voice => System.IO.Path.GetFileNameWithoutExtension(voice.Path))];

    /// <summary>
    /// How a voice is labelled in the picker. Derived from the id rather than tabled, because the
    /// id already says everything: accent, gender and name.
    /// </summary>
    public static string Label(string voiceId)
    {
        if (voiceId.Length < 4 || voiceId[2] != '_')
        {
            return voiceId;
        }

        var name = char.ToUpperInvariant(voiceId[3]) + voiceId[4..];
        var accent = voiceId[0] == 'b' ? "British" : "American";
        var gender = voiceId[1] == 'f' ? "female" : "male";

        return $"{name} — {gender}, {accent}";
    }

    /// <summary>Whether every file is present, which is what the settings row reports.</summary>
    public static bool IsInstalled(string folder) =>
        File.Exists(System.IO.Path.Combine(folder, "model.onnx"))
        && File.Exists(System.IO.Path.Combine(folder, "tokenizer.json"))
        && File.Exists(System.IO.Path.Combine(folder, "phoneme_dict.json"))
        && VoiceIds.All(voice =>
            File.Exists(System.IO.Path.Combine(folder, "voices", voice + ".bin")));

    /// <summary>
    /// Roughly what the whole thing costs to fetch, for the sentence a Commander reads before
    /// agreeing to it.
    /// </summary>
    public static double TotalMegabytes =>
        (Model.Bytes + Dictionary.Bytes + Tokenizer.Bytes + Voices.Sum(voice => voice.Bytes))
        / 1024.0 / 1024.0;
}
