using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Tts;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>Gives every core, the carrier captain and the tower a voice from the lists the providers return.</summary>
public sealed class VoicePairer
{
    private readonly SpeechClients _speech;
    private readonly SettingsService _settings;
    private readonly PersonaHost _personas;
    private readonly TurnLoop _turns;
    private readonly SpendTracker _spend;
    private readonly ILogger _logger;

    internal VoicePairer(
        SpeechClients speech,
        SettingsService settings,
        PersonaHost personas,
        TurnLoop turns,
        SpendTracker spend,
        ILogger logger)
    {
        _speech = speech;
        _settings = settings;
        _personas = personas;
        _turns = turns;
        _spend = spend;
        _logger = logger;

        speech.VoicesReady += provider =>
        {
            if (IsAboardOrCarrier(provider))
            {
                _ = Task.Run(() => PairVoicesAsync());
            }
        };
    }

    private TurnLoop Turns => _turns;

    private bool IsAboardOrCarrier(string provider)
    {
        var speech = _settings.Current.Speech;

        return string.Equals(provider, VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard), StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, VoiceGroups.ProviderFor(speech, VoiceGroup.Carrier), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A voice for the core aboard, when it has none.</summary>
    public async Task EnsureVoiceForCurrentPersonaAsync()
    {
        var persona = _personas.Current;

        if (_speech.VoicesFor(VoiceGroup.Aboard).Count == 0 || _settings.Current.Persona.Voices.ContainsKey(persona.Id))
        {
            return;
        }

        try
        {
            var voice = await VoicePairing.ChooseOneAsync(
                persona,
                _speech.VoicesFor(VoiceGroup.Aboard).Voices,
                _settings.Current.Persona.Voices.Values,
                Turns.Provider,
                Turns.BackgroundModel,
                _spend,
                PriceTable.Default,
                _logger).ConfigureAwait(false);

            if (voice is null)
            {
                return;
            }

            _settings.Replace("persona.voices", current => current with
            {
                Persona = current.Persona with
                {
                    Voices = new Dictionary<string, string>(current.Persona.Voices, StringComparer.Ordinal)
                    {
                        [persona.Id] = voice,
                    },
                    PairedVoices = VoicePairing.WithPairingsRecorded(
                        current.Persona.PairedVoices,
                        current.Persona.Voices,
                        new Dictionary<string, string>(StringComparer.Ordinal) { [persona.Id] = voice }),
                },
            });

            // Nothing else will notice: the pairing is not a settings row, and the core aboard has just
            // acquired the voice it is about to speak in.
            _speech.Apply();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not choose a voice for {Persona}", persona.Id);
        }
    }

    /// <summary>
    /// Drops any pairing that has Cora or Analyst Prime speaking in the wrong gender, once, and gives
    /// that core another voice in the same pass.
    /// </summary>
    private async Task RepairMiscastVoicesAsync()
    {
        if (_settings.Current.Persona.VoicesGenderChecked)
        {
            return;
        }

        var before = _settings.Current.Persona.Voices;

        var repair = await ReplaceVoicesAsync(
            before, VoicePairing.WithoutMiscastVoices(before, _speech.VoicesFor(VoiceGroup.Aboard).Voices, _logger)).ConfigureAwait(false);

        _settings.Replace("persona.voices", current => current with
        {
            Persona = current.Persona with
            {
                Voices = repair.Voices,
                VoicesGenderChecked = repair.Complete,
                PairedVoices = VoicePairing.WithPairingsRecorded(current.Persona.PairedVoices, before, repair.Voices),
            },
        });

        _speech.Apply();
    }

    private Task<VoicePairing.VoiceRepair> ReplaceVoicesAsync(
        IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) =>
        VoicePairing.WithReplacementsAsync(
            before,
            after,
            _speech.VoicesFor(VoiceGroup.Aboard).Voices,
            Turns.Provider,
            Turns.BackgroundModel,
            _spend,
            PriceTable.Default,
            _logger);

    /// <summary>Gives a new voice, once, to any core whose automatic pairing is a voice that is never cast.</summary>
    private async Task RepairNotCastVoicesAsync()
    {
        if (_settings.Current.Persona is { NotCastVoicesChecked: true, ChildVoicesChecked: true })
        {
            return;
        }

        var before = _settings.Current.Persona.Voices;

        var repair = await ReplaceVoicesAsync(
            before, VoicePairing.WithoutNotCastPairings(before, _settings.Current.Persona.PairedVoices))
            .ConfigureAwait(false);

        _settings.Replace("persona.voices", current => current with
        {
            Persona = current.Persona with
            {
                Voices = repair.Voices,
                NotCastVoicesChecked = repair.Complete,
                ChildVoicesChecked = repair.Complete,
                PairedVoices = VoicePairing.WithPairingsRecorded(current.Persona.PairedVoices, before, repair.Voices),
            },
        });

        _speech.Apply();
    }

    /// <summary>Re-casts the COVAS core once where its voice is the automatic pairing.</summary>
    private async Task RecastAutomaticCovasAsync()
    {
        if (_settings.Current.Persona.CovasRecastChecked)
        {
            return;
        }

        var before = _settings.Current.Persona.Voices;
        var covas = PersonaCatalog.Covas.Id;
        var automatic = before.TryGetValue(covas, out var voice)
            && string.Equals(_settings.Current.Persona.PairedVoices.GetValueOrDefault(covas), voice, StringComparison.OrdinalIgnoreCase);

        var repair = automatic
            ? await ReplaceVoicesAsync(before, before.Where(pair => pair.Key != covas).ToDictionary(pair => pair.Key, pair => pair.Value))
                .ConfigureAwait(false)
            : new VoicePairing.VoiceRepair(before, Complete: true);

        _settings.Replace("persona.voices", current => current with
        {
            Persona = current.Persona with
            {
                Voices = repair.Voices,
                CovasRecastChecked = repair.Complete,
                PairedVoices = VoicePairing.WithPairingsRecorded(current.Persona.PairedVoices, before, repair.Voices),
            },
        });

        _speech.Apply();
    }

    /// <summary>
    /// Removes every ship voice the aboard provider's list does not offer, so the pairing that follows
    /// gives those cores a voice from the list.
    /// </summary>
    private void ForgetVoicesNotListed()
    {
        var list = _speech.VoicesFor(VoiceGroup.Aboard);

        if (ReferenceEquals(SpeechCapability.WithoutVoicesNotIn(_settings.Current, list), _settings.Current))
        {
            return;
        }

        _logger.LogInformation(
            "Removing ship voices {Provider}'s list does not offer",
            VoiceGroups.ProviderFor(_settings.Current.Speech, VoiceGroup.Aboard));

        _settings.Replace("persona.voices", current => SpeechCapability.WithoutVoicesNotIn(current, list));
    }

    /// <summary>Serialises pairing passes, which read and write the same settings.</summary>
    private readonly SemaphoreSlim _pairing = new(1, 1);

    /// <summary>What one pairing pass gave a voice to.</summary>
    private readonly record struct PairingPass(int Cores, int CarrierRoles);

    /// <summary>
    /// A voice for every core, the carrier captain and the tower that has none, from the lists that
    /// have arrived. With <paramref name="forgetFirst"/>, every voice on every provider is forgotten,
    /// in the same settings write as the new voices, so nothing is left without a voice while the
    /// model is asked.
    /// </summary>
    private async Task<PairingPass> PairVoicesAsync(bool forgetFirst = false, CancellationToken cancellationToken = default)
    {
        await _pairing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!forgetFirst && _speech.VoicesFor(VoiceGroup.Aboard).Count > 0)
            {
                ForgetVoicesNotListed();
                await RepairMiscastVoicesAsync().ConfigureAwait(false);
                await RepairNotCastVoicesAsync().ConfigureAwait(false);
                await RecastAutomaticCovasAsync().ConfigureAwait(false);
            }

            return await PairUnvoicedAsync(forgetFirst, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Pairing is a convenience.
            _logger.LogWarning(ex, "Could not pair voices");
            return default;
        }
        finally
        {
            _pairing.Release();
        }
    }

