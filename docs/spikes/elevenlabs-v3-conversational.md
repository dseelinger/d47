# ElevenLabs v3 Conversational, measured against the pinned Flash 2.5

**Run 2026-09-04, from C#, against the live service on the Commander's own account.** The three
questions [#291](https://github.com/dseelinger/d47/issues/291) says the documentation does not
settle, plus the one it says is worth an ear.

Measured with `spike/ElevenLabsModelProbe`, which builds each request the way
`ElevenLabsTtsProvider.SynthesizeAsync` builds one — same URL, same `output_format=pcm_24000`, same
body shape, text through the same `SpokenNumbers.Expand` — and varies only `model_id`,
`language_code` and `voice_settings.speed`.

**In one line: `language_code` is accepted by both models on every line, the speed control is
accepted by v3 and does nothing at all, and the round trip is not 280 ms against 75 ms but
2.06 s against 0.31 s on a line d47 says every jump.**

---

## 1. The model exists, and the service's own words differ from the pricing page's

`GET /v1/models`, nine models listed, three of them relevant:

| `model_id` | What ElevenLabs says | Languages | Max characters | Speaker boost |
|---|---|---|---|---|
| `eleven_flash_v2_5` | "Our ultra low latency model in 32 languages. Ideal for conversational use cases." | 32 | 40,000 | no |
| `eleven_v3_conversational` | "The most expressive model, optimized for natural dialogue in conversational agents. … **Requires more prompt engineering than our previous models.**" | 74 | 5,000 | yes |
| `eleven_v3` | "The most expressive model. … Requires more prompt engineering than our previous models." | 74 | 5,000 | no |

Both v3 entries carry `en` in `languages`, and both advertise the same 74. The 5,000-character cap
is an eighth of Flash's and is far above anything d47 sends, so it is a fact rather than a limit.

**"Requires more prompt engineering than our previous models" is the service's own warning and it is
not on the pricing page.** It is the sentence a persona author would need to have read.

## 2. `language_code` is accepted by both, on every line

Every combination returned **200**: both models × four lines × `en`, `de` and no pin at all. There is
no repeat of Multilingual 2's outright rejection of the parameter — nothing here refuses it.

| Line | What it is for |
|---|---|
| `short` | "Contact on the scanner." — the shape of most of what d47 says |
| `system` | "Route plotted to Hyades Sector DB-X d1-112, 4 jumps, 88 tonnes of tritium aboard." — numerals and a system name, the line `SpokenNumbers` rewrites |
| `german` | "Commander Bergmann says: Achtung, Kopfgeldjager im Ring. Steht das noch, Commander?" — the exact input that disqualified Multilingual 2 |
| `tags` | "[whispers] Contact on the scanner. [sighs] It is a Federal Corvette, and it has seen us." — the reason to look at v3 at all |

**Accepted is not held, and no status code can tell the two apart.** So the probe writes 24 WAVs,
and the `de` rendition is the control that makes the listen an A/B rather than a judgement about a
single recording that already sounds English because the words are: a model honouring the pin says
an English sentence in a German accent and the pair is unmistakable, a model ignoring it hands back
the same reading twice.

> **Open, pending an ear.** `eleven_v3_conversational-short-en.wav` against
> `-short-de.wav` settles whether the parameter does anything; `-german-en.wav` settles whether the
> pin survives the sentence that broke the last model.

## 3. Speed: accepted by v3, and ignored

The finding, and the one that was not on the issue's list of risks. Each value is the median of
three syntheses of the `system` line, in seconds of returned audio:

| `voice_settings.speed` | Flash 2.5 | v3 Conversational |
|---|---|---|
| 0.5 | **400** — "expected to be greater or equal to 0.7 and less or equal to 1.2" | 8.64 s (8.48–8.72) |
| 0.6 | **400** | 8.96 s (8.64–9.28) |
| 0.7 | 9.43 s (9.15–9.89) | 8.64 s (8.16–8.64) |
| 0.8 | 8.08 s (7.94–8.31) | 8.64 s (8.48–8.88) |
| 0.9 | 7.48 s (7.11–7.57) | 8.48 s (8.40–8.72) |
| 1.0 | 6.64 s (6.22–6.69) | 8.48 s (8.08–9.36) |
| 1.1 | 6.22 s (6.22–6.55) | 8.40 s (7.92–8.56) |
| 1.2 | 5.25 s (5.15–5.25) | 8.96 s (8.56–9.04) |
| 1.3 | **400** | 8.64 s (8.32–9.12) |
| 1.5 | **400** | 8.64 s (8.40–8.80) |
| 2.0 | **400** | 8.80 s (8.00–8.80) |

**Flash moves monotonically and by a factor of 1.80 across its published range.** Every value outside
0.7–1.2 is refused with a message naming the range, which is the behaviour `SpeedFor`'s clamp is
written around and it is confirmed exactly.

**v3 accepts 0.5 and 2.0 — a four-fold span — and returns the same eight-and-a-half seconds
throughout.** The spread within one setting (8.08–9.36 s at 1.0) is wider than the spread across all
eleven settings, which is what "this number is not read" looks like. This is the same failure
Cartesia's speed control showed in
[cartesia-voices-and-speed.md](cartesia-voices-and-speed.md): accepted and ignored.

So it is not that v3 has *a different range* the rate row could narrow to, as the issue anticipated.
It has **no rate control**, and a row offered against it would be inert — a control a Commander moves
that changes nothing, with no error to explain why.

## 4. Round trip: 2.06 s against 0.31 s, not 280 ms against 75 ms

Five calls per condition, median with the full spread, on the same account and connection minutes
apart:

| Line | Characters | Flash 2.5 | v3 Conversational | Ratio |
|---|---|---|---|---|
| `short` | 23 | **165 ms** (151–193) | **600 ms** (526–910) | 3.6× |
| `system` | 81 | **313 ms** (296–350) | **2,060 ms** (1,950–2,233) | 6.6× |
| `german` | 83 | **285 ms** (264–571) | **1,724 ms** (1,602–1,762) | 6.0× |

**The published 75 ms and 280 ms are time to first byte for a caller that streams. d47 does not
stream** — `SynthesizeAsync` reads the whole body before the arbiter is handed a clip — so the delay
a Commander experiences is the whole round trip, and it grows with the length of the line rather
than sitting at a constant. The gap the issue weighed as 205 ms is, on the sentence d47 says on
every jump, **1.7 seconds**.

Two further facts from the same runs: v3 returns **more audio for the same words** (9.36 s against
6.36 s on `system`, 47% longer), and the ratio worsens with length, so a persona's longer replies
pay the most.

## 5. What v3 can do that Flash cannot — eight comparisons, open pending an ear

`--only compare` writes eight files, one per claimed difference. Each holds **v3 then Flash**, and
each read is introduced by that model saying which difference is being listened for and which model
it is — *"Whispering. This is the new V 3."* Both halves in one file rather than two, because the
question is a difference and a difference is heard in the seam.

The label is a separate request from the read. A tag at the head of a v3 generation colours
everything after it, so a label sharing the line would be part of the performance being judged.

| | Difference | Line | v3 | Flash |
|---|---|---|---|---|
| 1 | Whispering | `[whispers] Cutting the drives. There is something in the next ring…` | 6.32 s | 4.88 s |
| 2 | A weary sigh | `[sighs] That is the third interdiction this hour, Commander.` | 3.84 s | 3.20 s |
| 3 | Excitement | `[excited] Double painite hotspot, dead ahead…` | 5.20 s | 4.46 s |
| 4 | Shouting under pressure | `[shouting] Heat sink, now! Hull at 14 percent!` | 3.36 s | 3.20 s |
| 5 | Sarcasm | `[sarcastic] Beautiful landing, Commander. The pad will buff out.` | 3.44 s | 3.72 s |
| 6 | Laughter | `[laughs] The entire bounty is 812 credits.` | 4.08 s | 3.53 s |
| 7 | Urgency with no tag to tell it to | `We just lost the starboard thruster. Get us to the station.` | 3.20 s | 3.39 s |
| 8 | Emphasis on a capitalised word, no tag | `That is a Federal CORVETTE, not a Viper.` | 4.96 s | 4.13 s |

**Flash is sent the brackets unchanged rather than stripped**, so what it does with them is part of
the comparison rather than something taken on trust — a model that cannot read a tag should say the
word out loud, and the lengths do not distinguish "performed" from "spoken aloud".

The last two carry no tag at all, on purpose. A model that is expressive only when told to is a
different proposition from one that reads the sentence, and only the second changes anything for
d47's callouts, which nothing writes tags into today.

> **Open, pending an ear.** Cartesia (Phase 60) worked and did not impress, and the same test
> applies: the question is not whether the difference exists but whether it is worth 1.7 seconds.

## 6. Expressiveness — the earlier pair

`eleven_v3_conversational-tags-en.wav` against `eleven_flash_v2_5-tags-en.wav`, from section 2's
grid — the same question as section 5 on one line, kept because it is the file the language grid
already renders under all three pins.

---

## What this leaves the issue

The disqualifier the issue named — a model that infers the language per line — **did not appear as a
rejection**, and whether it appears as a *behaviour* is the listening test above.

Two things did appear that the issue's "if the spike passes, more or less" does not cover:

1. **There is no rate control on v3.** The plan's contingency was "if v3 differs, the rate row
   narrows per model". It does not differ; it is absent. A picker would have to disable the rate row
   for one of its two entries and say why.
2. **The latency figure the picker's entries would print is wrong.** "v3 Conversational — more
   expressive, ~280 ms" is a number from a streaming API description; on d47's own path the honest
   entry is nearer two seconds on a normal line.

Both are the maintainer's call, not the spike's.
