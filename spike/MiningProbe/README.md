# MiningProbe

Throwaway. Answers what `list.md` Phase 18's *Prospector and core callouts* needed before a threshold
could be chosen honestly. Finding:
[docs/spikes/mining-callouts.md](../../docs/spikes/mining-callouts.md).

| Script | Question |
|---|---|
| `scan_prospect.py` | What does `ProspectedAsteroid` carry and how often does it arrive; does Elite's own `Content` grade track what a miner cares about; and is one percentage threshold meaningful across materials? |

Run where the journals are — for the 912-journal corpus that is the second machine, over SSH:

```
ssh cooler 'python -' < spike/MiningProbe/scan_prospect.py
```

**The one finding worth the run:** `Content: Low` and `Content: High` have the same distribution of
best-material proportion, and 45% of the richest rocks are graded Low. The grade is about
engineering-material content rather than the commodity being refined — two different questions
sharing a word — so the callout ignores it. That is not visible from the schema.
