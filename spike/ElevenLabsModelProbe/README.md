# ElevenLabsModelProbe

Is `eleven_v3_conversational` a model d47 could offer beside the pinned `eleven_flash_v2_5`?
[#291](https://github.com/dseelinger/d47/issues/291). Finding:
[docs/spikes/elevenlabs-v3-conversational.md](../../docs/spikes/elevenlabs-v3-conversational.md).

```
dotnet run --project spike/ElevenLabsModelProbe -- --out clips
dotnet run --project spike/ElevenLabsModelProbe -- --out clips --only speed --voice <id>
```

| Flag | Meaning |
|---|---|
| `--out <dir>` | Where the WAVs go. Default `probe-out` beside the exe |
| `--only language,speed,latency,compare,plain` | Which sections to run. Each one spends, so a re-measurement of one need not pay for three |
| `--voice <id>` | Default is the first voice the account lists that is not a ™ one |
| `--repeats <n>` | Calls per latency condition, default 5 |
| `--install <root>` | The d47 install whose `data\secrets.json` holds the key. Default is the repo's `dev-install` if it is there, otherwise the published install |

**The key is read the way d47 reads it** — `SecretStore` over DPAPI, current user — so nothing is
typed at a prompt, pasted into a file or printed. `ELEVENLABS_API_KEY` overrides it for a machine
that has no d47 install.

**It spends real characters on the Commander's account.** A full run is about 9,000 characters, so
roughly $0.45 at the $0.05 per thousand both models list at.

`compare` is the side-by-side set: eight claimed differences, one WAV each holding v3 then Flash,
every read introduced by its own model naming the difference and which model it is - *"Whispering.
This is the new V 3."* The label is a separate request from the read, because a tag at the head of a
v3 generation colours everything after it. `plain` is the same six lines with the brackets stripped
and nothing else changed - the run that describes d47 as it is, since nothing writes a tag today.

Requests are built by hand rather than through `ElevenLabsTtsProvider`, because the whole point is
to vary the model and that is a `const` today. They are otherwise what the provider sends: same URL,
same `output_format=pcm_24000`, same body shape, text through the same `SpokenNumbers.Expand`.
