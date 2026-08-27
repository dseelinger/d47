# Cartesia — the voice library, the billing unit, and a speed control that validates and does nothing

Measured 2026-08-26 with [spike/CartesiaProbe](../../spike/CartesiaProbe/README.md) against
**API version `2024-11-13`** and model **`sonic-2`**, on the Commander's own account. Everything
below is that account's answer on that day; a later API version could change any of it without
changing a URL, which is why the version is in the heading rather than in a footnote.

This unblocks **Phase 60** of [per-role-voice-providers.md](../plans/per-role-voice-providers.md),
which said its three questions had to be answered before the phase was written.

## 1. The library: 924 voices, 417 of them English

The size was unpublished, and it was the whole of the case for building the phase at all — the
Commander's objection to Cartesia being shelved was *"12 voices is crap"*.

| | Voices |
|---|---|
| **Cartesia** | **924** (417 English) |
| ElevenLabs, this account | 473 |
| OpenAI | 13 |
| Edge Neural | 322 |

Tagged with `accents`, `country`, `description`, `gender`, `language`, `tagline`, `is_pro`,
`is_public`, `mode`. By gender: **480 feminine, 443 masculine, 1 gender-neutral** — which matters
because `VoiceCast.ForSender` matches sex where a provider publishes it, and OpenAI publishes none.
By language: en 417, es 79, fr 65, hi 49, de 33, ja 30, he 29, pt 18.

**Every voice is `is_public: true` and `is_owner: false`**, and there is an `is_pro` flag, so some
of the 924 may be tier-gated on a cheaper plan. Not established here.

**Ids are opaque** — the same property ElevenLabs has and OpenAI does not, so
`VoiceIdsAreOpaque` is **true** for this provider.

**Phase 60's first gate is cleared.** The phase's own instruction was that if the count came back
smaller than ElevenLabs already offers, the phase is re-argued rather than built. It came back at
roughly twice.

## 2. The billing unit is not discoverable from the API

Four account endpoints, four 404s:

```
GET /balance              404
GET /usage                404
GET /subscriptions/current 404
GET /account              404
```

So d47 cannot read a balance or a rate the way it might have, and the unit has to come from the
published price page and be entered by hand — the same position OpenAI left Phase 58 in, arrived at
differently. Whether the row can quote a real figure depends on whether that page prices per
character (like ElevenLabs, which `SpeechSpend` counts natively) or per minute (like OpenAI, where
Phase 58 measured the conversion wrong by up to 40% *with content* and refused to quote anything).

**Unanswered, deliberately:** this spike does not read a price page. That is a fact about a web page
rather than about the account, and it should be read by a person and recorded, not scraped.

## 3. Speed: validated, and inert

**This is the finding, and the first pass got it wrong.**

`speed` belongs in `voice.__experimental_controls`, not at the top level of the request. The proof
is a refusal, which is the most useful line the whole spike produced:

```
speed = 2.0  ->  400  invalid voice controls: speed float must be between -1.0 and 1.0, got: 2.000000
```

Sent at the **top level**, `2.0` was accepted with a `200`. So the top-level field is silently
dropped, and the first single-sample run was measuring nothing at all.

### What the numbers actually support

Three runs of the same line per setting, voice-control placement, durations in seconds:

| speed | mean | spread within the setting | runs |
|---|---|---|---|
| `slowest` | 11.08 | 0.51 | 11.10, 11.33, 10.82 |
| `normal` | 11.41 | 1.11 | 10.82, 11.47, 11.94 |
| `fastest` | 10.22 | 0.84 | 10.64, 10.22, 9.80 |
| `-1.0` | 11.16 | 1.30 | 11.38, 10.40, 11.70 |
| `0.0` | 10.65 | 2.14 | 10.68, 11.70, 9.57 |
| `1.0` | 10.60 | 1.95 | 10.08, 9.89, 11.84 |

**The largest difference between settings (1.19s, `fastest` against `normal`) is smaller than the
largest spread within a single setting (2.14s, at `0.0`).** And `slowest` came out *shorter* than
`normal`. There is no effect here to measure.

### The single-sample pass said the opposite, and would have shipped a wrong phase

The first run showed `slowest` 12.91s against `fastest` 9.52s — a 26% spread, monotonic across all
five enum values, and comfortably outside the ±7% call-to-call noise the
[OpenAI spike](openai-tts-language-and-speed.md) had already measured. It read as a clear effect.

It was noise that happened to fall in order. **Repeating the discriminating cases is what turned a
plausible finding into a false one**, and the cost of not repeating would have been a slider in the
settings surface controlling nothing.

## What this means for d47

**Cartesia is the second provider that cannot be told a speaking rate**, and it fails in a stranger
way than the first. OpenAI has no language parameter at all — the field does not exist. Cartesia
*has* a speed parameter, *validates* it precisely, and then does not act on it. A control path that
still checks its input while no longer reaching the synthesiser is the likeliest explanation, and
the `__experimental_` prefix is consistent with that.

**The mechanism to handle it already exists and needs nothing built.** Phase 58 established the
shape when OpenAI turned out to be unable to hold a language: a **declared property on
`TtsProviderInfo`**, enforced in two places — the picker does not offer it, and the resolver does
not obey a hand-edited `settings.json` that names it anyway, *"because a rule living only in a
dropdown is one a text editor walks straight past"*.

So Phase 60 should declare that Cartesia takes no speaking rate, and the rate row must not be
offered for a Cartesia slot. Offering it would be **a control that appears to work and does
nothing**, which is the exact failure `change-requests.md` 43 and `docs/capabilities/listening.md`
both name.

**`change-requests.md` 43 is unaffected but its wording needs one sentence.** The ruling of
2026-08-26 — one speaking-rate row, narrowed to ElevenLabs' range as the common denominator — holds
for the three providers that have a rate. Cartesia does not join that set, so it does not tighten
the denominator. Worth noting that its float is an **offset** in `[-1.0, 1.0]` rather than a
multiplier, so even if it were honoured it would have been a third unit needing conversion, not a
narrower version of an existing one.

## What was not established

- **Whether a price page prices per character or per minute.** Read it; do not scrape it.
- **Whether `is_pro` voices are reachable on this account's tier**, and so whether the usable count
  is 924 or fewer.
- **Whether a newer model than `sonic-2` honours speed.** Only `sonic-2` was measured, and the
  inertness may be specific to it.
- **The concurrency cap.** Phase 60 records 2–3 on entry tiers from the vendor's own documentation;
  this spike made no concurrent calls and so neither confirms nor contradicts it.
