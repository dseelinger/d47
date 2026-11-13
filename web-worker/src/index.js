// The web app Worker (https://github.com/dseelinger/d47/issues/2374).
//
// Signs a Commander in with Frontier, keeps a session, links a desktop install with a single-use
// code, stores one snapshot per Frontier customer id and deletes it on request. Every byte of a
// request is untrusted. Frontier's tokens, and the email address and name they carry, are dropped in
// the request that reads them. Everything else under public/ is served as static files.

const FRONTIER = 'https://auth.frontierstore.net';
const USER_AGENT = 'EDCD-Directive-1.0';

const SIGNIN_COOKIE = '__Host-d47-signin';
const SESSION_COOKIE = '__Host-d47-session';

const MINUTE = 60 * 1000;
const DAY = 24 * 60 * MINUTE;
const SIGNIN_LIFETIME = 10 * MINUTE;
const SESSION_LIFETIME = 30 * DAY;
const SESSION_REFRESH_AFTER = DAY;
const LINK_CODE_LIFETIME = 2 * MINUTE;
const SNAPSHOT_LIFETIME = 90 * DAY;
const SWITCH_OFF_LIFETIME = 90 * DAY;

// Below D1's 2,000,000-byte row limit, applied to the bytes sent and to the JSON stored.
const MOST_SNAPSHOT_BYTES = 1_000_000;
const MOST_LINK_BODY_BYTES = 512;

const SECRET = /^[A-Za-z0-9_-]{43}$/;
const CUSTOMER = /^[0-9]{1,32}$/;
const PORT = /^[0-9]{1,5}$/;

const ROUTES = {
  '/signin': ['GET'],
  '/callback': ['GET'],
  '/signout': ['POST'],
  '/link': ['GET'],
  '/link/token': ['POST'],
  '/snapshot': ['GET', 'PUT', 'DELETE'],
  '/account': ['DELETE'],
};

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const methods = ROUTES[url.pathname];

    if (!methods) {
      if ((request.method === 'GET' || request.method === 'HEAD') && env.ASSETS) {
        return env.ASSETS.fetch(request);
      }
      return said(404, 'Not found.');
    }

    if (!methods.includes(request.method)) {
      return said(405, `This route takes ${methods.join(' or ')}.`);
    }

    // /callback is reached by Frontier's cross-site redirect; its state check stands in for this one.
    if ((request.method !== 'GET' || url.pathname === '/link') && !sameSite(request, url)) {
      return said(403, 'This route does not take requests from another site.');
    }

    const now = Date.now();
    switch (`${request.method} ${url.pathname}`) {
      case 'GET /signin': return signIn(request, url, env);
      case 'GET /callback': return callback(request, url, env, now);
      case 'POST /signout': return signOut(request, env);
      case 'GET /link': return link(request, url, env, now);
      case 'POST /link/token': return linkToken(request, env, now);
      case 'GET /snapshot': return readSnapshot(request, env, now);
      case 'PUT /snapshot': return writeSnapshot(request, env, now);
      case 'DELETE /snapshot': return deleteSnapshot(request, env, now);
      default: return deleteAccount(request, env, now);
    }
  },

  async scheduled(controller, env) {
    await expire(env, Date.now());
  },
};

async function expire(env, now) {
  const snapshotsBefore = iso(now - SNAPSHOT_LIFETIME);
  await env.DB.batch([
    env.DB
      .prepare('DELETE FROM upload_tokens WHERE customer IN (SELECT customer FROM snapshots WHERE at < ?)')
      .bind(snapshotsBefore),
    env.DB.prepare('DELETE FROM snapshots WHERE at < ?').bind(snapshotsBefore),
    env.DB.prepare('DELETE FROM switch_offs WHERE at < ?').bind(iso(now - SWITCH_OFF_LIFETIME)),
    env.DB.prepare('DELETE FROM sessions WHERE expires <= ?').bind(iso(now)),
    env.DB.prepare('DELETE FROM link_codes WHERE expires <= ?').bind(iso(now)),
  ]);
}

// Sign-in

async function signIn(request, url, env) {
  const state = randomSecret();
  const verifier = randomSecret();
  const next = linkPath(url.searchParams.get('next')) ?? '/';

  const auth = new URL(`${FRONTIER}/auth`);
  auth.search = new URLSearchParams({
    audience: 'frontier,steam,epic',
    scope: 'auth',
    response_type: 'code',
    client_id: env.FRONTIER_CLIENT_ID,
    code_challenge: await s256(verifier),
    code_challenge_method: 'S256',
    state,
    redirect_uri: `${url.origin}/callback`,
  }).toString();

  return redirect(auth.toString(), [
    cookie(SIGNIN_COOKIE, `${state}.${verifier}.${base64url(new TextEncoder().encode(next))}`, SIGNIN_LIFETIME),
  ]);
}

