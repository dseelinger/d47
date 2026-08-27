# CartesiaProbe

Throwaway. Answers the three questions Phase 60 of
[per-role-voice-providers.md](../../docs/plans/per-role-voice-providers.md) says must be answered
**before** the phase is written. Finding will go in `docs/spikes/`.

| Question | Why the phase is blocked on it |
|---|---|
| How many voices, tagged how? | The library size is **unpublished**, and variety is the entire reason Phase 60 exists. If it comes back smaller than the ElevenLabs account already offers, the phase is re-argued rather than built. |
| What is the billing unit? | `SpeechSpend` counts characters. ElevenLabs bills per character, OpenAI publishes per minute, and Phase 58 had to refuse to quote any figure because the two do not convert. Which one Cartesia is decides whether its settings row can show a rate at all. |
| What is the speed range, and is it honoured? | `change-requests.md` 43 was answered on 2026-08-26 by taking **ElevenLabs' range as the common denominator** for every provider. If Cartesia's is tighter, that ruling has to be re-taken rather than inherited. |

```
python spike/CartesiaProbe/probe_voices.py
```

`--only voices|billing|speed` asks one question without paying for the others. `--out DIR` chooses
where the audio lands; it defaults to `cartesia-spike/` under the working directory, which is
**not** in the repository and should not be.

## It needs a key and finds one itself

`CARTESIA_API_KEY` if set; otherwise `cartesia.apiKey` from d47's own DPAPI store, read from
`dev-install/data/secrets.json` or the installed one, decrypted in-process. **The key is never
printed and never written to a file** — it goes into the `X-API-Key` header and nowhere else.

There is no Cartesia row in d47's settings yet, so the store route only works if the key was put
there by hand. The environment variable is the expected path for now:

```powershell
$env:CARTESIA_API_KEY = "your-key"
python spike/CartesiaProbe/probe_voices.py
```

Set that way it lives in one shell and dies with it, which is the point — it does not reach the
repository, a dotfile, or a chat transcript.

## Two things about the instrument

**The API is pinned by date, not by path.** Cartesia versions with a `Cartesia-Version` header, and
the probe sends `2024-11-13`. Whatever this measures was measured against that version, and a
finding written from it should say so — a later version could change the speed vocabulary without
changing any URL.

**The speed question is asked twice, deliberately.** The documented parameter is an enum
(`slowest`…`fastest`), and the probe also sends numbers either side of it. A provider that
**refuses** an out-of-range value is one d47 can adapt to, because a refusal is a contract
`EndpointDemotions` can see; one that **accepts and ignores** it is invisible to every caller. That
asymmetry is exactly what moved the ElevenLabs pin off Multilingual 2 and what Phase 58 measured
OpenAI for, so it is asked of every new provider rather than assumed.

The audio is kept rather than discarded so the durations can be checked by ear as well as by byte
count — and because synthesis is not deterministic, which the OpenAI spike measured at about ±7%
between identical calls. Any conclusion drawn from a single pair of durations here is a conclusion
drawn from noise.
