# Does OpenAI's TTS drift language, and is `speed` honoured?

**Measured 2026-08-26** against the live API. Probe:
[`spike/OpenAiVoiceProbe`](../../spike/OpenAiVoiceProbe). Answers
[#48](https://github.com/dseelinger/d47/issues/48), which blocks writing **Phase 58** of
[per-role-voice-providers.md](../plans/per-role-voice-providers.md).

Model `gpt-4o-mini-tts-2025-12-15`, voice `onyx`, `response_format: wav`. Every figure below is
from two runs unless it says otherwise.

**Three answers, and one of them is the opposite of what the plan says.**

| Question | Answer |
|---|---|
| Does it drift language on Elite proper nouns? | **No.** English throughout, both runs, seeded and control. |
| Can it be told a language? | **No — but it is *ignored*, not rejected.** The plan says the opposite mechanism. |
| Is `speed` honoured? | **Yes**, across the whole documented range. |

The consequences: **OpenAI is fit for the Aboard slot and Cartesia does not displace it**; it
stays disqualified from the four comms slots, and for a *worse* reason than the plan gives; and
the speaking-rate row ships with a real range rather than being pinned shut.

---

## 1. No drift. English on every proper noun, twice

The seeded line, carrying every name the issue asks for:

> Directive forty-seven acknowledged. Course is laid to Shinrarta Dezhra by way of Ngalinn and
> Deciat. LHS 3447 remains within the tolerance you set. HIP 21991 and HIP 63835 are held in
> reserve. Your inferior systems will be optimised.

Sent back through OpenAI's **multilingual** transcription — `whisper-1`, `verbose_json`, which
reports the language it heard. That is the instrument, and it had to be a network one: d47 ships
`ggml-base.en`, and an English-only model transcribes whatever it hears as English words, which is
exactly the failure being looked for.

```
run 1  seeded  20.85s  heard as [english]
run 2  seeded  20.80s  heard as [english]
run 1  control  9.95s  heard as [english]
run 2  control 11.45s  heard as [english]
```

The control line is the same sentence shape with the proper nouns removed. Had the seeded line
drifted while the control held, the names would have been the cause. Neither drifted.

**What the transcript shows about the names is not what it looks like.** Read back:

```
… Course is laid to Shinrarta Deshra by way of Meghalan and Desyat …
```

`Dezhra` → *Deshra*, `Ngalinn` → *Meghalan* / *Mgalan*, `Deciat` → *Desyat*. Those are the
**transcriber's** spellings of an English-accented reading, not a change of language: the detected
language is `english` in every case, and the two runs disagree with each other about the spelling
while agreeing about the language. The numerals settle it — run 1 heard `HIP 2191` and
`HIP 6383-5`, run 2 heard `HIP 21991` and `HIP 63835` exactly, from audio synthesised from the
same string. That is transcription noise on both counts.

**The clips are the evidence and a person is the judge.** Nothing here can hear an *accent*, and
an accent on the proper nouns is a pass while a switch of language is the failure. The probe keeps
the WAVs for that reason.

## 2. It cannot be told a language — and the plan is wrong about why

[per-role-voice-providers.md §3.7](../plans/per-role-voice-providers.md) reads the published
OpenAPI schema and concludes:

> `CreateSpeechRequest` is `additionalProperties: false` … sending a language field would be
> **rejected**, not ignored.

**Measured, it is accepted with `200` and does nothing.**

```
language=fr                            -> HTTP 200, accepted
a field that cannot mean anything      -> HTTP 200, accepted
a valid field with an invalid value    -> HTTP 400: Invalid 'speed': decimal above maximum
                                                    value. Expected a value <= 4.0, but got 99
```

The middle row is the one that settles it. `d47_nonsense_field: "banana"` is accepted too, so the
live endpoint **does not enforce `additionalProperties: false`** — it drops unknown properties
silently. Validation is real for *declared* fields, as the `400` shows. So `language` being
accepted says nothing about `language` being supported, and the direct test says it is not:

```
no language field    10.50s  heard as [english]: Directive 47 acknowledged. The course is laid …
language=fr          10.95s  heard as [english]: Directive 47 acknowledged. The course is laid …
```

Same English input, same English output, same duration inside the run-to-run variance measured in
§3. The tag changes nothing.

**This makes the finding worse rather than better, and the wording in Phase 58 should say so.**
A rejected parameter is a contract a caller can *see*: the request fails, and d47's
`EndpointDemotions` machinery exists precisely to notice that and adapt. A parameter that is
accepted and ignored is invisible — which is the exact failure that moved the ElevenLabs pin off
Multilingual 2, arriving by a different road. Nothing d47 could send would tell it that the
language it asked for was not the language it got.

So the ruling stands and its argument changes: OpenAI is **out of the four comms slots**, not
because the schema refuses a language but because it accepts one and lies about it.

## 3. `speed` is honoured, and the row should ship with a real range

The same input at each documented rate, measured as WAV duration off the RIFF header:

| `speed` | Duration | Against 1.0 | Expected |
|---|---|---|---|
| 0.25 | 40.82s | 0.26× | 0.25× |
| 0.5 | 21.83s | 0.49× | 0.50× |
| **1.0** | **10.75s** | 1.00× | — |
| 1.5 | 7.45s | 1.44× | 1.50× |
| 2.0 | 5.45s | 1.97× | 2.00× |
| 4.0 | 3.26s | 3.30× | 4.00× |

**The brief's warning — documented but historically ignored on this model — does not hold.** The
figure moves, monotonically, across the whole `0.25`–`4.0` range. So the honest outcome is *not*
`MinimumRate = MaximumRate = 1.0`; Phase 58 declares a real range on `TtsProviderInfo`.

Two qualifications worth carrying into that phase:

- **It saturates at the top.** `4.0` buys 3.30× rather than 4×, so the fastest rate is faster than
  asked-for in name only. d47's normalised units are the Commander's dial, and a rate that
  promises more than it delivers is the kind of thing the settings row's help should not claim.
- **Synthesis is not deterministic.** The same string at `1.0` came back as 9.95s, 10.50s, 10.75s
  and 11.45s across four calls — about ±7%. Every ratio above is therefore approximate, and any
  future test asserting on duration needs that tolerance or it will flake.

## What this settles for Phase 58

1. **OpenAI is the Aboard provider it was hoped to be.** The drift the brief feared is absent, so
   the fork in §4.1 resolves toward OpenAI and **Cartesia is not needed for this phase**. It stays
   argued in the proposal as a case, not a task.
2. **§3.7's mechanism needs correcting**, and the correction strengthens the ruling rather than
   weakening it. Amended in the plan alongside this page.
3. **The rate row ships.** `MinimumRate = 0.25`, `MaximumRate = 4.0` are the measured bounds; what
   the row's help *promises* at the top end is a judgement call for whoever writes the phase.
4. **`instructions` was deliberately not measured.** That is
   [#49](https://github.com/dseelinger/d47/issues/49), and out of scope here on purpose.

## Running it again

```
python spike/OpenAiVoiceProbe/probe_speech.py
```

It reads d47's own stored `openai.apiKey` — decrypted in-process, never printed — or
`OPENAI_API_KEY` if that is set. `--only schema|language|speed` asks one question without paying
for the others. The whole run above cost a few cents.
