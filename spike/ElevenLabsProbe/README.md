# ElevenLabsProbe

Throwaway. Answers `list.md` Phase 19, *Spike: what voices does a new ElevenLabs account actually
offer*. Finding:
[docs/spikes/elevenlabs-voice-sources.md](../../docs/spikes/elevenlabs-voice-sources.md).

| Script | Question |
|---|---|
| `probe_voices.py` | What does `GET /v1/voices` return, are the premade voices in it, can a shared-library voice be spoken by id, and is adding one a call d47 could make? |

Needs no corpus and no account — most of what it reports it gets from a caller with no key at all,
which is itself the headline. Run it anywhere with a network:

```
python spike/ElevenLabsProbe/probe_voices.py
```

Set `ELEVENLABS_API_KEY` to run the authenticated half, which is the part an anonymous caller
cannot answer: what a *specific* account's list looks like, and whether a Voice Library voice can
be synthesised without being added first.

```
ELEVENLABS_API_KEY=sk_… python spike/ElevenLabsProbe/probe_voices.py
```

Two things it deliberately does not do. It never calls `POST /v1/voices/add/…` with a real key —
that writes to the Commander's account, and a probe that leaves something behind is not a probe —
so question 4 is answered from the unauthenticated refusal and the documented contract. And it
cannot create an account, which is the one thing the item asks about; the fresh-account column of
the finding is marked as documented rather than measured for that reason.