async function callback(request, url, env, now) {
  const [state, verifier, packedNext] = (readCookie(request, SIGNIN_COOKIE) ?? '').split('.');
  const clear = cookie(SIGNIN_COOKIE, '', 0);

  if (!SECRET.test(state ?? '') || !SECRET.test(verifier ?? '') || !same(state, url.searchParams.get('state') ?? '')) {
    return said(400, 'This sign-in was not started here, or took too long. Sign in again.', [clear]);
  }

  if (url.searchParams.get('error') === 'access_denied') {
    return said(403, 'Sign-in was declined at Frontier.', [clear]);
  }

  const code = url.searchParams.get('code');
  if (!code) {
    return said(400, 'Frontier sent no sign-in code.', [clear]);
  }

  const customer = await customerFromFrontier(env, code, verifier, `${url.origin}/callback`);
  if (!customer) {
    return said(502, 'Frontier did not complete the sign-in. Try again in a minute.', [clear]);
  }

  const session = randomSecret();
  await env.DB
    .prepare('INSERT INTO sessions (hash, customer, expires) VALUES (?, ?, ?)')
    .bind(await sha256(session), customer, iso(now + SESSION_LIFETIME))
    .run();

  const next = linkPath(decodeNext(packedNext)) ?? '/';
  return redirect(next, [clear, cookie(SESSION_COOKIE, session, SESSION_LIFETIME)]);
}

/** Exchanges the code with PKCE and returns the customer id, or null. Frontier's tokens go out of scope here. */
async function customerFromFrontier(env, code, verifier, redirectUri) {
  try {
    const exchange = await fetch(`${FRONTIER}/token`, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded', 'user-agent': USER_AGENT },
      body: new URLSearchParams({
        grant_type: 'authorization_code',
        client_id: env.FRONTIER_CLIENT_ID,
        code,
        redirect_uri: redirectUri,
        code_verifier: verifier,
      }).toString(),
    });
    if (!exchange.ok) return null;

    const accessToken = (await exchange.json())?.access_token;
    if (typeof accessToken !== 'string' || accessToken === '') return null;

    const decode = await fetch(`${FRONTIER}/decode`, {
      headers: { authorization: `Bearer ${accessToken}`, 'user-agent': USER_AGENT },
    });
    if (!decode.ok) return null;

    const id = (await decode.json())?.usr?.customer_id;
    const customer = typeof id === 'number' ? String(id) : id;
    return typeof customer === 'string' && CUSTOMER.test(customer) ? customer : null;
  } catch {
    return null;
  }
}

async function signOut(request, env) {
  const value = readCookie(request, SESSION_COOKIE);
  if (value) {
    await env.DB.prepare('DELETE FROM sessions WHERE hash = ?').bind(await sha256(value)).run();
  }
  return empty(204, [cookie(SESSION_COOKIE, '', 0)]);
}

// Desktop link

async function link(request, url, env, now) {
  const port = url.searchParams.get('port') ?? '';
  const challenge = url.searchParams.get('challenge') ?? '';
  const path = linkPath(`/link?port=${encodeURIComponent(port)}&challenge=${encodeURIComponent(challenge)}`);
  if (!path) {
    return said(400, 'A link needs a port from 1024 to 65535 and an S256 challenge.');
  }

  const session = await sessionOf(request, env, now);
  if (!session) {
    return redirect(`/signin?next=${encodeURIComponent(path)}`);
  }

  const code = randomSecret();
  await env.DB
    .prepare('INSERT INTO link_codes (hash, customer, challenge, expires) VALUES (?, ?, ?, ?)')
    .bind(await sha256(code), session.customer, challenge, iso(now + LINK_CODE_LIFETIME))
    .run();

  return redirect(`http://127.0.0.1:${Number(port)}/linked?code=${code}`, session.cookies);
}

async function linkToken(request, env, now) {
  const refused = lengthRefusal(request, MOST_LINK_BODY_BYTES);
  if (refused) return refused;

  let code;
  let verifier;
  try {
    ({ code, verifier } = JSON.parse(await request.text()) ?? {});
  } catch {
    return said(400, 'The body must be JSON.');
  }

  if (typeof code !== 'string' || !SECRET.test(code) || typeof verifier !== 'string' || verifier.length < 43 || verifier.length > 128) {
    return said(400, 'A link token needs a code and a verifier.');
  }

  const hash = await sha256(code);
  const row = await env.DB.prepare('SELECT hash, customer, challenge, expires FROM link_codes WHERE hash = ?').bind(hash).first();
  if (row) {
    await env.DB.prepare('DELETE FROM link_codes WHERE hash = ?').bind(hash).run();
  }

  if (!row || !same(row.hash, hash) || row.expires <= iso(now) || !same(row.challenge, await s256(verifier))) {
    return said(400, 'That link code is not valid. Link again from d47.');
  }

  const token = randomSecret();
  await env.DB.batch([
    env.DB.prepare('INSERT INTO upload_tokens (hash, customer) VALUES (?, ?)').bind(await sha256(token), row.customer),
    env.DB.prepare('DELETE FROM switch_offs WHERE customer = ?').bind(row.customer),
  ]);

  return json({ token, customerId: row.customer }, { 'cache-control': 'no-store' });
}