    /// <summary>The body of <see cref="PairVoicesAsync"/>, run under its lock.</summary>
    private async Task<PairingPass> PairUnvoicedAsync(bool forgetFirst, CancellationToken cancellationToken)
    {
        D47Settings Basis(D47Settings settings) => forgetFirst ? VoiceMemory.Forgotten(settings).Settings : settings;

        var start = Basis(_settings.Current);
        var speech = start.Speech;
        var persona = start.Persona;
        var aboard = _speech.VoicesFor(VoiceGroup.Aboard);
        var carrier = _speech.VoicesFor(VoiceGroup.Carrier);
        var shared = string.Equals(
            VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard),
            VoiceGroups.ProviderFor(speech, VoiceGroup.Carrier),
            StringComparison.OrdinalIgnoreCase);

        var cores = VoicePairing.Cores.Where(slot => !persona.Voices.ContainsKey(slot.Id)).ToArray();
        var roles = VoicePairing.CarrierRoles.Where(slot => CarrierVoice(speech, slot) is null).ToArray();
        string[] carrierTaken = [.. new[] { speech.CarrierCaptainVoice, speech.TowerVoice }.OfType<string>()];

        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);

        if (aboard.Count > 0)
        {
            await PairFromAsync(
                aboard,
                shared ? [.. cores, .. roles] : cores,
                shared ? persona.Voices.Values.Concat(carrierTaken) : persona.Voices.Values,
                chosen,
                cancellationToken).ConfigureAwait(false);
        }

        if (!shared && carrier.Count > 0 && roles.Length > 0)
        {
            await PairFromAsync(carrier, roles, carrierTaken, chosen, cancellationToken).ConfigureAwait(false);
        }

        string[] pairedCores = [.. chosen.Keys.Where(id => cores.Any(slot => slot.Id == id))];

        if (!forgetFirst && chosen.Count == 0 && (aboard.Count == 0 || persona.VoicesPaired))
        {
            return default;
        }

        _settings.Replace(forgetFirst ? SpeechCapability.ResetVoicesKey : "persona.voices", settings =>
        {
            var current = Basis(settings);
            var voices = new Dictionary<string, string>(current.Persona.Voices, StringComparer.Ordinal);

            foreach (var id in pairedCores)
            {
                voices.TryAdd(id, chosen[id]);
            }

            return current with
            {
                Persona = current.Persona with
                {
                    Voices = voices,
                    VoicesPaired = current.Persona.VoicesPaired || aboard.Count > 0,
                    PairedVoices = VoicePairing.WithPairingsRecorded(
                        current.Persona.PairedVoices, current.Persona.Voices, voices),
                },
                Speech = current.Speech with
                {
                    CarrierCaptainVoice = current.Speech.CarrierCaptainVoice
                        ?? chosen.GetValueOrDefault(VoicePairing.CarrierCaptain.Id),
                    TowerVoice = current.Speech.TowerVoice ?? chosen.GetValueOrDefault(VoicePairing.Tower.Id),
                },
            };
        });

        if (chosen.Count > 0 || forgetFirst)
        {
            // Nothing else will notice: the core aboard or the carrier may have just changed voice.
            _speech.Apply();
        }

        return new PairingPass(pairedCores.Length, chosen.Count - pairedCores.Length);
    }

    /// <summary>Pairs <paramref name="slots"/> from one provider's list into <paramref name="chosen"/>.</summary>
    private async Task PairFromAsync(
        VoiceCatalogue list,
        IReadOnlyList<VoicePairing.Slot> slots,
        IEnumerable<string> taken,
        Dictionary<string, string> chosen,
        CancellationToken cancellationToken)
    {
        foreach (var (id, voice) in await VoicePairing.ChooseForAsync(
            list.Voices,
            slots,
            taken,
            Turns.Provider,
            Turns.BackgroundModel,
            _spend,
            PriceTable.Default,
            _logger,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            chosen[id] = voice;
        }
    }

    /// <summary>The voice held for one carrier role.</summary>
    private static string? CarrierVoice(SpeechSettings speech, VoicePairing.Slot role) =>
        role == VoicePairing.CarrierCaptain ? speech.CarrierCaptainVoice : speech.TowerVoice;

    /// <summary>
    /// Forgets every voice on every provider, pairs the selected provider again at once and reports
    /// what was paired.
    /// </summary>
    public async Task<string?> ResetVoicesAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var (_, providers) = VoiceMemory.Forgotten(_settings.Current);
        var pass = await PairVoicesAsync(forgetFirst: true, cancellationToken).ConfigureAwait(false);

        return VoiceMemory.ForgottenSaid(
            pass.Cores,
            pass.CarrierRoles,
            providers,
            TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(_settings.Current.Speech, VoiceGroup.Aboard)).Name,
            byModel: Turns.Provider is not null);
    }
}
