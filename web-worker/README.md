# The web app Worker

A Cloudflare Worker in front of one D1 database, built for
[#2374](https://github.com/dseelinger/d47/issues/2374), part of
[#1196](https://github.com/dseelinger/d47/issues/1196). It signs a Commander in with Frontier, keeps
a session, links a desktop install, stores one snapshot for each Frontier account and deletes it on
request. It is separate from the donations Worker in `worker/`, the story ratings Worker in
`ratings-worker/` and the usage-count Worker in `usage-worker/`.

Nothing in the .NET build references this folder, `dotnet test` does not run it, and it ships in no
release.

## Routes

| Route | Does |
|---|---|
| `GET /signin` | Sets a 10-minute `__Host-d47-signin` cookie holding a random `state`, a PKCE verifier and where to go after, and redirects to Frontier's `/auth` with `scope=auth`, the S256 challenge and `redirect_uri=<origin>/callback`. `?next=` is kept only when it is a valid `/link` path. |
| `GET /callback` | Checks `state` against the cookie, posts the code, the client ID and the verifier to Frontier's `/token`, reads `usr.customer_id` from `/decode` and drops Frontier's tokens and everything else it returned. Sets the session cookie and redirects to `next` or `/`. |
| `POST /signout` | Deletes the session and clears the cookie. 204. |
| `GET /link?port=<n>&challenge=<S256>` | Needs a session; without one, redirects to `/signin?next=<this link>`. Stores a single-use code that lasts two minutes and redirects to `http://127.0.0.1:<n>/linked?code=<code>`. The port is 1024 to 65535 and the challenge 43 base64url characters. |
| `POST /link/token` | Body `{"code":"…","verifier":"…"}`. Uses up the code, checks `S256(verifier)` against its challenge, and returns `{"token":"…","customerId":"…"}`. Removes the customer id's switch-off record. |
| `PUT /snapshot` | `Authorization: Bearer <upload token>`. Body: the snapshot document as JSON, plain or with `Content-Encoding: gzip`. Stores the JSON for the token's customer id, replacing the one before. 204. |
| `GET /snapshot` | Needs a session. The stored JSON, or 404 with `{"state":"off","since":"<time>"}` when uploads were turned off, `{"state":"linked"}` when the customer id has an upload token and no snapshot, and `{"state":"none"}` otherwise. `off` is reported even when another install of the same account is still linked. |
| `DELETE /snapshot` | `Authorization: Bearer <upload token>`. Deletes the snapshot and that token, and writes the switch-off record. 204. |
| `DELETE /account` | Needs a session. Deletes the snapshot, every session, every upload token, every link code and the switch-off record for the customer id. 204. |

Any other `GET` is served from `public/`, which holds one placeholder page. Any other method on a
route gets 405.

The session cookie is `__Host-d47-session`, `Secure`, `HttpOnly`, `SameSite=Lax`, holding 32 random
bytes. A session lasts 30 days from its last use: a request more than a day after the last extension
moves the expiry and sends the cookie again.

Refused with nothing written:

- a `POST`, `PUT`, `DELETE` or `GET /link` whose `Origin` is another site, `null` included (403). A
  request with no `Origin`, as the desktop app and a top-level navigation send, is allowed.
  `GET /callback` is not checked, because Frontier's redirect to it is a cross-site navigation; its
  `state` must match the `HttpOnly` sign-in cookie instead;
- a callback whose `state` does not match the cookie, or with no cookie (400); one Frontier declined
  with `error=access_denied` (403); one where the exchange or `/decode` failed or gave no numeric
  customer id (502);
- a link with a bad port or challenge (400);
- a link code that is unknown, used, older than two minutes or given with the wrong verifier (400).
  A wrong verifier uses the code up;
- a request needing a session or an upload token that has none, or an unknown or expired one (401);
- a body with no `Content-Length` (411), or one above 1,000,000 bytes for a snapshot or 512 for
  `/link/token` (413, refused before the body is read). A gzip snapshot is refused with 413 as soon
  as its JSON passes 1,000,000 bytes;
- a snapshot with any other `Content-Encoding` (415), or that is not a JSON object (400);
- a snapshot whose `frontierId` is not `F` followed by the token's customer id (403). #2371 found
  Frontier's `customer_id` is the journal's `FID` without its leading `F`.

Hashes are looked up by key and then compared in constant time, as are `state` and the PKCE
challenge.

## Stored

| Table | Columns | Removed |
|---|---|---|
| `sessions` | SHA-256 of the cookie value, customer id, expiry | On sign-out, account delete, or by the schedule once expired |
| `link_codes` | SHA-256 of the code, customer id, challenge, expiry | On first use, account delete, or by the schedule once expired |
| `upload_tokens` | SHA-256 of the token, customer id | On `DELETE /snapshot` for that token, account delete, or with its snapshot after 90 days |
| `snapshots` | customer id, the JSON, when it was stored | On `DELETE /snapshot`, account delete, or by the schedule 90 days after it was last replaced |
| `switch_offs` | customer id, when uploads were turned off | On a new upload token, account delete, or by the schedule 90 days after it was written |

Nothing else is kept: no email address, no name, no Frontier token, no address and no request log.
Session values, link codes and upload tokens are stored as hashes only. Observability is off.

The schedule is a cron trigger at 03:17 UTC each day.

## Provisioning, once

Figures checked 2026-11-13. Vendors move these numbers; re-verify before provisioning.

1. In Frontier's Developer Zone, open the **Directive 47** client's **View** page and add
   `https://d47-web.dseelinger.workers.dev/callback` under **ENDPOINTS**. #2371 signed in with no
   endpoint registered; the page says redirect URIs must be registered exactly.
2. Create the database and set the secret:

   ```bash
   npm install -g wrangler
   wrangler login
   wrangler d1 create d47-web
   ```

   Paste the `database_id` that `create` prints into `wrangler.toml`, then:

   ```bash
   wrangler d1 migrations apply d47-web --remote
   wrangler secret put FRONTIER_CLIENT_ID
   wrangler deploy
   ```

The client ID is the Worker's only secret. The exchange is PKCE with the client ID and no shared
key: #2371 found the client's shared key **Inactive**, and the PKCE exchange is the one that worked.

The address is `https://d47-web.dseelinger.workers.dev`.

## Why this does not bill

| Limit | Free plan |
|---|---|
| Worker requests | 100,000 per day across every Commander, then Error 1027 until 00:00 UTC. No overage billing on the free plan. |
| Worker CPU time | 10 ms per request and per cron run |
| Cron triggers | 5 per account; this Worker uses 1 |
| D1 rows read | 5 million per day |
| D1 rows written | 100,000 per day |
| D1 row size | 2,000,000 bytes for a row, string or BLOB |
| D1 database size | 500 MB |
| D1 storage | 5 GB in total |

Past a free limit, requests fail until the daily reset, and D1 refuses writes once the database is
full, rather than charging. The snapshot cap of 1,000,000 bytes keeps every snapshot row under D1's
2,000,000-byte row limit, which the cap relies on. At the cap, 500 MB holds 500 Commanders'
snapshots. An install uploading at most once
a minute sends about 240 requests in a four-hour session, so the request limit covers about 400 such
sessions a day.

## Tests

```bash
node --test
```

Run from this folder. Node 22 or later: the suite runs the Worker's SQL against Node's built-in
`node:sqlite` in place of D1, replaces `fetch` with a fake of Frontier's `/token` and `/decode`, and
moves the clock with `node:test`'s mock timers.
