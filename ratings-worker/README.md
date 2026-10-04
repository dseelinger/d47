# The story ratings endpoint

A Cloudflare Worker in front of one D1 database, built for
[#882](https://github.com/dseelinger/d47/issues/882). It stores each Commander's 1 to 5 star vote
for a stock story and serves the averages. It is separate from the donations Worker in `worker/`,
which accepts only POST and holds a single R2 binding.

Nothing in the .NET build references this folder, `dotnet test` does not run it, and it ships in no
release.

## Routes

Every request carries `d47-format: 1`. Any other format is refused with 400.

| Route | Does |
|---|---|
| `GET /ratings` | `{"format":1,"stories":{"<id>":{"average":4.25,"count":12}}}` for every story with a vote. Cached for 300 seconds. |
| `PUT /ratings/<id>` | Header `d47-voter: <32 lowercase hex>`, body `{"stars":1-5}`. Inserts or replaces that voter's vote and returns the story's `{"average","count"}`. |
| `DELETE /ratings/<id>` | Header `d47-voter`. Removes that voter's vote and returns the new aggregate. A vote that does not exist is a 200. A story with no votes left returns `{"average":0,"count":0}`. |

Any other method or path gets 405 or 404.

Refusals: a story id outside `^[A-Za-z0-9._-]{1,80}$` or containing `..`, a bad voter, stars that
are not an integer from 1 to 5, a body whose `Content-Length` is above 64 bytes (refused before it
is read), and a vote for a new story once the table holds 1,000 distinct stories (409).

Stored: `story`, `voter`, `stars`, `at`. Nothing else about a request is kept, and observability is
off.

## Provisioning, once

Figures checked 2026-10-04. Vendors move these numbers; re-verify before provisioning.

```bash
npm install -g wrangler
wrangler login
wrangler d1 create d47-ratings
```

Paste the `database_id` that `create` prints into `wrangler.toml`, then:

```bash
wrangler d1 migrations apply d47-ratings --remote
wrangler deploy
```

The address is `https://d47-ratings.dseelinger.workers.dev`.

## Why this does not bill

| Limit | Free plan |
|---|---|
| Worker requests | 100,000 per day, then requests fail until 00:00 UTC. No overage billing on the free plan. |
| D1 rows read | 5 million per day |
| D1 rows written | 100,000 per day |
| D1 storage | 5 GB in total |

Past a free limit, D1 queries fail until the daily reset rather than charging. The table is capped
at 1,000 stories.

## Tests

```bash
node --test
```

Run from this folder. Node 22 or later: the suite runs the Worker's SQL against Node's built-in
`node:sqlite` in place of D1.