// Snapshot

async function readSnapshot(request, env, now) {
  const session = await sessionOf(request, env, now);
  if (!session) {
    return said(401, 'Sign in first.');
  }

  const headers = { 'cache-control': 'no-store' };
  const snapshot = await env.DB.prepare('SELECT body FROM snapshots WHERE customer = ?').bind(session.customer).first();
  if (snapshot) {
    return new Response(snapshot.body, { headers: withCookies({ 'content-type': 'application/json', ...headers }, session.cookies) });
  }

  const off = await env.DB.prepare('SELECT at FROM switch_offs WHERE customer = ?').bind(session.customer).first();
  const linked = await env.DB.prepare('SELECT 1 AS linked FROM upload_tokens WHERE customer = ? LIMIT 1').bind(session.customer).first();
  const state = off ? { state: 'off', since: off.at } : linked ? { state: 'linked' } : { state: 'none' };

  return json(state, headers, 404, session.cookies);
}

async function writeSnapshot(request, env, now) {
  const owner = await tokenOwner(request, env);
  if (!owner) {
    return said(401, 'That upload token is not valid. Link again from d47.');
  }

  const refused = lengthRefusal(request, MOST_SNAPSHOT_BYTES);
  if (refused) return refused;

  const encoding = (request.headers.get('content-encoding') ?? 'identity').trim().toLowerCase();
  if (encoding !== 'identity' && encoding !== 'gzip') {
    return said(415, 'A snapshot is sent as JSON, plain or gzip.');
  }

  const text = await readText(request.body, encoding === 'gzip', MOST_SNAPSHOT_BYTES);
  if (text === TOO_LARGE) {
    return said(413, 'That snapshot is larger than the Worker keeps.');
  }

  let document;
  try {
    document = text === null ? null : JSON.parse(text);
  } catch {
    document = null;
  }
  if (document === null || typeof document !== 'object' || Array.isArray(document)) {
    return said(400, 'The body must be a JSON snapshot.');
  }

  if (document.frontierId !== `F${owner.customer}`) {
    return said(403, "That snapshot's frontierId is not the Frontier account this token was linked to.");
  }

  await env.DB
    .prepare('INSERT OR REPLACE INTO snapshots (customer, body, at) VALUES (?, ?, ?)')
    .bind(owner.customer, text, iso(now))
    .run();

  return empty(204);
}

async function deleteSnapshot(request, env, now) {
  const owner = await tokenOwner(request, env);
  if (!owner) {
    return said(401, 'That upload token is not valid.');
  }

  await env.DB.batch([
    env.DB.prepare('DELETE FROM snapshots WHERE customer = ?').bind(owner.customer),
    env.DB.prepare('DELETE FROM upload_tokens WHERE hash = ?').bind(owner.hash),
    env.DB.prepare('INSERT OR REPLACE INTO switch_offs (customer, at) VALUES (?, ?)').bind(owner.customer, iso(now)),
  ]);

  return empty(204);
}

async function deleteAccount(request, env, now) {
  const session = await sessionOf(request, env, now);
  if (!session) {
    return said(401, 'Sign in first.');
  }

  const customer = session.customer;
  await env.DB.batch([
    env.DB.prepare('DELETE FROM snapshots WHERE customer = ?').bind(customer),
    env.DB.prepare('DELETE FROM upload_tokens WHERE customer = ?').bind(customer),
    env.DB.prepare('DELETE FROM switch_offs WHERE customer = ?').bind(customer),
    env.DB.prepare('DELETE FROM link_codes WHERE customer = ?').bind(customer),
    env.DB.prepare('DELETE FROM sessions WHERE customer = ?').bind(customer),
  ]);

  return empty(204, [cookie(SESSION_COOKIE, '', 0)]);
}

// Sessions and tokens

