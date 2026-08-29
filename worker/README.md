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

## Deleting a donation

One object, one delete — which is what keeps erasure cheap enough to be a real promise, and why
donated corpora are never committed to the repository
([#167](https://github.com/dseelinger/d47/issues/167): a committed fixture cannot be erased without
rewriting the history of a public repository).

A donor quoting their receipt names the object exactly. A donor who has lost it can name their
installation identifier, which is the whole of their prefix:

```bash
wrangler r2 object delete d47-donations/corpus/<token>/<object>
```

Deleting their `data\donor-token.txt` ends the grouping going forward. It does not reach back —
what has already been sent stays under the old token until it is deleted here.
