---
title: Donation privacy notice
group: General help
nav_order: 7
---

**Read this before you donate anything.** It says who ends up holding your data, why they are
allowed to, what it is used for, how long it lasts and how to have it deleted. Nothing on this page
applies unless you deliberately send something — d47 sends nothing on its own, and there is no
setting that makes it.

This is a statement of practice, not legal advice. The GDPR is the benchmark it was written
against, because it is the strictest rule likely to apply and writing to it costs nothing here.
Nothing on this page should be read as a lawyer's opinion that it is met.

## What this is about, and what it is not

**Only data that arrives somewhere.** Almost everything d47 writes down stays in `data\` beside
`d47.exe` on your own machine: your journals, your settings, the log, the snapshots. Nobody else
has any of it, nobody needs a basis to hold it, and deleting it is deleting a file. The
[retention policy](data-retention.html) covers all of that and it needs no notice, because nothing
was received.

**A donation is the one thing that crosses.** Since
[#175](https://github.com/dseelinger/d47/issues/175) the review window can send what it is showing
you to a store this project runs. That is the moment data about *somebody else* — you — starts
being held by a party who is not you, and this page is that party saying so.

## Who holds it

Directive 47 is one person's project, built in the open. The holder is its author, the owner of the
[dseelinger/d47](https://github.com/dseelinger/d47) repository. There is no company, no processor,
no analytics vendor and nobody else with access; the store is a Cloudflare R2 bucket on that
person's account, reached through a Worker that is the only thing allowed to write to it.

**A donation is pseudonymous rather than anonymous, and the difference is worth understanding.**
The names inside an excerpt are replaced with stand-ins, and those stand-ins are deliberately *not*
stable between donations, so two of your donations cannot be joined on `CMDR ALPHA`. What ties your
donations together is a different thing: a random identifier made on your machine the first time
you send anything, derived from nothing about you, and stored in `data\donor-token.txt`. It names
an installation. Nothing anywhere maps it to a person, because nothing anywhere holds a list of
them — there is no account, no email address and no sign-up.

**What is not covered is a copy you carried somewhere yourself.** The review window can also put
the excerpt on your clipboard or save it to a file, and where it goes after that is where it went.
If you paste one into a public issue or a Discord channel, that copy is out of this project's
reach: a comment on a public repository is copied to third-party archives within the hour and
mailed whole to everybody watching, and deleting it recalls neither.

## On what basis

**Consent, given once, per donation.** There is no standing consent, no remembered choice and no
auto-send — a consent given once that uploads forever afterwards is telemetry wearing a consent
form. The window closes on the send, so the next donation is a fresh decision rather than a repeat
of this one.

**The document you read in that window is the consent record**, and it is deliberately not
described a second time here. It says, for your donation specifically, what was replaced, what was
withheld, what was dropped whole, how long the result is kept and what to do to have it back — and
it is the same rendering that becomes the payload, so what you read is what leaves rather than a
preview of it. A paraphrase on this page would be a second source of truth that could drift out of
agreement with the first. d47 keeps your copy of it in `data\donations\` when you send, with the
hash of exactly what left, so you can check that claim rather than believe it.

## For what, and nothing else

**A replay case and a diagnosis, for defects in this software.** An excerpt's journal half is
driven through the same fold the running app uses, so a fix is proven against what actually
happened; its log half is what this build did with those events. A donated journal history is test
data — nobody reads it.

That is the whole purpose, and the design is what enforces it rather than the promise being the
enforcement. There is no backend beyond an object store, nothing that indexes or searches what is
in it, no profile of any kind, and no third party with access. Purpose limitation is not a slogan
here: it is why donating a whole journal history stayed out of scope until there was a stated use
for one, and why there is no backend beyond the store that use needs.

## What is never taken

- **Another player's words.** Every in-game message is dropped rather than scrubbed — you cannot
  consent on somebody else's behalf — and your excerpt says so in every case, including the case
  where there were none.
- **Your own speech, unless you choose it.** Per donation, per incident, as a decision you make on
  the window rather than a default you have to notice.
- **Any audio at all.** Voice is biometric, and no donation has ever carried a recording. The audio
  audio recorder writes only to your own disk and its clips can never join a donation.
- **Anything ambient.** No crash reporter, no analytics, no metrics endpoint, and the donation
  endpoint keeps no request log — so there is no record anywhere of who reached it or when.

## How long it is kept

An incident excerpt lasts **30 days** and goes on its own. A donated journal history is kept
**indefinitely**, on purpose: it exists to be a regression case, and a regression case that expires
stops being one. The [retention policy](data-retention.html) states both with the thing that
enforces each, along with every other number in the product.

## How to have it deleted

One press. **Settings → Privacy → "Forget it, and delete what was sent."** d47 asks the store to
delete everything filed under your installation identifier — the deleted objects are named on the
receipt it writes to `data\donations\` — and then forgets the identifier locally. Withdrawal is no
harder than consent was: the same kind of press that sent is the press that takes back.

**If no donation address is set when you press it** — you donated once, then cleared the endpoint —
d47 can only do the local half, and the receipt says so plainly: it records the identifier it just
forgot and names the destination as nowhere, because that identifier is what a custodian needs to
find and delete what was already sent. Send it with the ask below.

**Asking a person instead.** If the machine that donated is gone, [the Discord](community.html)
reaches a person directly and does not put your identifier anywhere public — a GitHub issue is a
public archive, so if you open one, leave the object name and the identifier out of it. Your
receipt in `data\donations\` names the exact object and its hash; failing that, the identifier
from `data\donor-token.txt` is the whole of your prefix in the store. A human deletion has no
stated turnaround; the press above needs none.

**What deletion reaches, and what it does not.** The data goes — the stored objects, and nothing
else anywhere holds a copy. What does not go is what was decided because of it — the fix, the
released build, the changelog line, the test that now passes. Those are the product of having read
the data once; they are not the data, and a published release cannot be recalled in any case.
Nothing donated has been committed to this repository, a journal history never will be, and the
narrow conditions under which an incident excerpt may ever become a committed test fixture were
written down before the first one exists — in the Worker runbook (`worker/README.md`, "The rule
for a committed replay fixture"): severable by construction, and the donor told before the commit
that this one is permanent.

**Deleting `data\donor-token.txt` by hand** is the local half only: future donations then travel
under a new identifier and cannot be joined to the old ones, but what was already sent stays at
the store under the old name until a deletion reaches it. The press does both halves; the file
does one.
