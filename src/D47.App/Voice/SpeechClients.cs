using D47.Audio;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Seats;
using D47.Core.Storage;
using D47.Tts;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// The speech clients and voice lists: which provider speaks for each slot, what each offers, the cast
/// built over them, and the local voices' downloads.
/// </summary>
public sealed class SpeechClients : IDisposable
{
    private readonly SettingsService _settings;
    private readonly SecretStore _secrets;
    private readonly ILoggerFactory _loggers;
    private readonly ILogger<SpeechClients> _logger;
    private readonly AppPaths _paths;
    private readonly PersonaHost _personas;
    private readonly VoicePipeline _voice;
    private readonly AudioArbiter _audio;
    private readonly OwnVoice _ownVoice;
    private readonly CustomVoices _customVoices;
    private readonly Func<CrewSeatStore> _crewSeats;
    private readonly IFileSystem _files;

    internal SpeechClients(
        SettingsService settings,
        SecretStore secrets,
        ILoggerFactory loggers,
        AppPaths paths,
        IFileSystem files,
        PersonaHost personas,
        VoicePipeline voice,
        AudioArbiter audio,
        OwnVoice ownVoice,
        CustomVoices customVoices,
        Func<CrewSeatStore> crewSeats)
    {
        _files = files;
        _settings = settings;
        _secrets = secrets;
        _loggers = loggers;
        _logger = loggers.CreateLogger<SpeechClients>();
        _paths = paths;
        _personas = personas;
        _voice = voice;
        _audio = audio;
        _ownVoice = ownVoice;
        _customVoices = customVoices;
        _crewSeats = crewSeats;
        NameAccents = new NameAccents(files, paths.NameAccentsFile, _logger);

        voice.SpeakerFor = Speaker;
        voice.PinnedFor = CastClient;
        voice.CastVoiceFailed = (key, reason) => _castVoiceFailures[key] = reason;
    }

    /// <summary>A provider's voice list arrived, or the ship moved to a provider whose list is held. Raised for every provider.</summary>
    public event Action<string>? VoicesReady;

    /// <summary>The ids the voice picker offers for one slot.</summary>
    internal IReadOnlyList<string> VoiceIds(VoiceGroup group = VoiceGroup.Aboard) =>
        [.. VoicesFor(group).Voices.Select(voice => voice.Id)];

    /// <summary>How the picker labels one — "Ava — Female, en-US" rather than the raw id.</summary>
    internal string? VoiceNameFor(string id) => VoiceGroups.NameFor(VoicesFor, id);

    /// <summary>
    /// How a voice is shown to the Commander, wherever one is shown — the row, its tooltip and the
    /// picker all read this.
    /// </summary>
    internal string VoiceLabelFor(string id) => VoiceLabelFor(VoiceGroup.Aboard, id);

    /// <summary>What the provider tags one voice's gender as, or null where it says nothing (#146).</summary>
    internal string? VoiceGenderFor(VoiceGroup group, string id) =>
        VoicesFor(group).Voices
            .FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Gender;

    internal bool VoiceIsCustom(VoiceGroup group, string id) =>
        VoicesFor(group).Voices.Any(voice => voice.Custom && string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc cref="VoiceLabelFor(string)"/>
    internal string VoiceLabelFor(VoiceGroup group, string id) =>
        VoicesFor(group).LabelFor(
            id,
            TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(_settings.Current.Speech, group)));

    /// <summary>
    /// Why the voice picker has nothing in it, when it has nothing in it (Phase 19;
    /// docs/spikes/elevenlabs-voice-sources.md §3).
    /// </summary>
    internal string? WhyNoVoices(VoiceGroup group = VoiceGroup.Aboard)
    {
        var provider = TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(_settings.Current.Speech, group));

