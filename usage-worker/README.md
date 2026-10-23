# The usage-count endpoint

A Cloudflare Worker in front of one D1 database, built for
[#1554](https://github.com/dseelinger/d47/issues/1554), part of
[#1523](https://github.com/dseelinger/d47/issues/1523). It receives d47's daily counts of how often
each tool was used. It is separate from the story ratings Worker in `ratings-worker/` and the
donations Worker in `worker/`, so the destination a Commander reads names only what it receives.

Nothing in the .NET build references this folder, `dotnet test` does not run it, and it ships in no
release.

## Routes

Every request carries `d47-format: 1`. Any other format is refused with 400.

| Route | Does |
|---|---|
| `POST /usage` | Body `{"day":"2026-10-22","uses":[{"tool":"plot_route","caller":"commander","count":3}]}`. Adds each count to `counts` and 1 to `reports` for the day, in one batch. Replies 204. |

There is no GET route. Any other method gets 405 and any other path 404.

Refused with nothing written:

- a missing or different `d47-format` (400);
- a `Content-Length` that is missing (411) or above 32,768 bytes (413, refused before the body is
  read);
- a body that is not JSON of exactly the shape above, or whose `uses` is empty (400);
- a `day` that is not a real `YYYY-MM-DD` date, is more than one day after today UTC, or is more
  than 30 days before it (400);
- a `tool` outside `^[a-z0-9]+(_[a-z0-9]+)*$` or longer than 64 characters (400);
- a `caller` other than `commander` or `model` (400);
- a `count` that is not an integer from 1 to 10,000 (400);
- the same tool and caller twice in one report (400);
- a report that would add a tool name once `counts` holds 1,000 distinct tool names (409).

A report sent twice is counted twice.

## Stored

| Table | Columns |
|---|---|
| `counts` | `day`, `tool`, `caller`, `uses`; primary key `(day, tool, caller)` |
| `reports` | `day` (primary key), `reports`: how many installs reported that day |

Nothing else about a request is kept: no address, no header, no time of arrival. Observability is
off.

## Provisioning, once

Figures checked 2026-10-23. Vendors move these numbers; re-verify before provisioning.

```bash
npm install -g wrangler
wrangler login
wrangler d1 create d47-usage
```

Paste the `database_id` that `create` prints into `wrangler.toml`, then:

```bash
wrangler d1 migrations apply d47-usage --remote
wrangler deploy
```

The address is `https://d47-usage.dseelinger.workers.dev`.

## Why this does not bill

| Limit | Free plan |
|---|---|
| Worker requests | 100,000 per day, then requests fail until 00:00 UTC. No overage billing on the free plan. |
| D1 rows read | 5 million per day |
| D1 rows written | 100,000 per day |
| D1 storage | 5 GB in total |

Past a free limit, D1 queries fail until the daily reset rather than charging. A report writes one
row per use plus one for the day.

## Reading the counts

Run with `wrangler d1 execute d47-usage --remote --command "<query>"`.

Uses per tool across all days:

```sql
SELECT tool, SUM(uses) AS uses FROM counts GROUP BY tool ORDER BY uses DESC;
```

The Commander and Model split per tool:

```sql
SELECT tool,
       SUM(CASE WHEN caller = 'commander' THEN uses ELSE 0 END) AS commander,
       SUM(CASE WHEN caller = 'model' THEN uses ELSE 0 END) AS model
FROM counts GROUP BY tool ORDER BY tool;
```

Uses per tool for a date range:

```sql
SELECT tool, SUM(uses) AS uses FROM counts
WHERE day BETWEEN '2026-10-01' AND '2026-10-31'
GROUP BY tool ORDER BY uses DESC;
```

Installs reporting per day:

```sql
SELECT day, reports FROM reports ORDER BY day;
```

## Tests

```bash
node --test
```

Run from this folder. Node 22 or later: the suite runs the Worker's SQL against Node's built-in
`node:sqlite` in place of D1.
