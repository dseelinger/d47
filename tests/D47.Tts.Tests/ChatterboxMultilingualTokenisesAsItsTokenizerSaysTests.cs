using Xunit;

namespace D47.Tts.Tests;

public sealed class ChatterboxMultilingualTokenisesAsItsTokenizerSaysTests
{
    /// <summary>
    /// The real tokenizer's shape in miniature: a Replace normaliser for the space, added tokens for [SPACE], [UNK], a
    /// tag and [ja], a character BPE merging "か"+"な", and the template's special tokens, which are not added tokens.
    /// </summary>
    internal const string Tokenizer = """
        {
          "added_tokens": [
            { "id": 0, "content": "[STOP]", "special": true, "normalized": false },
            { "id": 1, "content": "[UNK]", "special": true, "normalized": false },
            { "id": 2, "content": "[SPACE]", "special": true, "normalized": true },
            { "id": 611, "content": "[sigh]", "special": true, "normalized": false },
            { "id": 723, "content": "[ja]", "special": false, "normalized": false }
          ],
          "normalizer": {
            "type": "Sequence",
            "normalizers": [ { "type": "Replace", "pattern": { "String": " " }, "content": "[SPACE]" } ]
          },
          "pre_tokenizer": { "type": "Whitespace" },
          "post_processor": {
            "type": "TemplateProcessing",
            "single": [
              { "SpecialToken": { "id": "<EXAGGERATION>", "type_id": 0 } },
              { "SpecialToken": { "id": "<s>", "type_id": 0 } },
              { "Sequence": { "id": "A", "type_id": 0 } },
              { "SpecialToken": { "id": "</s>", "type_id": 0 } },
              { "SpecialToken": { "id": "<START_SPEECH>", "type_id": 0 } },
              { "SpecialToken": { "id": "<START_SPEECH>", "type_id": 0 } }
            ],
            "special_tokens": {
              "<EXAGGERATION>": { "id": "<EXAGGERATION>", "ids": [6563], "tokens": ["<EXAGGERATION>"] },
              "<s>": { "id": "<s>", "ids": [255], "tokens": ["<s>"] },
              "</s>": { "id": "</s>", "ids": [0], "tokens": ["</s>"] },
              "<START_SPEECH>": { "id": "<START_SPEECH>", "ids": [6561], "tokens": ["<START_SPEECH>"] }
            }
          },
          "model": {
            "type": "BPE",
            "unk_token": "[UNK]",
            "fuse_unk": false,
            "vocab": {
              "[STOP]": 0, "[UNK]": 1, "[SPACE]": 2, "。": 3, "3": 4,
              "か": 10, "゙": 11, "な": 12, "かな": 13, "え": 14, "き": 15,
              "[sigh]": 611, "[ja]": 723
            },
            "merges": [ "か な" ]
          }
        }
        """;

    private static readonly ChatterboxMultilingualTokeniser Tokeniser = ChatterboxMultilingualTokeniser.Parse(Tokenizer);

    private static long[] Wrapped(params long[] text) => [6563, 255, .. text, 0, 6561, 6561];

    [Fact]
    public void ALineIsWrappedInExaggerationStartEndAndTwoStartSpeechTokens() =>
        Assert.Equal(Wrapped(723, 13, 3), Tokeniser.Encode("[ja]かな。"));

    [Fact]
    public void AVoicingMarkSplitOffByNfkdIsItsOwnToken() =>
        Assert.Equal(Wrapped(723, 14, 15, 10, 11), Tokeniser.Encode("[ja]えきが".Normalize(System.Text.NormalizationForm.FormKD)));

    [Fact]
    public void AKanjiPassedThroughUnconvertedBecomesTheUnknownToken() =>
        Assert.Equal(Wrapped(723, 1, 13), Tokeniser.Encode("[ja]駅かな"));

    [Fact]
    public void ASpaceBecomesTheSpaceToken() =>
        Assert.Equal(Wrapped(723, 13, 2, 13), Tokeniser.Encode("[ja]かな かな"));

    [Fact]
    public void DigitsAndPunctuationArePiecesOfTheirOwn() =>
        Assert.Equal(Wrapped(4, 3, 13), Tokeniser.Encode("3。かな"));
}
