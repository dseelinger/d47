# ColonisationProbe

Throwaway. Answers the journal half of `list.md` Phase 16, *Spike: what is already known about
colonisation, and by whom*. Finding:
[docs/spikes/colonisation-sources.md](../../docs/spikes/colonisation-sources.md).

| Script | Question |
|---|---|
| `scan_depot.py` | What does `ColonisationConstructionDepot` carry, is `ResourcesRequired` a snapshot or a delta, and does it fire only while docked at the site? |
| `scan_sites.py` | Can two sites be active at once, what do the other three colonisation events carry, and does the manifest ever move? |

Both read `%USERPROFILE%\Saved Games\...\Journal.*.log` and print. Neither writes anything. Run them
where the journals are — for the 912-journal corpus that is the second machine, over SSH:

```
ssh cooler 'python -' < spike/ColonisationProbe/scan_depot.py
```

Piping the script on stdin matters: the remote default shell reads piped input a line at a time, so a
pipeline split across lines is swallowed with exit code 0 and no output.
