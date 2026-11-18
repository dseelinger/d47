using D47.Core.Storage;

namespace D47.Core.Speech;

/// <summary>What has to be on this machine before Chatterbox Multilingual can speak a Japanese line, apart from Turbo's files.</summary>
public static class ChatterboxMultilingualAssets
{
    public const string Repository = "onnx-community/chatterbox-multilingual-ONNX";

    public const string Host = ChatterboxAssets.Host;

    /// <summary>The four graphs, each an <c>.onnx</c> file and the <c>.onnx_data</c> it names, which must sit beside it.</summary>
    public static readonly IReadOnlyList<KokoroAsset> Graphs =
    [
        new("onnx/embed_tokens.onnx", Repository, 13_286,
            "f785819ca4f6271262d5bb8971d62796c3a909e3b031982c113dbe83a4c3b854"),
        new("onnx/embed_tokens.onnx_data", Repository, 68_390_912,
            "2a15f7dd73b2ee47f6edf87740324011594b5a528ed6471ae55e327ed6cad68c"),
        new("onnx/speech_encoder.onnx", Repository, 1_184_608,
            "8f1c8a0f89b77bf9cd5dd8f2e034eb2c79dc00fe70d41196b28c257643b00ccb"),
        new("onnx/speech_encoder.onnx_data", Repository, 591_274_880,
            "92f8f290fc9720e169bc2412c507209e20b03f6564bc3243739e25c56f7dfb8f"),
        new("onnx/language_model_q4.onnx", Repository, 227_911,
            "7f8cdca83b2493536cbf3acf421199808a3d68736f55f4eabd20ef8a99da4313"),
        new("onnx/language_model_q4.onnx_data", Repository, 353_621_248,
            "e79ab8784122a501718868b9631ff46e151c552d9b24e50f25d721f375e3526c"),
        new("onnx/conditional_decoder.onnx", Repository, 6_350_448,
            "1656d0d31332bae1854839959a3139300ebb67c178651dfa3f8c5fbfa5351351"),
        new("onnx/conditional_decoder.onnx_data", Repository, 533_970_816,
            "51d58345a272747665ec9d5bb61e01835258a940e321a288582ac4c18cf01b5a"),
    ];

    /// <summary>The BPE vocabulary, its language tags and the template that wraps a line.</summary>
    public static readonly KokoroAsset Tokenizer = new(
        "tokenizer.json",
        Repository,
        71_798,
        "29d48c4a178f6af3ad5130097c34744639e9294847b38a7b912c8c68027cb819");

    /// <summary>Every file, in the order they are fetched.</summary>
    public static IReadOnlyList<KokoroAsset> All { get; } = [Tokenizer, .. Graphs];

    /// <summary>Where one graph's model file lands under <paramref name="folder"/>. Only the language model is the q4 build.</summary>
    public static string GraphPath(string folder, string graph) =>
        System.IO.Path.Combine(folder, "onnx", graph == "language_model" ? graph + "_q4.onnx" : graph + ".onnx");

    /// <summary>Where a repository path lands under <paramref name="folder"/>; the layout is the repository's own.</summary>
    public static string Destination(string folder, KokoroAsset asset) =>
        System.IO.Path.Combine(folder, asset.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>Whether every file is present at its pinned size.</summary>
    public static bool IsInstalled(IFileSystem fileSystem, string folder) =>
        All.All(asset => fileSystem.Stat(Destination(folder, asset)) is { } file
                         && file.Length == asset.Bytes);

    public static double TotalMegabytes => All.Sum(asset => asset.Bytes) / 1024.0 / 1024.0;

    /// <summary><see cref="TotalMegabytes"/> as a sentence writes it, such as "1,483".</summary>
    public static string TotalMegabytesText => TotalMegabytes.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
