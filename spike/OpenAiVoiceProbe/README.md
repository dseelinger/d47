# OpenAiVoiceProbe

Throwaway. Answers [#48](https://github.com/dseelinger/d47/issues/48) — *does OpenAI TTS drift
language on Elite system names, and is its `speed` parameter honoured?* — which blocked writing
Phase 58. Finding:
[docs/spikes/openai-tts-language-and-speed.md](../../docs/spikes/openai-tts-language-and-speed.md).

| Script | Question |
|---|---|
| `probe_speech.py` | Does a Guardian line seeded with `Shinrarta Dezhra`, `Ngalinn`, `Deciat`, `LHS 3447` and two HIP designations come back in English? Can the endpoint be told a language at all? Does `speed` move the duration? |

```
python spike/OpenAiVoiceProbe/probe_speech.py
```

**It needs a key and finds one itself.** `OPENAI_API_KEY` if set; otherwise d47's own stored
`openai.apiKey`, read from `dev-install/data/secrets.json` or the installed store, DPAPI-decrypted
in-process. **The key is never printed and never written to a file** — it goes straight into the
`Authorization` header of the calls below and nowhere else.

`--only schema|language|speed` asks one of the three questions without paying for the other two.
`--out DIR` chooses where the audio lands; it defaults to `openai-tts-spike/` under the working
directory, which is **not** in the repository and should not be.

Two things about the instrument, because they decide whether the answer means anything.

**The transcription has to be multilingual, and d47's is not.** Drift is measured by sending the
synthesised audio back through OpenAI's `whisper-1` with `response_format: verbose_json`, which
reports the language it heard. The local Whisper d47 ships is `ggml-base.en` — an English-only
model transcribes whatever it hears as English words, which is exactly the failure being looked
for, so it cannot be the judge here.

**The clips are kept because nothing here can hear an accent.** An accent on the proper nouns is a
pass and a switch of language is the failure; the transcriber answers the second and only a person
answers the first. Listen to `seeded.wav` before believing the table.

Two things it deliberately does not do. It never touches `instructions`, which is
[#49](https://github.com/dseelinger/d47/issues/49) and a different question. And it writes nothing
to the account and stores nothing — every call is a read, and the audio stays on this machine.
