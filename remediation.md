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

- [x] **2. A hand's width of nothing between the two steppers.** Reported with a picture: the
  search box, then `‹`, then a long gap, then `›` with the count stranded to the right of both.

  **`LastChildFill` overrides the last child's own `Dock`.** The steppers were declared last, so
  one of them was the filling child and stretched across the row while its `Dock="Right"` was
  quietly ignored — the markup looked right, and every control in it carried the attribute that
  was being disregarded. The box is the last child now, and the trio is declared next, previous,
  count, because a `DockPanel` gives its *first* right-docked child the *rightmost* slot.

  The spare width goes to the search box, which is the one control here that can use it and the
  one that can give it back on a narrow pane. **Capping it was tried and does not work**: a
  stretched child with a `MaxWidth` is centred in what is left, which puts a gap on *both* sides
  of the box, and a child aligned right does not stretch at all — it collapses to its minimum at
  every window size. So the box is wide on a wide window, which is the cosmetic cost of having no
  gap at any width.

