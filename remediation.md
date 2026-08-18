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

- [x] **3. A spoken yes left the card on screen.** Reported with a picture: "Yes" was heard, d47
  answered *Added "Run to the supermarket" to the custom list*, and the panel went on showing the
  proposal card, `Suggestions (1)`, and a list without the new line on it.

  **Two subscriptions, both wrong, and both invisible until the change came from somewhere else.**
  The suggestions page refreshed itself from its own Accept and Decline buttons and from nothing
  else, so it never followed a proposal answered by voice. And the checklist page subscribed in its
  constructor and unsubscribed on detach, which is not a pair — drilling into Suggestions reflows
  the tab into two panes and reparents the page, so it detached, unsubscribed, and was deaf for the
  rest of the session.

  Both listen from attach to detach now, and both catch up on the way in, since being reparented by
  a reflow means missing whatever happened in between. Each half fails on its own with the other in
  place, which is what says they were two faults rather than one.

- [x] **4. The goals band should scroll.** Reported with a picture: nine arcs, the third clipped at
  the bottom edge, and the checklist underneath gone entirely.

  **A docked child takes the height it asks for**, and nine arcs ask for all of it. The band is a
  window onto the arcs now, bounded to a share of the page, and what does not fit scrolls. Below
  the cap it takes only what it needs, so two arcs are two arcs and no scrollbar.

  **A share alone was not enough.** The row of buttons above the list costs the same fifty pixels
  on a tall window and a short one, so a purely proportional band left the list fifty-six pixels —
  a scrollbar and half a line. The list keeps a floor and the band gives, which is the right way
  round: the band is the thing the Commander opened and can close again. The gap between the two
  sits outside the scroller, or the last arc came to rest against the first checklist line and they
  read as one list.

- [x] **5. `Ships › Tulimiekka › Reaper › Cartage` — a trail through three ships at once.** A wide
  panel shows the level you are on beside the one above it, so the fleet list is still on screen
  and still pressable while a ship is open — and pressing another ship there pushed it on top of
  the first rather than in place of it.

  A crumb can now say what **kind** of level it is, and pushing one replaces the level of its own
  kind that is already open along with everything underneath it: a slot of the Tulimiekka is not a
  slot of the Reaper. Declared on the crumb by the page that pushes rather than worked out from
  key prefixes, and a crumb that names no kind nests exactly as it always did — so no other tab
  changed.

- [x] **6. A search box on a page that cannot search.** "Reaper" typed on the Ships page, the list
  unmoved. The box was drawn wherever a surface had one, whether or not the page answered it.

  A page now says whether a query would do anything to it as it is showing, and the box is drawn
  only where the answer is yes. A drill strip answers for the levels it currently shows, which is
  the case that has to be live: it changes as the Commander drills. The transcript is the exception
  with no page to ask — it highlights and steps rather than filtering, which is not a filterable
  page and is still a search.

  *The alternative was to make the fleet list filterable instead of hiding the box. Hiding it is
  what was asked for; making Ships searchable is a small change on top if it is wanted.*

- [x] **7. No way to drop a hull you do not own.** The button that plans one is on the index and
  nothing undid it, so a Python planned by mistake was on the fleet list for good.

  A planned item offers to be dropped, and asks first — it is authored work with no way back, and
  the confirmation is a chooser rather than a dialog because a popup cannot exist in the VR path.
  **An owned ship does not offer it**: it comes out of the journal and is not d47's to remove,
  which is the same rule the checklist draws between a computed line and a written one. Absent
  rather than disabled, because a control that exists to be refused teaches the wrong thing about
  what the page can do. Suits and weapons get it on the same terms, since the two services already
  had the same delete.

