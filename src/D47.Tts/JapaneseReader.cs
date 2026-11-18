using D47.Core.Audio;
using D47.Core.Speech;
using D47.Core.Storage;
using MeCab;

namespace D47.Tts;

/// <summary>One word of a Japanese line: its text, and its pronunciation in katakana, or null where the dictionary does not hold it.</summary>
public sealed record JapaneseWord(string Surface, string? Pronunciation);

/// <summary>Splits Japanese text into words and reads each one with MeCab and the IPAdic dictionary in <c>folder</c>.</summary>
/// <remarks>Safe to call from several threads; lines are read one at a time.</remarks>
public sealed class JapaneseReader(IFileSystem files, string folder) : IDisposable
{
    private const int PronunciationField = 8;

    private readonly Lock _gate = new();
    private MeCabTagger? _tagger;

    /// <summary>The words of <paramref name="line"/> in order. Whitespace between words is not returned.</summary>
    /// <exception cref="TtsException">The dictionary is not installed, or will not load.</exception>
    public IReadOnlyList<JapaneseWord> Read(string line)
    {
        lock (_gate)
        {
            var tagger = Tagger();
            var words = new List<JapaneseWord>();

            foreach (var node in tagger.ParseToNodes(line))
            {
                if (node.Stat is MeCabNodeStat.Bos or MeCabNodeStat.Eos)
                {
                    continue;
                }

                words.Add(new JapaneseWord(node.Surface, Pronunciation(node)));
            }

            return words;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _tagger?.Dispose();
            _tagger = null;
        }
    }

    private static string? Pronunciation(MeCabNode node)
    {
        if (node.Stat == MeCabNodeStat.Unk || node.Feature is not { } feature)
        {
            return null;
        }

        var fields = feature.Split(',');

        return fields.Length > PronunciationField && fields[PronunciationField] is { Length: > 0 } pronunciation
                                                  && pronunciation != "*"
            ? pronunciation
            : null;
    }

    private MeCabTagger Tagger()
    {
        if (_tagger is { } tagger)
        {
            return tagger;
        }

        if (!JapaneseDictionaryAssets.IsInstalled(files, folder))
        {
            throw new TtsException(
                $"The Japanese dictionary (IPAdic) is not downloaded to {folder}, so Japanese text cannot be read.");
        }

        try
        {
            return _tagger = MeCabTagger.Create(new MeCabParam(folder));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TtsException($"The Japanese dictionary (IPAdic) in {folder} would not load: {ex.Message}", ex);
        }
    }
}