        return provider.Speaks ? VoicesOf(provider.Id).WhyEmpty(provider.Name) : null;
    }

    /// <summary>Makes the stored voices and the selected provider agree.</summary>
    private void ReconcileVoicesWithProvider()
    {
        var speech = _settings.Current.Speech;
        var selected = TtsProviderCatalog.Selected(speech.Provider).Id;

        if (speech.VoicesProvider is { } chosenFor && !string.Equals(chosenFor, selected, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "The live voices were chosen for {Previous}; filing them there and taking back {Now}'s",
                chosenFor,
                selected);
        }

        // The decision itself is a pure function of settings and lives where a test can reach it.
        _settings.Replace(SpeechCapability.ProviderKey, VoiceMemory.Reconciled);
    }

    /// <summary>Whether the selected provider has whatever credential it needs, if it needs one.</summary>
    public bool HasKeyFor(TtsProviderInfo provider) =>
        provider.KeySecretName is not { } secret || _secrets.Has(secret);

    /// <summary>Drops one voice the provider refused, everywhere it is written down.</summary>
    public void ForgetTheVoice(string voiceId)
    {
        _logger.LogInformation("{Voice} was refused by the provider; removing it", voiceId);

        _settings.Replace(
            SpeechCapability.ProviderKey,
            current => SpeechCapability.WithoutTheVoice(current, voiceId));

        Apply();
    }

    /// <summary>The folder holding the local voice's model files.</summary>
    internal string KokoroFolder() => Path.Combine(_paths.Data, "models", "kokoro");

    /// <summary>Whether the local voice is here, and what it would cost if not.</summary>
    internal string LocalVoiceState() =>
        D47.Core.Speech.KokoroAssets.IsInstalled(_files, KokoroFolder())
            ? "Installed. Nothing D47 speaks through this provider leaves this machine."
            : $"Not downloaded. About {D47.Core.Speech.KokoroAssets.TotalMegabytes:0} MB, fetched "
              + "once from huggingface.co.";

    /// <summary>Whether a download is already running, atomic because the button is a press.</summary>
    private int _fetchingVoice;

    internal string ChatterboxFolder() => Path.Combine(_paths.Data, "models", "chatterbox");

    /// <summary>The shipped reference clips, copied beside the executable.</summary>
    private static string ChatterboxVoicesFolder() => Path.Combine(AppContext.BaseDirectory, "voices", "chatterbox");

    /// <summary>Starts fetching the Chatterbox clip a voice row picked, when it is not on this PC. Never waits.</summary>
    internal void FetchPickedVoice(string key)
    {
        if (SpeechCapability.PickedVoice(key, _settings.Current) is not { } picked
            || !string.Equals(
                VoiceGroups.ProviderFor(_settings.Current.Speech, picked.Group),
                TtsProviderCatalog.ChatterboxId,
                StringComparison.OrdinalIgnoreCase)
            || ClientFor(TtsProviderCatalog.ChatterboxId) is not ChatterboxTtsProvider chatterbox)
        {
            return;
        }

        _ = Task.Run(() => chatterbox.FetchAsync(picked.Voice));
    }

    internal string ChatterboxState() =>
        D47.Core.Speech.ChatterboxAssets.IsInstalled(_files, ChatterboxFolder())
            ? "Installed. Nothing D47 speaks through this provider leaves this machine."
            : $"Not downloaded. About {D47.Core.Speech.ChatterboxAssets.TotalMegabytes:0} MB, fetched "
              + "once from huggingface.co.";

    /// <summary>The download setup offers for a voice provider, or null where it is not local or already installed.</summary>
    internal D47.App.Settings.LocalVoiceDownload? LocalVoiceToFetch(string providerId)
    {
        if (string.Equals(providerId, TtsProviderCatalog.KokoroId, StringComparison.OrdinalIgnoreCase)
            && !D47.Core.Speech.KokoroAssets.IsInstalled(_files, KokoroFolder()))
        {
            return new("Kokoro", D47.Core.Speech.KokoroAssets.TotalMegabytes, DownloadLocalVoice);
        }

        if (string.Equals(providerId, TtsProviderCatalog.ChatterboxId, StringComparison.OrdinalIgnoreCase)
            && !D47.Core.Speech.ChatterboxAssets.IsInstalled(_files, ChatterboxFolder()))
        {
            return new("Chatterbox", D47.Core.Speech.ChatterboxAssets.TotalMegabytes, DownloadChatterbox);
        }

        return null;
    }

    /// <summary>Fetches Chatterbox's model, off the UI thread, then asks it for its voices.</summary>
    internal async Task<string?> DownloadChatterbox(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fetchingVoice, 1) == 1)
        {
            return "A download is already running.";
        }

        try
        {
            using var installer = new ChatterboxInstaller(
                _files,
                ChatterboxFolder(), _loggers.CreateLogger<ChatterboxInstaller>());

            var reported = new Progress<KokoroProgress>(step => progress.Report(step.Fraction));

            var result = await Task.Run(
                () => installer.InstallAsync(reported, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("The Chatterbox download ended as {Outcome}", result.Outcome);

            if (result.Outcome is not (KokoroInstall.Installed or KokoroInstall.AlreadyPresent))
            {
                return result.Detail ?? "Chatterbox could not be downloaded.";
            }

            if (ClientFor(TtsProviderCatalog.ChatterboxId) is { } client)
            {
                await LoadVoicesAsync(client).ConfigureAwait(false);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            _logger.LogWarning(ex, "Chatterbox could not be downloaded");
            return $"Chatterbox could not be downloaded: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _fetchingVoice, 0);
        }
    }

    /// <summary>What the local voice says the moment it can say anything.</summary>
    private const string LocalVoiceProof =
        "Local voice installed. This is D47, speaking from your own machine. Nothing I say through "
        + "this provider leaves it.";

    /// <summary>Fetches the local voice, off the UI thread, saying how far it has got.</summary>
    internal async Task<string?> DownloadLocalVoice(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fetchingVoice, 1) == 1)
        {
            return "A download is already running.";
        }

        try
        {
            using var installer = new KokoroInstaller(
                _files,
                KokoroFolder(), _loggers.CreateLogger<KokoroInstaller>());

            var reported = new Progress<KokoroProgress>(step => progress.Report(step.Fraction));

            var result = await Task.Run(
                () => installer.InstallAsync(reported, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("The local voice download ended as {Outcome}", result.Outcome);

            if (result.Outcome is not (KokoroInstall.Installed or KokoroInstall.AlreadyPresent))
            {
                return result.Detail ?? "The local voice could not be downloaded.";
            }

            // The picker's list, asked for again now that there is something to list.
            await RefreshLocalVoicesAsync().ConfigureAwait(false);

            return await SpeakLocalVoiceProofAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            _logger.LogWarning(ex, "The local voice could not be downloaded");
            return $"The local voice could not be downloaded: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _fetchingVoice, 0);
        }
    }

    /// <summary>Swaps the local voice onto a different one of Kokoro's eight builds (#139).</summary>
    internal async Task<string?> SwitchLocalVoiceBuild(
        string buildId,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fetchingVoice, 1) == 1)
        {
            return "A download is already running.";
        }

        try
        {
            using var installer = new KokoroInstaller(
                _files,
                KokoroFolder(), _loggers.CreateLogger<KokoroInstaller>());

            var reported = new Progress<KokoroProgress>(step => progress.Report(step.Fraction));

            // Let go of the file before overwriting it.
            DropLocalVoiceClient();

            var result = await Task.Run(
                () => installer.SwitchAsync(buildId, reported, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "The local voice build change to {Build} ended as {Outcome}", buildId, result.Outcome);

            if (result.Outcome is not (KokoroInstall.Installed or KokoroInstall.AlreadyPresent))
            {
                return result.Detail ?? $"The {buildId} build could not be downloaded.";
            }

            await RefreshLocalVoicesAsync().ConfigureAwait(false);

            return await SpeakLocalVoiceProofAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            _logger.LogWarning(ex, "The local voice build could not be changed");
            return $"The {buildId} build could not be downloaded: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _fetchingVoice, 0);
        }
    }

    /// <summary>Closes and forgets the Kokoro client, so nothing is holding <c>model.onnx</c> open.</summary>
    private void DropLocalVoiceClient()
    {
        ITtsProvider? client;

        lock (_speechGate)
        {
            _clients.Remove(TtsProviderCatalog.KokoroId, out client);
        }

        (client as IDisposable)?.Dispose();
    }

    /// <summary>Asks the local voice what it offers, now that it has something to offer.</summary>
    private async Task RefreshLocalVoicesAsync()
    {
        if (ClientFor(TtsProviderCatalog.KokoroId) is { } client)
        {
            await LoadVoicesAsync(client).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Speaks one line in the voice that was just downloaded, through the one arbiter like everything
    /// else that makes a sound.
    /// </summary>
    private async Task<string?> SpeakLocalVoiceProofAsync(CancellationToken cancellationToken)
    {
        var shared = ClientFor(TtsProviderCatalog.KokoroId);
        var own = shared is null
            ? new KokoroTtsProvider(
                _files,
                KokoroFolder(),
                _loggers.CreateLogger<KokoroTtsProvider>(),
                _paths.PronunciationsFile)
            : null;

        try
        {
            var clip = await (shared ?? own!).SynthesizeAsync(
                LocalVoiceProof,
                new VoiceSelection(
                    SpeechCapability.ShipVoiceFor(_settings.Current, _personas.Current.Id),
                    SpeechCapability.RateFor(_settings.Current, TtsProviderCatalog.KokoroId)),
                cancellationToken).ConfigureAwait(false);

            _audio.Enqueue(new AudioRequest
            {
                Channel = AudioChannel.Speech,
                Clip = clip,
                Group = VoiceAuditions.Group,
                Caption = clip.Name,
            });

            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // The files are there and something else is wrong, which is worth saying on the row: the state
            // above it now reads "installed" and the Commander heard nothing.
            _logger.LogWarning(ex, "The local voice was downloaded but could not speak");
            return $"Downloaded, but the voice could not speak: {ex.Message}";
        }
        finally
        {
            own?.Dispose();
        }
    }

    /// <summary>One provider's client, or null for a provider that does not speak.</summary>
    private ITtsProvider? BuildSpeechClient(string providerId) => providerId switch
    {
        SpeechCapability.EdgeId =>
            new EdgeNeuralTtsProvider(_loggers.CreateLogger<EdgeNeuralTtsProvider>()),

        SpeechCapability.ElevenLabsId => new ElevenLabsTtsProvider(
            () => _secrets.TryGet(ElevenLabsTtsProvider.KeySecretName, out var key) ? key : null,
            _loggers.CreateLogger<ElevenLabsTtsProvider>(),

            // Asked per line rather than captured, the same as the key, so switching model applies to the
            // next thing said rather than to the next session (#291).
            model: () => _settings.Current.Speech.ElevenLabsModel),

        TtsProviderCatalog.OpenAiId => new OpenAiTtsProvider(
            () => _secrets.TryGet(OpenAiTtsProvider.KeySecretName, out var key) ? key : null,
            _loggers.CreateLogger<OpenAiTtsProvider>(),

            // How the core aboard should be performed, asked per sentence because a Commander switches core
            // while d47 is running (#49).
            direction: () => VoiceDirection.For(
                _settings.Current.Llm.PersonalityEnabled ? _personas.Current : null)),

        TtsProviderCatalog.CartesiaId => new CartesiaTtsProvider(
            () => _secrets.TryGet(CartesiaTtsProvider.KeySecretName, out var key) ? key : null,
            _loggers.CreateLogger<CartesiaTtsProvider>()),

        // The local voice (Phase 59).
        TtsProviderCatalog.KokoroId => new KokoroTtsProvider(
            _files,
            KokoroFolder(),
            _loggers.CreateLogger<KokoroTtsProvider>(),
            _paths.PronunciationsFile),

        TtsProviderCatalog.ChatterboxId => new ChatterboxTtsProvider(
            _files,
            ChatterboxFolder(),
            ChatterboxVoicesFolder(),
            Path.Combine(_paths.Data, "voices", "chatterbox"),
            _loggers.CreateLogger<ChatterboxTtsProvider>(),
            _ownVoice,
            _customVoices,
            _paths.PronunciationsFile),

        _ => null,
    };

    /// <summary>Records the speech models the provider lists for its key, or none where it lists none.</summary>
    private async Task ListSpeechModelsAsync(ElevenLabsTtsProvider provider)
    {
        try
        {
            ModelCatalogSource.Shared.ListSpeech(provider.Id, await provider.ListModelsAsync().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record {Provider}'s speech models", provider.Id);
        }
    }

    internal void RelistChatterboxVoices()
    {
        if (ClientFor(TtsProviderCatalog.ChatterboxId) is ChatterboxTtsProvider chatterbox)
        {
            _ = LoadVoicesAsync(chatterbox);
        }
    }

    private async Task LoadVoicesAsync(ITtsProvider provider)
    {
        try
        {
            var listed = await provider.ListVoicesAsync().ConfigureAwait(false);
            lock (_speechGate)
            {
                _voicesByProvider[provider.Id] = listed;
            }

            _logger.LogInformation(
                "{Provider}'s voice list has {Count} voices ({Listing})",
                provider.Id,
                listed.Count,
                listed.Listing);

            // The pool a re-voiced sender is drawn from, on this provider's cast.
            var cast = Casting.Of(provider.Id);
            cast.Pool = VoicePool.From(listed.Voices);

            // And which of them are a woman's, so a sender whose name reads as one is given one.
            cast.Feminine = VoicePool.Feminine(listed.Voices);

            // And which read as British, so an Empire station can be given one (#68).
            cast.British = VoicePool.British(listed.Voices);

            // And what each sounds like, so an NPC's line can be written for the voice that speaks it (#415).
            cast.Voices = listed.Voices
                .GroupBy(voice => voice.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            // A sender's name may suggest an accent and a sex; the question is queued and answered off the speech path.
            cast.ReadingOfName = name => NameAccents.Get(provider.Id, name);
            cast.NameUnknown = name => NameAccents.Enqueue(provider.Id, cast.Accents, [name]);

            // Both numbers, because one of them alone is what hid that: "1 voice available" is alarming
            // beside "473 offered" and unremarkable on its own.
            _logger.LogInformation(
                "{Count} of {Offered} voices are available for re-voiced senders, {Feminine} of them women's",
                cast.Pool.Count,
                listed.Count,
                cast.Feminine.Count);

            VoicesReady?.Invoke(provider.Id);
        }
        catch (Exception ex)
        {
            // No list is a capability being partly off, not a failure: the row still accepts a voice name
            // typed in, and speaking still works with the provider's default.
            _logger.LogWarning(ex, "Could not fetch the list of voices");
        }
    }

    /// <summary>Rebuilds the clients, the cast and the voice pipeline's wiring from the speech settings. Any thread may call it.</summary>
    public void Apply()
    {
        ReconcileVoicesWithProvider();

        SpeechWiringPlan plan;
        SpeechSettings speech;
        string aboard;
        List<ITtsProvider> released = [];

        lock (_speechGate)
        {
            speech = _settings.Current.Speech;

            // What to build, what to release, which slots moved and whose list to ask for again are decided in
            // Core, where a test can reach them; what to build and how to fetch it stay here, where the loggers
            // and the secret store are.
            plan = SpeechWiring.Plan(
                _speechWiring,
                VoiceGroups.Selected(speech),
                id => HasKeyFor(TtsProviderCatalog.Selected(id)));

            _speechWiring = plan.Next;

            // Released first, so a slot moving from ElevenLabs to Edge and another moving the other way do not
            // hold two of each at once.
            foreach (var gone in plan.Dispose)
            {
                if (_clients.Remove(gone, out var client))
                {
                    released.Add(client);
                }

                _voicesByProvider.Remove(gone);
                Casting.Forget(gone);
                ModelCatalogSource.Shared.ListSpeech(gone, []);
            }

            foreach (var wanted in plan.Build)
            {
                if (BuildSpeechClient(wanted) is { } built)
                {
                    _clients[wanted] = built;
                }
            }

            // One decorator per slot over the shared client, which is what lets the spend row answer "which slot
            // is costing money" without a second connection to the provider — the thing
            // ElevenLabsTtsProvider.MaxConcurrent's reasoning depends on (Phase 57).
            foreach (var moved in plan.Rewire)
            {
                _slots[moved] = _clients.GetValueOrDefault(VoiceGroups.ProviderFor(speech, moved)) is { } client
                    ? new MeteredTtsProvider(client, Spend, moved)
                    : null;
            }

            released.AddRange(ReleaseCastClients(all: false));

            _voice.Tts = _slots.GetValueOrDefault(VoiceGroup.Aboard);

            aboard = VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard);

            // Everyone d47 can speak as, filled in from settings.
            var carrier = VoiceGroups.ProviderFor(speech, VoiceGroup.Carrier);

            foreach (var providerId in VoiceGroups.ProvidersInUse(speech))
            {
                var cast = Casting.Of(providerId);

                // A rate is a property of the synthesiser rather than of the Commander's patience, once two of
                // them can be speaking at once: ElevenLabs *rejects* a speed outside its range rather than
                // clamping it, so a figure chosen for Edge and applied here would not be a fast carrier but a
                // silent one (Phase 57).
                cast.Rate = SpeechCapability.RateFor(_settings.Current, providerId);

                // The ship's voice belongs to the ship's provider and to nobody else's.
                cast.DefaultVoice = string.Equals(providerId, aboard, StringComparison.OrdinalIgnoreCase)
                    ? SpeechCapability.ShipVoiceFor(_settings.Current, _personas.Current.Id)
                    : null;

                // Likewise the carrier's two, which are ids issued by whoever speaks for the carrier.
                var speaksForTheCarrier = string.Equals(providerId, carrier, StringComparison.OrdinalIgnoreCase);

                cast.Assign(VoiceRole.CarrierCaptain, speaksForTheCarrier ? speech.CarrierCaptainVoice : null);
                cast.Assign(VoiceRole.TowerControl, speaksForTheCarrier ? speech.TowerVoice : null);

                // The Narrator speaks for the ship's provider, and never in the ship's voice.
                cast.Assign(
                    VoiceRole.Narrator,
                    string.Equals(providerId, aboard, StringComparison.OrdinalIgnoreCase) ? speech.NarratorVoice : null);

                // A crew seat's own voice, else its role's, both from the ship's provider.
                cast.SeatVoice = string.Equals(providerId, aboard, StringComparison.OrdinalIgnoreCase)
                    ? seatId => SeatVoiceOf(seatId, providerId, cast)
                    : null;
            }

            _voice.Voice = Casting.Of(aboard).For(VoiceRole.ShipAi);
            _voice.CuesEnabled = speech.CuesEnabled;
            _voice.BedEnabled = speech.ThinkingBedEnabled;
            _voice.GuardianColour = GuardianVoice.ColourFor(speech, _personas.Current);
            _voice.GuardianRunning = GuardianVoice.RunningColourFor(speech, _personas.Current);
        }

        // Disposed outside the lock: Kokoro's Dispose waits on the gate its model load holds.
        foreach (var client in released)
        {
            (client as IDisposable)?.Dispose();
        }

        // Fetched in the background.
        foreach (var asking in plan.RefetchVoices)
        {
            if (ClientFor(asking) is { } client)
            {
                _ = Task.Run(() => LoadVoicesAsync(client));

                if (client is ElevenLabsTtsProvider elevenLabs)
                {
                    _ = Task.Run(() => ListSpeechModelsAsync(elevenLabs));
                }
            }
        }

        // A ship moved to a provider whose list is already held gets no fetch, so its voices are checked here.
        if (plan.Rewire.Contains(VoiceGroup.Aboard)
            && !plan.RefetchVoices.Contains(aboard, StringComparer.OrdinalIgnoreCase)
            && VoicesOf(aboard).Count > 0)
        {
            _ = Task.Run(() => VoicesReady?.Invoke(aboard));
        }
    }

    /// <summary>Serialises every read and write of <c>_clients</c>, <c>_slots</c>, <c>_speechWiring</c> and <c>_voicesByProvider</c>. Held only for in-memory work.</summary>
    private readonly Lock _speechGate = new();

    /// <summary>The voice provider in use.</summary>
    private readonly Dictionary<string, ITtsProvider> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What each slot actually speaks through: a thin metering decorator over one of the shared clients
    /// above, or null for a slot on "none".
    /// </summary>
    private readonly Dictionary<VoiceGroup, ITtsProvider?> _slots = new();

    /// <summary>Which client speaks for a slot.</summary>
    public ITtsProvider? Speaker(VoiceGroup group)
    {
        lock (_speechGate)
        {
            return _slots.GetValueOrDefault(group);
        }
    }

    /// <summary>The shared client for a provider, or null.</summary>
    public ITtsProvider? ClientFor(string providerId)
    {
        lock (_speechGate)
        {
            return _clients.GetValueOrDefault(providerId);
        }
    }

    /// <summary>Local clients built for a story's cast, for a provider no slot speaks through.</summary>
    private readonly Dictionary<string, ITtsProvider> _castClients = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _castGate = new();

    /// <summary>
    /// The client a story's cast member speaks through, metered with every slot: the slots' own when one is on that
    /// provider, otherwise one built for the cast. Null for a provider id d47 does not have.
    /// </summary>
    public ITtsProvider? CastClient(string providerId)
    {
        if (ClientFor(providerId) is { } shared)
        {
            return new MeteredTtsProvider(shared, Spend);
        }

        lock (_castGate)
        {
            if (!_castClients.TryGetValue(providerId, out var built))
            {
                if (BuildSpeechClient(providerId) is not { } client)
                {
                    return null;
                }

                _castClients[providerId] = built = client;
            }

            return new MeteredTtsProvider(built, Spend);
        }
    }

    /// <summary>Why a character's chosen voice last failed and the story's own spoke instead, by StoryVoices key.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _castVoiceFailures = new(StringComparer.Ordinal);

    /// <summary>Why the chosen voice for <paramref name="key"/> last failed, or null.</summary>
    internal string? CastVoiceFailure(string key) => _castVoiceFailures.GetValueOrDefault(key);

    /// <summary>Forgets why <paramref name="key"/>'s chosen voice last failed.</summary>
    internal void ForgetCastVoiceFailure(string key) => _castVoiceFailures.TryRemove(key, out _);

    /// <summary>The voices <paramref name="providerId"/> lists, for a story character's voice picker.</summary>
    internal async Task<VoiceCatalogue> CastVoicesAsync(string providerId, CancellationToken cancellationToken)
    {
        if (VoicesOf(providerId) is { Count: > 0 } held)
        {
            return held;
        }

        return CastClient(providerId) is { } client
            ? await client.ListVoicesAsync(cancellationToken).ConfigureAwait(false)
            : VoiceCatalogue.Silent;
    }

    private string? SeatVoiceOf(string seatId, string providerId, VoiceCast cast)
    {
        var seat = _crewSeats().Ships.SelectMany(ship => ship.Seats).FirstOrDefault(known => known.Id == seatId);

        return seat is null
            ? null
            : D47.Core.Seats.SeatVoices.Resolve(
                seat,
                providerId,
                _settings.Current.Speech.SeatVoices,
                id => cast.Voices.Count == 0 || cast.Voices.ContainsKey(id));
    }

    /// <summary>Removes the cast's own clients, every one or those a slot now has a client for, and returns them to dispose. The caller holds <c>_speechGate</c>.</summary>
    private List<ITtsProvider> ReleaseCastClients(bool all)
    {
        lock (_castGate)
        {
            var released = new List<ITtsProvider>();

            foreach (var id in _castClients.Keys.Where(id => all || _clients.ContainsKey(id)).ToList())
            {
                released.Add(_castClients[id]);
                _castClients.Remove(id);
            }

            return released;
        }
    }

    /// <summary>What is on this PC for a story's cast to speak with.</summary>
    internal D47.Core.Stories.CastVoicesHere CastVoicesHere() => new(
        D47.Core.Speech.KokoroAssets.IsInstalled(_files, KokoroFolder()),
        D47.Core.Speech.ChatterboxAssets.IsInstalled(_files, ChatterboxFolder()),
        _ownVoice.Exists)
    {
        HasKey = id => TtsProviderCatalog.Selected(id).KeySecretName is not { } secret || _secrets.Names.Contains(secret),
    };

    /// <summary>
    /// Whether a line written for this slot may carry delivery direction — asked of the client that
    /// will speak it, never of the settings (#291).
    /// </summary>
    public bool DirectableIn(VoiceGroup group) => Speaker(group)?.ReadsAudioTags == true;

    /// <summary>
    /// Which provider each slot is on, and whether it had its key last time speech settings were
    /// applied.
    /// </summary>
    private SpeechWiringState _speechWiring = SpeechWiringState.Nothing;

    /// <summary>Everyone d47 can speak as (Phase 11).</summary>
    public VoiceCasting Casting { get; } = new();

    /// <summary>The accents the model judged sender names to suggest, asked off the speech path.</summary>
    public NameAccents NameAccents { get; }

    /// <summary>The cast aboard the ship.</summary>
    public VoiceCast Cast => Casting.Of(VoiceGroups.ProviderFor(_settings.Current.Speech, VoiceGroup.Aboard));

    /// <summary>What each provider in use offers, cached.</summary>
    private readonly Dictionary<string, VoiceCatalogue> _voicesByProvider = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What one provider offers, or nothing if it has not answered yet.</summary>
    public VoiceCatalogue VoicesOf(string providerId)
    {
        lock (_speechGate)
        {
            return _heldForTest.GetValueOrDefault(providerId)
                ?? _voicesByProvider.GetValueOrDefault(providerId)
                ?? VoiceCatalogue.Silent;
        }
    }

    private readonly Dictionary<string, VoiceCatalogue> _heldForTest = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Holds a voice list for a provider that no fetch or <see cref="Apply"/> replaces.</summary>
    internal void HoldVoicesForTest(string providerId, VoiceCatalogue catalogue)
    {
        lock (_speechGate)
        {
            _heldForTest[providerId] = catalogue;
        }
    }

    /// <summary>Raises <see cref="VoicesReady"/> as a list arriving does.</summary>
    internal void AnnounceVoicesForTest(string providerId) => VoicesReady?.Invoke(providerId);

    /// <summary>What one slot's provider offers.</summary>
    public VoiceCatalogue VoicesFor(VoiceGroup group) =>
        VoicesOf(VoiceGroups.ProviderFor(_settings.Current.Speech, group));

    /// <summary>The ship's own provider's list.</summary>
    /// <summary>What the voices have cost this session (Phase 19).</summary>
    public SpeechSpend Spend { get; } = new();

    /// <summary>Whether one voice in a slot's list has a free sample.</summary>
    internal bool HasPreviewFor(VoiceGroup group, string id) =>
        VoicesFor(group).Voices.Any(voice =>
            string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase) && voice.PreviewUrl is not null);

    /// <summary>
    /// Tries the stored speech key for real, against the provider's own voice list — which is the call
    /// d47 makes anyway the moment a key lands, so this proves the exact thing that has to work rather
    /// than a proxy for it.
    /// </summary>
    internal async Task<SecretCheck> VerifySpeechKeyAsync(string providerId, CancellationToken cancellationToken)
    {
        var selected = TtsProviderCatalog.Selected(providerId);

        if (selected.KeySecretName is not { } name)
        {
            return SecretCheck.Works($"{selected.Name} needs no key.");
        }

        if (!_secrets.TryGet(name, out var key))
        {
            return SecretCheck.Rejected($"No {selected.Name} key is stored.");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(AppHost.KeyCheckBudget);

        // Its own instance rather than the live one, so a refusal surfaces here as a verdict instead of being
        // swallowed by the background refresh's catch.
        ITtsProvider? provider = selected.Id switch
        {
            SpeechCapability.ElevenLabsId => new ElevenLabsTtsProvider(
                () => key,
                _loggers.CreateLogger<ElevenLabsTtsProvider>()),

            TtsProviderCatalog.OpenAiId => new OpenAiTtsProvider(
                () => key,
                _loggers.CreateLogger<OpenAiTtsProvider>()),

            TtsProviderCatalog.CartesiaId => new CartesiaTtsProvider(
                () => key,
                _loggers.CreateLogger<CartesiaTtsProvider>()),

            _ => null,
        };

        if (provider is null)
        {
            return SecretCheck.Unreachable($"D47 has no client for {selected.Name} yet.");
        }

        // A provider whose catalogue is static cannot be checked by listing it: the list is known without a
        // key, so it would answer "accepted the key" for a key that had never left this machine.
        if (selected.VoicesAreStatic)
        {
            return await ProveSpeechKeyAsync(provider, selected, budget.Token).ConfigureAwait(false);
        }

        try
        {
            var voices = await provider.ListVoicesAsync(budget.Token).ConfigureAwait(false);

            // Read from the listing rather than from the count, which is what this check was getting wrong
            // without saying so: the provider answers an empty list rather than throwing, so a rejected key
            // arrived here as "accepted the key — 0 voices" (Phase 19).
            return voices.Listing switch
            {
                VoiceListing.KeyRejected => SecretCheck.Rejected(
                    $"{selected.Name} refused the key{Reason(voices.Detail)}"),

                VoiceListing.Unreachable => SecretCheck.Unreachable(
                    $"{selected.Name} could not be reached{Reason(voices.Detail)}"),

                VoiceListing.NoKey => SecretCheck.Rejected($"No {selected.Name} key is stored."),

                _ => SecretCheck.Works($"{selected.Name} accepted the key — {voices.Count} voices."),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SecretCheck.Unreachable($"{selected.Name} did not answer within {AppHost.KeyCheckBudget.TotalSeconds:0} seconds.");
        }
        catch (TtsException ex)
        {
            // The provider's own refusal, which is the one case that means the key is wrong.
            return SecretCheck.Rejected(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The {Provider} key check could not be completed", selected.Name);
            return SecretCheck.Unreachable(ex.Message);
        }
    }

    /// <summary>Proves a key by speaking one character and throwing the audio away (Phase 58).</summary>
    private async Task<SecretCheck> ProveSpeechKeyAsync(
        ITtsProvider provider,
        TtsProviderInfo selected,
        CancellationToken cancellationToken)
    {
        try
        {
            // Any voice from the provider's own list: the check is of the key, not of a choice.
            var listed = await provider.ListVoicesAsync(cancellationToken).ConfigureAwait(false);

            _ = await provider
                .SynthesizeAsync(".", new VoiceSelection(listed.Voices.FirstOrDefault()?.Id), cancellationToken)
                .ConfigureAwait(false);

            return SecretCheck.Works($"{selected.Name} accepted the key.");
        }
        catch (OperationCanceledException)
        {
            return SecretCheck.Unreachable(
                $"{selected.Name} did not answer within {AppHost.KeyCheckBudget.TotalSeconds:0} seconds.");
        }
        catch (TtsException ex) when (ex.Fault == TtsFault.KeyRejected)
        {
            return SecretCheck.Rejected(ex.Message);
        }
        catch (Exception ex)
        {
            // Everything else is the network's problem rather than the key's, which is what the Commander
            // needs to know: there is nothing here for them to change.
            _logger.LogWarning(ex, "The {Provider} key check could not be completed", selected.Name);
            return SecretCheck.Unreachable(ex.Message);
        }
    }

    /// <summary>The service's own words where it gave any, punctuated to finish the sentence.</summary>
    private static string Reason(string? detail) =>
        detail is { Length: > 0 } said ? $" — {said}." : ".";

    public void Dispose()
    {
        List<ITtsProvider> released;

        lock (_speechGate)
        {
            released = [.. _clients.Values];
            _clients.Clear();
            _slots.Clear();
            released.AddRange(ReleaseCastClients(all: true));
        }

        foreach (var client in released)
        {
            (client as IDisposable)?.Dispose();
        }
    }
}
