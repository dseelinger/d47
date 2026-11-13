# Spike: Frontier sign-in from a Worker

#1196 settled on a hosted web app that signs the Commander in with Frontier, with the code exchange
done on the server. This page answers, from a real sign-in on 2026-11-13, whether a Cloudflare Worker
can do that and learn which Commander signed in (#2371). `capi.md` has what was known before.

## How it was run

- **The Worker.** A throwaway Worker, `d47-signin-probe`, deployed to the maintainer's Cloudflare
  account on its `workers.dev` address (`https://d47-signin-probe.<subdomain>.workers.dev`). It had
  two routes:
  - `/start` set a cookie holding a random `state` and a PKCE verifier, then redirected the browser
    to `https://auth.frontierstore.net/auth` with `audience=frontier,steam,epic`, `scope`,
    `response_type=code`, `client_id`, `state`, `redirect_uri=<Worker>/callback`, and, when PKCE
    was on, `code_challenge` and `code_challenge_method=S256`.
  - `/callback` checked `state`, posted the code to `https://auth.frontierstore.net/token`, waited
    61 seconds, called `GET /decode`, waited 61 seconds, called `GET /me`, and printed each
    status, time and response with every identifying value reduced to its type. It then discarded
    the token. It stored nothing and had logging off.
- **The client.** The client created in the Developer Zone on 2026-10-07 for `capi.md`'s probe. Its
  client ID was the Worker's only secret, typed into `wrangler secret put` by the maintainer.
- **The account.** The maintainer's Frontier account (`usr.platform: frontier`), signed in through
  the browser.
- **Pace.** No two requests to Frontier from the Worker were less than a minute apart, as `capi.md`
  advises. Four sign-ins were made between 12:25 and 12:43 UTC.

| Run | Request | What the Commander did | Result |
| --- | --- | --- | --- |
| 1 | `scope=auth`, PKCE, no shared key, `User-Agent: EDCD-Directive-0.110` | Signed in, 2FA code, no consent screen | Token, `/decode`, `/me` |
| 2 | The same | No consent screen | Token |
| 3 | The same, after deauthorising the client | Consent screen, **DENY** | `error=access_denied` |
| 4 | `scope=auth`, PKCE, no shared key, `User-Agent: d47-signin-probe/1.0` | Still signed in; consent screen, **APPROVE** | Token, `/decode`, `/me` |

## Answers

1. **Hosted redirect: yes, with nothing registered.** `/auth` accepted
   `redirect_uri=https://d47-signin-probe.<subdomain>.workers.dev/callback` and redirected to it with
   `code` and `state` in runs 1, 2 and 4. `/token` with the same `redirect_uri` answered 200 each
   time.
   - The Create Client form has no redirect field, but the client's **View** page has an
     **ENDPOINTS** section: "These are the exact URLs you will be using to return to the auth service
     (in OAuth2 known as the redirect_uri). They must be exact, and use the https:// URL scheme."
     with a **New endpoint** field and **SUBMIT**.
   - No endpoint was registered for this client, so that rule was not enforced for it. Whether it
     is enforced once a client is active, or once one endpoint is registered, was not tested.
2. **Scope: `auth` alone works.** Every run asked for `scope=auth` without `capi`. `/token` answered
   200, and `/decode` returned `"scope": "auth"`. `auth capi` was not needed and was not tried.
3. **Shared key: not tested; PKCE without it works.**
   - The client's **View** in the Developer Zone shows `Shared key` with the word **Inactive** and
     no key, so there was no key to send. The exchange with `client_id` and `client_secret` and no
     verifier could not be made.
   - With PKCE and no shared key, `POST /token` with `grant_type=authorization_code`, `client_id`,
     `code`, `redirect_uri` and `code_verifier` answered 200 from the Worker in all three runs that
     reached it.
4. **Identity.** `/token` returns `access_token`, `token_type` (`Bearer`), `expires_in` (14,399 or
   14,400) and `refresh_token`, and no `scope`.
   - `/decode` returns `iss` (`https://auth.frontierstore.net`), `iat`, `exp` (`exp` − `iat` =
     14,400 s), `scope`, `2fa`, `crossplay` and `usr`.
   - `usr` holds `email`, `customer_id` (a string), `firstname`, `lastname`, `developer`,
     `allowedDownloads`, `platform` (`frontier`), `roles` (`["ROLE_USER"]`) and `mfa`
     (`bypass_level`, `totp_enabled`, `passkey_enabled`, `email_enabled`).
   - `/me` returns the same fields as `usr`, at the top level, and none of `iss`, `iat`, `exp`,
     `scope`, `2fa` or `crossplay`.
   - `allowedDownloads` changed shape between two tokens issued 12 minutes apart: in run 1 it was an
     object keyed by number with gaps in the numbering, and in run 4 a JSON array with more entries.
     A reader has to accept both.
5. **Journal match: yes.** The Worker printed the SHA-256 of `usr.customer_id`. It equals the
   SHA-256 of the `FID` from the journals on this PC with its leading `F` removed, for one of the four
   Commanders whose journals are there, and the same hash came back from `/decode` and `/me` in runs 1
   and 4. Neither value is recorded here.
6. **Worker requests: Frontier answers them, and the `User-Agent` form is not enforced.** Every
   request the Worker made to `auth.frontierstore.net` was answered; the responses carry
   `server: cloudflare` and an `AWSALBCORS` cookie. In run 4, `/token`, `/decode` and `/me` were sent
   with `User-Agent: d47-signin-probe/1.0`, which does not match `EDCD-[A-Za-z]+-[.0-9]+`, and all
   three answered 200. Frontier still asks for that form (`capi.md`, **A client ID**), so the web
   app should send it.
7. **The client.** The client from 2026-10-07 is still listed in the Developer Zone, under "You
   can create clients here in order to use our OAuth2 service, and allow access to certain Frontier
   APIs.", as **Directive 47** with its homepage URL, the scopes **AUTH** and **CAPI**, an
   **INACTIVE** badge, and **DELETE** and **VIEW** buttons. **View** shows Name, Description, URL,
   Client ID, `Shared key` as **Inactive** with no key, Scopes, and the **ENDPOINTS** section
   (question 1). Neither page says how a client becomes active. The PKCE sign-in with its client ID
   still works. On the account's **Authorised Applications** page it
   is listed by its app name, **Directive 47**.
8. **What the Commander sees.**
   - When the account has already approved the client (runs 1 and 2): Frontier's sign-in page, the
     2FA page, then the callback. No consent screen.
   - When it has not (runs 3 and 4), a page titled **Frontier Authentication** reads:

     > Please approve account access for - they will have access to the following:
     >
     > Email, Name, Account ID, Product IDs, linked account IDs, Customer platform.

     with the buttons **APPROVE** and **DENY**. A dash stands where the app name would be; the
     screen does not name the app.
   - **Authorised Applications** (`https://user.frontierstore.net/linked`) reads "These applications
     have access information about you." over a table of **Name**, **Scope** and **Access**, with a
     **DEAUTHORISE** button per row. "Directive 47" was listed twice; the Scope column was empty for
     every application. Deauthorising one "Directive 47" row removed both, and after run 4's approval
     it was listed once.
   - After a deauthorise, the next sign-in shows the consent screen again. After a **DENY**, so does
     the one after it.
9. **Declining.** After **DENY**, Frontier redirected to the callback with `error=access_denied` and
   the original `state`, and no `code` and no `error_description`.

## Timings and errors

| Step | Time |
| --- | --- |
| Deploying the Worker | 5 s |
| `/start` to callback, sign-in and 2FA (run 1) | 170.0 s |
| `/start` to callback, no consent screen (run 2) | 41.3 s |
| `/start` to callback, consent screen and **DENY** (run 3) | 99.0 s |
| `/start` to callback, already signed in, **APPROVE** (run 4) | 4.8 s |
| `POST /token` | 138–525 ms |
| `GET /decode` | 392–405 ms |
| `GET /me` | 118–126 ms |
| Deleting the Worker | 3 s |

The `/start` to callback times include the maintainer reading and typing. Frontier returned no 4xx or
5xx and no timeout, and no response carried a rate-limit header. A test response held open for 122 s
by the Worker, streaming a byte every 12 s, reached the browser whole, so the two one-minute waits
inside a callback did not time out.

## Not tested

- The exchange with the shared key, since the key is Inactive (question 3).
- Steam and Epic accounts. The account signed in has `platform: frontier`.
- The Legacy galaxy. Sign-in does not choose a galaxy; the customer id was matched only against the
  journals on this PC.
- A refresh, and the 25-day limit on refresh tokens (`capi.md`, **OAuth and token lifetimes**).
- An account that plays more than one Commander.

## Clean-up

The Worker `d47-signin-probe` and its one secret were deleted with `wrangler delete` at 12:43 UTC.
Its address then answered 404 with Cloudflare error 1042, and `wrangler secret list` reported the
Worker not found. The shared key was never set, so there was no second secret. The client was
deauthorised before run 3 and approved again in run 4.

## Verdict

**Frontier sign-in works for a hosted Worker, with named changes.** A Worker on `workers.dev` can
send the Commander to Frontier, take the code on its own `https` callback, exchange it, and learn the
Commander's `customer_id`, which is the journal's `FID` without the `F`. The changes to #1196's plan:

- **Register the callback.** The client's **ENDPOINTS** list says redirect URIs must be registered
  exactly and use `https`. The probe worked with none registered, but the web app's Worker should
  register its callback rather than rely on that.
- **PKCE, not the shared key.** While the client's shared key is Inactive, the Worker cannot hold it.
  The exchange that works is PKCE with the client ID and no secret. The Worker keeps the verifier
  between `/start` and `/callback` itself; the probe kept it in an `HttpOnly` cookie.
- **`scope=auth`.** `capi` is not needed, and the token still carries the Commander's email address,
  real name and customer id, so the egress disclosure has to name them (`capi.md`, question 19).
- **No app name on the consent screen** while the client is in its current state. A Commander
  approving sees a dash where "Directive 47" should be. Whether activating the client fixes this is
  not known.
- **Handle `error=access_denied`** at the callback, and accept `allowedDownloads` as either an
  object or an array.
