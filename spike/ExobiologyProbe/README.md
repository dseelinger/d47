# ExobiologyProbe

Throwaway. Answers the journal half of `list.md` Phase 16, *Spike: what can be known about exobiology
before you land*. Finding: [docs/spikes/exobiology-sources.md](../../docs/spikes/exobiology-sources.md).

| Script | Question |
|---|---|
| `scan_organic.py` | Does the mass code predict what pays, what is the first-footfall multiplier, what position does `ScanOrganic` carry, and what does a scan sequence look like? |

The spansh contract in §1 of the finding was established against the live service rather than by a
script here — it is four `curl` calls and a poll, and the finding records the request shape and the
parameter oracle in full.

Run where the journals are — for the 912-journal corpus that is the second machine, over SSH:

```
ssh cooler 'python -' < spike/ExobiologyProbe/scan_organic.py
```

**One thing this probe got wrong on the first pass, kept as a comment in the script:** it read
`SellOrganicData`'s `Value` as a unit price. It is a row total for an unstated number of specimens,
which inflates whichever mass code happened to contain a bulk sale — arriving at the folklore the
probe exists to test, by accident.
