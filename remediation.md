# Remediation 11

Reported from 2026-08-18 against v0.34.1, one item at a time. Each is checked off as it ships, and
**checked only once it has been seen to work** — a change that compiles is not a fixed item.

Remediation 10 shipped whole in [v0.34.1](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

- [x] **1. "Accept" answered the same sentence twice.** Reported verbatim: *There is no such item
  on your checklist. There is no such item on your checklist.* One outcome, said twice, which read
  out loud is indistinguishable from a stutter.

  **Two faults, one in front of the other.** Accepting joins every proposal's own sentence end to
  end, so two proposals with the same outcome say it twice — that is now collapsed, and two
  *different* outcomes are still both reported, because a Commander who accepted two things is
  owed what became of each. Counting them instead ("that happened twice") was rejected: how many
  proposals were waiting is d47's bookkeeping, and the question was what happened to the list.

  **And there should not have been two.** Asking for the same change twice recorded it twice, and
  the second copy can never do anything the first did not — accepting one applies the change and
  the other is a guaranteed no-op that still costs a sentence. A proposal identical to one already
  waiting is refused and says so. Two acts on one line are not duplicates, since proposing to
  finish something and proposing to drop it are opposite requests about the same words.