/** The signed-in session, extended to 30 days from now at most once a day; null when there is none. */
async function sessionOf(request, env, now) {
  const value = readCookie(request, SESSION_COOKIE);
  if (!value || !SECRET.test(value)) return null;

  const hash = await sha256(value);
  const row = await env.DB.prepare('SELECT hash, customer, expires FROM sessions WHERE hash = ?').bind(hash).first();
  if (!row || !same(row.hash, hash) || row.expires <= iso(now)) return null;

  const cookies = [];
  if (row.expires < iso(now + SESSION_LIFETIME - SESSION_REFRESH_AFTER)) {
    await env.DB.prepare('UPDATE sessions SET expires = ? WHERE hash = ?').bind(iso(now + SESSION_LIFETIME), hash).run();
    cookies.push(cookie(SESSION_COOKIE, value, SESSION_LIFETIME));
  }

  return { customer: row.customer, cookies };
}

async function tokenOwner(request, env) {
  const match = /^Bearer ([A-Za-z0-9_-]{43})$/.exec(request.headers.get('authorization') ?? '');
  if (!match) return null;

  const hash = await sha256(match[1]);
  const row = await env.DB.prepare('SELECT hash, customer FROM upload_tokens WHERE hash = ?').bind(hash).first();
  return row && same(row.hash, hash) ? row : null;
}

/** A /link path rebuilt from a port of 1024 to 65535 and a 43-character challenge, or null. */
function linkPath(path) {
  if (typeof path !== 'string' || !path.startsWith('/link?')) return null;

  const query = new URLSearchParams(path.slice('/link?'.length));
  const port = query.get('port') ?? '';
  const challenge = query.get('challenge') ?? '';
  if (!PORT.test(port) || Number(port) < 1024 || Number(port) > 65535 || !SECRET.test(challenge)) return null;

  return `/link?port=${Number(port)}&challenge=${challenge}`;
}

function decodeNext(packed) {
  try {
    return new TextDecoder().decode(fromBase64url(packed ?? ''));
  } catch {
    return null;
  }
}

function sameSite(request, url) {
  const origin = request.headers.get('origin');
  return origin === null || origin === url.origin;
}

// Bodies

const TOO_LARGE = Symbol('too large');

function lengthRefusal(request, most) {
  const length = Number(request.headers.get('content-length'));
  if (!Number.isInteger(length) || length <= 0) {
    return said(411, 'The body must declare its length.');
  }
  if (length > most) {
    return said(413, 'That body is larger than this route takes.');
  }
  return null;
}

/** The body as UTF-8 text, gunzipped when asked; TOO_LARGE past `most` bytes, null when it is not text. */
async function readText(body, gzip, most) {
  if (!body) return null;

  const chunks = [];
  let total = 0;
  try {
    const reader = (gzip ? body.pipeThrough(new DecompressionStream('gzip')) : body).getReader();
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > most) {
        await reader.cancel();
        return TOO_LARGE;
      }
      chunks.push(value);
    }
  } catch {
    return null;
  }

  const bytes = new Uint8Array(total);
  let at = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, at);
    at += chunk.byteLength;
  }

  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return null;
  }
}

// Secrets and hashes

function randomSecret() {
  return base64url(crypto.getRandomValues(new Uint8Array(32)));
}

async function sha256(text) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}

async function s256(verifier) {
  return base64url(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))));
}

/** Compares two strings in time that depends on their length only. */
function same(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string' || a.length !== b.length) return false;
  let difference = 0;
  for (let i = 0; i < a.length; i++) difference |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return difference === 0;
}

function base64url(bytes) {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64url(text) {
  const binary = atob(text.replace(/-/g, '+').replace(/_/g, '/'));
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

function iso(ms) {
  return new Date(ms).toISOString();
}

// Responses

function readCookie(request, name) {
  for (const part of (request.headers.get('cookie') ?? '').split(';')) {
    const at = part.indexOf('=');
    if (at > 0 && part.slice(0, at).trim() === name) return part.slice(at + 1).trim();
  }
  return null;
}

function cookie(name, value, lifetimeMs) {
  return `${name}=${value}; Path=/; Secure; HttpOnly; SameSite=Lax; Max-Age=${Math.floor(lifetimeMs / 1000)}`;
}

function withCookies(headers, cookies = []) {
  const out = new Headers(headers);
  for (const c of cookies) out.append('set-cookie', c);
  return out;
}

function redirect(location, cookies = []) {
  return new Response(null, { status: 303, headers: withCookies({ location, 'cache-control': 'no-store' }, cookies) });
}

function json(body, headers = {}, status = 200, cookies = []) {
  return new Response(JSON.stringify(body), { status, headers: withCookies({ 'content-type': 'application/json', ...headers }, cookies) });
}

function empty(status, cookies = []) {
  return new Response(null, { status, headers: withCookies({}, cookies) });
}

function said(status, text, cookies = []) {
  return new Response(text + '\n', { status, headers: withCookies({ 'content-type': 'text/plain' }, cookies) });
}
