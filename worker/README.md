# The donation endpoint

d47's whole backend: a Cloudflare Worker in front of an R2 bucket, argued in
[#175](https://github.com/dseelinger/d47/issues/175). About a hundred lines of JavaScript, one
command to deploy, and no VM, framework or database anywhere in it.

It lives outside `src/` on purpose. Nothing in the .NET build references it, `dotnet test` does not
run it, and it ships in no release — a d47 build reaches it over HTTPS like any other destination
and would be equally happy pointed at somebody else's.

## Why this is not a bill waiting to happen

Three hard stops, and none of them charges anybody.

| Stop | What it does |
|---|---|
| Worker requests | 100,000/day on the free plan, then Error 1027 until 00:00 UTC. **There is no overage billing on that plan** — exceeding it takes an explicit upgrade, not a card |
| Payload size | Refused in `src/index.js` from `Content-Length`, before any byte is read or written |
| R2 free tier | 10 GB, 1M Class A ops/month, 10M Class B, **zero egress**, permanently — no twelve-month clock |

10 GB holds roughly 300 corpus donations at 32.5 MB, or 300 donor-years of accumulation.

**The one real billing surface is R2 itself**, which requires a payment method on file to activate
even for the free tier and bills on overage independently of the Workers plan. That is a deliberate
act on an account that had none, not a footnote — and it is why d47 ships with no endpoint address
baked in.

Workers KV was the zero-billing-surface alternative and does not survive
[#174](https://github.com/dseelinger/d47/issues/174): its 25 MiB per-value ceiling is smaller than
one corpus donation.

## Provisioning, once

Figures checked 2026-08-29. These are numbers vendors move — re-verify before provisioning.

```bash
npm install -g wrangler
wrangler login
wrangler r2 bucket create d47-donations
```

### Then the two retention rules, which are the point

A donated corpus is permanent by design; that is the whole payoff, a regression case that cannot
rot. An excerpt is evidence for one defect and should go when the issue closes. Opposite rules, so
two prefixes and two rules — and the rules live on the bucket rather than in a person's memory,
which is [#168](https://github.com/dseelinger/d47/issues/168)'s stated acceptance criterion.

```bash
wrangler r2 bucket lifecycle add d47-donations --name expire-excerpts --prefix excerpts/ --expire-days 30
```

```bash
wrangler r2 bucket lifecycle list d47-donations
```

`corpus/` gets **no rule at all**, deliberately. It is the one prefix where "forever" is the
answer, and #168 wants that written down with its reason rather than fudged into a long number.

### Check the defaults before trusting a delete

Azure's portal turns blob soft delete **on** by default, which silently makes a delete not a
delete — precisely the property GitHub was ruled out for, arriving through a default nobody chose.
R2 must be checked for the same class of thing:

```bash
wrangler r2 bucket lock list d47-donations
```

Expect no locks. A lock outlives a lifecycle rule and would turn "ask and it is deleted" into a
promise the store cannot keep — which is the promise
[#165](https://github.com/dseelinger/d47/issues/165) removed from the excerpt window for exactly
this reason.

**Then verify a delete actually deletes**, rather than assuming it from the absence of a lock:

```bash
wrangler r2 object delete d47-donations/excerpts/<token>/<object>
```

```bash
wrangler r2 object get d47-donations/excerpts/<token>/<object>
```

The second must fail. Until it has been seen to fail, d47's erasure sentence is unverified.

### Deploy

```bash
wrangler deploy
```

Take the address it prints, drop the trailing `/donate`, and paste the origin into d47's
**Privacy and egress → Where donations are sent** row. That row is
[protected](../docs/capabilities/privacy.md#donation-endpoint): the panel can set it and the
language model cannot.

## Testing it

```bash
node --test
```

Node's own test runner, from this folder. No framework, no `package.json`, no `node_modules` — a
second language in the tree is the honest cost of this endpoint and a second dependency tree on top
of it would not be. Eighteen tests, and what they mostly assert is that a malformed donation is
**refused with nothing written**.

**It is not part of `dotnet test`, and that gap is stated rather than papered over.** The three
gates that run as tests do so because they must not drift from the code; this one cannot join them
without putting Node into CI, which is a decision to make deliberately rather than a side effect of
shipping an endpoint.

## The envelope

Agreed in #175 before the consent lane and the transport lane split, and carried from day one so
both could be built at once. `D47.Core/Diagnostics/Donation/DonationEnvelope.cs` is the other half
of this table; the two are a published interface the moment a build ships.

| Header | What it is |
|---|---|
| `d47-format` | The envelope version. **Anything but `1` is refused rather than guessed at** |
| `d47-kind` | `excerpt` or `corpus`. Decides the prefix, and therefore which of two opposite retention rules applies |
| `d47-donor` | The random per-installation token ([#176](https://github.com/dseelinger/d47/issues/176)), 32 lowercase hex characters |
| `d47-sha256` | SHA-256 of the **scrubbed payload**, lowercase hex |
| `d47-build`, `d47-taken-at`, `d47-bytes` | Kept as object metadata, bounded before storing |

The body is the payload, gzipped, and is stored that way.

**The hash is over the payload and not over what arrives.** gzip output is not reproducible from
the payload — levels and implementations differ — so a hash over it would prove the transfer and
nothing a donor cares about. This one is checkable by anybody holding the object, after a gunzip,
and against the receipt d47 wrote on the donor's own machine when it sent.

## What the Worker refuses to do

- **Choose an object key from anything a client sent.** The key is derived here from a token
  already checked against `^[0-9a-f]{32}$`, a fixed prefix and a fixed suffix. A client-supplied
  object name is a stranger choosing a path inside somebody else's bucket.
- **Answer a GET with anything.** There is nothing to browse, and a friendly GET is an invitation
  to find out what else answers.
- **Say what went wrong at the store.** A write failure is between this Worker and its bucket.
- **Keep a request log.** `observability` is off in `wrangler.toml`: a log accumulating who posted
  what and when is the ambient collection `architecture.md` §1 rules out, arriving at the other end
  of the wire.

## Erasure on request — the runbook

[#167](https://github.com/dseelinger/d47/issues/167). **A runbook is exactly the thing that goes
wrong when it has never been written**: performed rarely, under time pressure, by somebody who
wants it done today. So it is written before it is needed, and it is short.

**What is deleted is the data. What is not deleted is what was decided because of it** — the
Commander's own framing and the whole of the design. A defect a donation found stays fixed, the
release that carried the fix stays released, and the changelog line naming it stays written. A
published tag never moves, and nobody is asking it to: those are the product of having read the
data once, and they are not the data.

### The donor does not need you

**The self-serve road is the one to point at first, because it is not harder than consenting
was** — which is #167's criterion, and the thing the old answer failed. In d47:

> **Privacy and egress → Your donation identifier → Forget it, and delete what was sent**

That posts the installation identifier to `/forget`, which deletes **every object under both
prefixes for that identifier** and answers with what went. d47 then forgets the identifier here and
writes an erasure receipt into `data\donations`. No thread to post in, nobody to ask, no wait.

**A refused erasure keeps the identifier on purpose.** It is the only handle anybody has on what
was sent — the store cannot find it without one and neither can the donor — so a failed press
leaves it in place, says so, and can simply be made again.

### When they ask you instead

Somebody who has lost their identifier, or reinstalled, or would simply rather ask a person.

1. **Find it.** Their receipt names the object and the identifier exactly. Without one, the only
   handle is the identifier; there is deliberately no way to search the store by anything about a
   person, because nothing about a person is in it.
2. **Delete it**, one object or the whole prefix:

   ```bash
   wrangler r2 object delete d47-donations/corpus/<token>/<object>
   ```

3. **Check the repository for anything that still links back.** A committed replay fixture is the
   case this exists to catch — see the rule below.
4. **Tell them it is done**, and say what was not deleted and why: the fix, the release, the
   changelog line.

**Within one calendar month of the request**, which is the ordinary expectation and costs nothing
to state. Most requests will never reach a person at all — the press above is immediate, and an
excerpt expires on its own after thirty days in any case.

### The rule for a committed replay fixture, decided before the first one is committed

**A committed fixture cannot be erased.** Deleting it later means rewriting the history of a public
repository, which is not a promise anybody can keep. So the rule is decided here rather than after
somebody has already committed one.

**A donated corpus is never committed.** It stays server-side and is fetched for replay runs. That
is what keeps erasure a single delete, and it keeps the repository small.

**A donated excerpt may become a committed fixture, and only under all of these:**

- **Severable by construction.** No issue number, no handle, no paperwork header, no donation
  identifier, no `Fixes #N` pointing at a donation thread. Anything that resolves back to a person
  is stripped before the commit, not after — pseudonymised is not anonymous, and a filename or a
  commit message restores the link the stand-ins were supposed to break.
- **The stand-ins survive.** They are what makes the fixture a regression case rather than a
  transcript.
- **The donor is told, before it is committed, that this one is permanent.** A fixture is not
  covered by the erasure promise, and consenting to a donation is not consenting to that.

**What survives, and why that survival is not a deletion anybody can ask for.** A severed fixture
is a sequence of game-world events with no remaining pointer to a person. It is the same class of
artefact as the fix it proves, and it is kept for the same reason: a regression case that can be
deleted is a regression case that will eventually be deleted, and the defect comes back.

**The residual is accepted rather than assumed away.** A jump sequence with timestamps is
game-world fact, but a determined reader holding a public EDSM or Inara history to match it against
is not obviously defeated by pseudonyms. **That is judged acceptable**, and it is written down
here rather than left to be discovered: the surviving artefact is small, the matching is
speculative, and the alternative — no committed regression cases at all — costs the thing the
donation was collected for. It is the same residual [#176](https://github.com/dseelinger/d47/issues/176)
accepts for an accumulating corpus, on the same reasoning.

### What is out of scope entirely

Anything on the Commander's own machine — the journal, the 14-day log, `data\backups`. Those are
theirs, they never left, and there is nothing to erase on request because nothing was received.
And anything they copied or saved and carried somewhere themselves: the windows offer a clipboard
and a file, and where those went afterwards is not reachable from here. Anything posted publicly
can be archived beyond anyone's reach, which is the whole reason a public destination was ruled out.

### Verifying it, once

The commands are under **Check the defaults before trusting a delete** above. Until a delete has
been *seen* to make an object unreadable, d47's erasure sentence is unverified — and it is a
sentence a donor consented under.
