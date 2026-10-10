using D47.Core.Audio;
using D47.Core.Speech;

namespace D47.App.Voice;

/// <summary>A local voice that hands each line to Edge while its model is not installed, and takes the next line back once it is.</summary>
internal sealed class LocalVoiceWithEdgeStandIn(
    ITtsProvider local,
    ITtsProvider edge,
    Func<bool> installed,
    Func<double> edgeRate) : ITtsProvider, IDisposable
{
    /// <summary>The local provider behind <paramref name="client"/>, or the client itself where it is not wrapped.</summary>
    public static ITtsProvider? Unwrapped(ITtsProvider? client) =>
        client is LocalVoiceWithEdgeStandIn standIn ? standIn.Local : client;

    private ITtsProvider Local => local;

    public string Id => local.Id;

    public string Name => local.Name;

    private ITtsProvider Speaker => installed() ? local : edge;

    private VoiceSelection Voice(VoiceSelection voice) =>
        installed() ? voice : voice with { VoiceId = LocalVoiceStandIn.EdgeVoice, Rate = edgeRate() };

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        local.ListVoicesAsync(cancellationToken);

    public Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        installed()
            ? local.SynthesizeAsync(text, voice, cancellationToken)
            : edge.SynthesizeAsync(text, Voice(voice), cancellationToken);

    public Task<ArrivingClip> StreamAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        installed()
            ? local.StreamAsync(text, voice, cancellationToken)
            : edge.StreamAsync(text, Voice(voice), cancellationToken);

    public Task<AudioClip?> PreviewAsync(string voiceId, CancellationToken cancellationToken = default) =>
        local.PreviewAsync(voiceId, cancellationToken);

    public int GroupsSentencesUpTo => Speaker.GroupsSentencesUpTo;

    public bool ReadsAudioTags => Speaker.ReadsAudioTags;

    public bool Performs(string tag) => Speaker.Performs(tag);

    public string Billable(string text) => Speaker.Billable(text);

    public string? Phonemes(string text, VoiceSelection voice) =>
        installed() ? local.Phonemes(text, voice) : null;

    public void Dispose()
    {
        (local as IDisposable)?.Dispose();
        (edge as IDisposable)?.Dispose();
    }
}
