# ColonisationProbe

Throwaway. Answers the journal half of `list.md` Phase 16, *Spike: what is already known about
colonisation, and by whom*. Finding:
[docs/spikes/colonisation-sources.md](../../docs/spikes/colonisation-sources.md).

| Script | Question |
|---|---|
| `scan_depot.py` | What does `ColonisationConstructionDepot` carry, is `ResourcesRequired` a snapshot or a delta, and does it fire only while docked at the site? |
| `scan_sites.py` | Can two sites be active at once, what do the other three colonisation events carry, and does the manifest ever move? |
| `scan_cargo.py` | Does the `Cargo` event carry a usable manifest, and can a carrier's stock be derived from `CargoTransfer`? |
| `scan_join.py` | Do the three ways Elite spells a commodity join to each other, and which source supplies the display name? |
| `scan_spansh.py` | What can the galaxy index be asked about a candidate system, which of its filters are silently dropped, and how is one system's bodies reached? |

`scan_cargo.py` and `scan_join.py` were added on 2026-08-16 for `list.md` Phase 18's tracking item,
which needs to know what the Commander *has* as well as what a site *wants* — §7 of the finding.
`scan_spansh.py` followed the same day for *Find somewhere worth colonising*, and is the odd one out
here: it asks a live service rather than the corpus, and its answers are §8.

The four journal scripts read `%USERPROFILE%\Saved Games\...\Journal.*.log` and print. None writes
anything. Run them where the journals are — for the 912-journal corpus that is the second machine,
over SSH:

```
ssh cooler 'python -' < spike/ColonisationProbe/scan_depot.py
```

Piping the script on stdin matters: the remote default shell reads piped input a line at a time, so a
pipeline split across lines is swallowed with exit code 0 and no output.

`scan_spansh.py` needs no corpus and runs anywhere with a network. It takes about two minutes,
almost all of it in one deliberate call: the request that section 6 is about is not slow, it is
unbounded, and it has to be waited out to be reported honestly.
