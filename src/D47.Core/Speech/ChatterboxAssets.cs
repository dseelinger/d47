namespace D47.Core.Speech;

/// <summary>What has to be on this machine before Chatterbox Turbo can speak: its q4 graphs and tokenizer.</summary>
public static class ChatterboxAssets
{
    public const string Repository = "ResembleAI/chatterbox-turbo-ONNX";

    public const string Host = "huggingface.co";

    /// <summary>The four graphs, each an <c>.onnx</c> file and the <c>.onnx_data</c> it names, which must sit beside it.</summary>
    public static readonly IReadOnlyList<KokoroAsset> Graphs =
    [
        new("onnx/embed_tokens_q4.onnx", Repository, 2_844,
            "fd6ba1d22902e8f539d3dd6d7c1c44b98ebb4c84ebbb5e47fcb826ddcf667561"),
        new("onnx/embed_tokens_q4.onnx_data", Repository, 37_286_384,
            "f54a51e234b509b64c3a03bb79e1149fba7e2eba6c2d9c222f18883379e1f5d8"),
        new("onnx/speech_encoder_q4.onnx", Repository, 1_200_346,
            "37956c20b67bed85a0da4bc83509d67b5969a1b257d1c546516a5236a17ad71e"),
        new("onnx/speech_encoder_q4.onnx_data", Repository, 229_560_112,
            "58956db217c6443e49c91bdd54d7cf76b4a243f225c748b7bf746459fc27bc7d"),
        new("onnx/language_model_q4.onnx", Repository, 274_572,
            "b39d03d3f8b943b9e60c6fce3fb41191dbc1df4589f913291db1e214eef669b1"),
        new("onnx/language_model_q4.onnx_data", Repository, 204_456_572,
            "2c029dc0acf48752473d8c74c72b5ceaaad76b9886fe106eaf2022142d5b5d5e"),
        new("onnx/conditional_decoder_q4.onnx", Repository, 2_179_022,
            "dccb7a6cea3472dc7f7d070eeb70ade18e6327fb4ec61a3d62cf211bfed90ea2"),
        new("onnx/conditional_decoder_q4.onnx_data", Repository, 246_397_384,
            "b5c5317e0b79a1a19dd3d5e2b2091ea06b15716716ab801a54eaeb906c6971ec"),
    ];

    /// <summary>The byte-level BPE vocabulary and the paralinguistic tags in its <c>added_tokens</c>.</summary>
    public static readonly KokoroAsset Tokenizer = new(
        "tokenizer.json",
        Repository,
        3_562_272,
        "3f04e34bea22f9144d1a19151154095bc9ce0430bf421304f5797e716288a906");

    /// <summary>Every file, in the order they are fetched.</summary>
    public static IReadOnlyList<KokoroAsset> All { get; } = [Tokenizer, .. Graphs];

    /// <summary>Where one graph's model file lands under <paramref name="folder"/>.</summary>
    public static string GraphPath(string folder, string graph) =>
        System.IO.Path.Combine(folder, "onnx", graph + "_q4.onnx");

    /// <summary>Where a repository path lands under <paramref name="folder"/>; the layout is the repository's own.</summary>
    public static string Destination(string folder, KokoroAsset asset) =>
        System.IO.Path.Combine(folder, asset.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>Whether every file is present at its pinned size.</summary>
    public static bool IsInstalled(string folder) =>
        All.All(asset => new FileInfo(Destination(folder, asset)) is { Exists: true } file
                         && file.Length == asset.Bytes);

    public static double TotalMegabytes => All.Sum(asset => asset.Bytes) / 1024.0 / 1024.0;
}
