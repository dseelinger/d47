// Run with `node --test` from this folder. Node's built-in SQLite stands in for D1, so the
// Worker's SQL runs for real, and a fake stands in for Frontier's auth server. Not part of `dotnet test`.

import { test, beforeEach, afterEach, mock } from 'node:test';
import assert from 'node:assert/strict';
import { createHash, randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import { gzipSync } from 'node:zlib';
import worker from './src/index.js';

const ORIGIN = 'https://web.invalid';
const CUSTOMER = '1234567';
const OTHER = '7654321';
const T0 = Date.parse('2026-11-13T12:00:00.000Z');
const DAY = 24 * 60 * 60 * 1000;

// What the fake Frontier hands back; none of it may be stored.
const PERSONAL = ['frontier-access-token', 'frontier-refresh-token', 'cmdr@example.invalid', 'Vesna', 'Okonkwo-Hale'];

/** A D1-shaped wrapper over in-memory SQLite with the real migration applied. */
function database() {
  const sqlite = new DatabaseSync(':memory:');
  sqlite.exec(readFileSync(new URL('./migrations/0001_web.sql', import.meta.url), 'utf8'));

  const statement = (sql, args = []) => ({
    sql,
    bind: (...bound) => statement(sql, bound),
    first: async () => sqlite.prepare(sql).get(...args) ?? null,
    all: async () => ({ results: sqlite.prepare(sql).all(...args) }),
    run: async () => sqlite.prepare(sql).run(...args),
  });

  return {
    sqlite,
    prepare: (sql) => statement(sql),
    batch: async (statements) => {
      const out = [];
      sqlite.exec('BEGIN');
      try {
        for (const s of statements) out.push(/^\s*SELECT/i.test(s.sql) ? await s.all() : await s.run());
        sqlite.exec('COMMIT');
      } catch (error) {
        sqlite.exec('ROLLBACK');
        throw error;
      }
      return out;
    },
  };
}

const TABLES = ['sessions', 'link_codes', 'upload_tokens', 'snapshots', 'switch_offs'];
const rows = (env, table) => env.DB.sqlite.prepare(`SELECT * FROM ${table}`).all().map((row) => ({ ...row }));
const everything = (env) => JSON.stringify(Object.fromEntries(TABLES.map((t) => [t, rows(env, t)])));

let frontier;
let realFetch;

beforeEach(() => {
  mock.timers.enable({ apis: ['Date'], now: T0 });
  frontier = { customer: CUSTOMER, token: 200, decode: 200, calls: [] };
  realFetch = globalThis.fetch;
  globalThis.fetch = async (input, init = {}) => {
    const url = new URL(input);
    frontier.calls.push({ url, init });
    if (url.origin !== 'https://auth.frontierstore.net') throw new Error(`unexpected fetch to ${url}`);
    if (url.pathname === '/token') {
      return Response.json(
        { access_token: 'frontier-access-token', token_type: 'Bearer', expires_in: 14400, refresh_token: 'frontier-refresh-token' },
        { status: frontier.token },
      );
    }
    if (url.pathname === '/decode') {
      return Response.json(
        {
          iss: 'https://auth.frontierstore.net',
          scope: 'auth',
          usr: { email: 'cmdr@example.invalid', customer_id: frontier.customer, firstname: 'Vesna', lastname: 'Okonkwo-Hale', platform: 'frontier' },
        },
        { status: frontier.decode },
      );
    }
    return new Response('not found', { status: 404 });
  };
});

afterEach(() => {
  mock.timers.reset();
  globalThis.fetch = realFetch;
});

const environment = () => ({
  DB: database(),
  FRONTIER_CLIENT_ID: 'test-client-id',
  ASSETS: { fetch: async (request) => new Response(`asset ${new URL(request.url).pathname}`) },
});

const setTime = (ms) => mock.timers.setTime(ms);

function send(env, method, path, { headers = {}, body, cookie } = {}) {
  const init = { method, headers: { ...headers } };
  if (cookie) init.headers.cookie = cookie;
  if (body !== undefined) {
    init.body = body;
    init.headers['content-length'] ??= String(typeof body === 'string' ? Buffer.byteLength(body) : body.byteLength);
  }
  return worker.fetch(new Request(`${ORIGIN}${path}`, init), env);
}

const setCookies = (response) => response.headers.getSetCookie();
const cookieValue = (response, name) => {
  const line = setCookies(response).find((c) => c.startsWith(`${name}=`));
  return line ? line.slice(name.length + 1).split(';')[0] : undefined;
};
const sha256 = (text) => createHash('sha256').update(text).digest('hex');
const s256 = (text) => createHash('sha256').update(text).digest('base64url');
const secret = () => randomBytes(32).toString('base64url');

/** Signs in through the fake Frontier and returns the session cookie value and the final response. */
async function signIn(env, next) {
  const start = await send(env, 'GET', next ? `/signin?next=${encodeURIComponent(next)}` : '/signin');
  const state = new URL(start.headers.get('location')).searchParams.get('state');
  const signinCookie = `__Host-d47-signin=${cookieValue(start, '__Host-d47-signin')}`;
  const done = await send(env, 'GET', `/callback?code=frontier-code&state=${state}`, { cookie: signinCookie });
  return { session: cookieValue(done, '__Host-d47-session'), done };
}

const sessionCookie = (session) => `__Host-d47-session=${session}`;

/** Links an install for the signed-in session and returns the upload token response. */
async function link(env, session, { verifier = secret() } = {}) {
  const linked = await send(env, 'GET', `/link?port=50123&challenge=${s256(verifier)}`, { cookie: sessionCookie(session) });
  const code = new URL(linked.headers.get('location')).searchParams.get('code');
  return send(env, 'POST', '/link/token', { body: JSON.stringify({ code, verifier }) });
}

async function uploadToken(env, session) {
  return (await (await link(env, session)).json()).token;
}

const document = (frontierId = `F${CUSTOMER}`, extra = {}) => JSON.stringify({ format: 1, frontierId, name: 'Vale Tamsin', ...extra });

const putSnapshot = (env, token, body = document(), headers = {}) =>
  send(env, 'PUT', '/snapshot', { headers: { authorization: `Bearer ${token}`, ...headers }, body });

const getSnapshot = (env, session) => send(env, 'GET', '/snapshot', { cookie: sessionCookie(session) });

// Sign-in

test('SignInRedirectsToFrontierWithARandomStateAndAnS256Challenge', async () => {
  const env = environment();
  const first = await send(env, 'GET', '/signin');
  const second = await send(env, 'GET', '/signin');

  assert.equal(first.status, 303);
  const auth = new URL(first.headers.get('location'));
  assert.equal(auth.origin + auth.pathname, 'https://auth.frontierstore.net/auth');
  assert.equal(auth.searchParams.get('client_id'), 'test-client-id');
  assert.equal(auth.searchParams.get('scope'), 'auth');
  assert.equal(auth.searchParams.get('response_type'), 'code');
  assert.equal(auth.searchParams.get('code_challenge_method'), 'S256');
  assert.equal(auth.searchParams.get('redirect_uri'), `${ORIGIN}/callback`);
  assert.match(auth.searchParams.get('state'), /^[A-Za-z0-9_-]{43}$/);
  assert.notEqual(auth.searchParams.get('state'), new URL(second.headers.get('location')).searchParams.get('state'));

  const [state, verifier] = cookieValue(first, '__Host-d47-signin').split('.');
  assert.equal(state, auth.searchParams.get('state'));
  assert.equal(s256(verifier), auth.searchParams.get('code_challenge'));
  assert.match(setCookies(first)[0], /; Path=\/; Secure; HttpOnly; SameSite=Lax; Max-Age=600$/);
  assert.equal(everything(env), JSON.stringify(Object.fromEntries(TABLES.map((t) => [t, []]))));
});

test('TheCallbackExchangesTheCodeWithPkceAndSetsASessionCookie', async () => {
  const env = environment();
  const start = await send(env, 'GET', '/signin');
  const auth = new URL(start.headers.get('location'));
  const [, verifier] = cookieValue(start, '__Host-d47-signin').split('.');

  const done = await send(env, 'GET', `/callback?code=frontier-code&state=${auth.searchParams.get('state')}`, {
    cookie: `__Host-d47-signin=${cookieValue(start, '__Host-d47-signin')}`,
  });

  assert.equal(done.status, 303);
  assert.equal(done.headers.get('location'), '/');

  const exchange = frontier.calls.find((c) => c.url.pathname === '/token');
  const form = new URLSearchParams(exchange.init.body);
  assert.equal(exchange.init.method, 'POST');
  assert.equal(form.get('grant_type'), 'authorization_code');
  assert.equal(form.get('client_id'), 'test-client-id');
  assert.equal(form.get('code'), 'frontier-code');
  assert.equal(form.get('code_verifier'), verifier);
  assert.equal(form.get('redirect_uri'), `${ORIGIN}/callback`);
  assert.equal(form.has('client_secret'), false);
  assert.match(exchange.init.headers['user-agent'], /^EDCD-[A-Za-z]+-[.0-9]+$/);

  const decode = frontier.calls.find((c) => c.url.pathname === '/decode');
  assert.equal(decode.init.headers.authorization, 'Bearer frontier-access-token');

  const session = cookieValue(done, '__Host-d47-session');
  assert.match(session, /^[A-Za-z0-9_-]{43}$/);
  assert.equal(Buffer.from(session, 'base64url').length, 32);
  const line = setCookies(done).find((c) => c.startsWith('__Host-d47-session='));
  assert.match(line, /; Path=\/; Secure; HttpOnly; SameSite=Lax; Max-Age=2592000$/);
  assert.ok(setCookies(done).some((c) => c.startsWith('__Host-d47-signin=;') && c.endsWith('Max-Age=0')));

  assert.deepEqual(rows(env, 'sessions'), [
    { hash: sha256(session), customer: CUSTOMER, expires: new Date(T0 + 30 * DAY).toISOString() },
  ]);
});

test('NothingFrontierSentIsStoredButTheCustomerId', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  await putSnapshot(env, token);

  const stored = everything(env);
  for (const value of [...PERSONAL, session, token, 'frontier-code']) {
    assert.equal(stored.includes(value), false, value);
  }
  assert.ok(stored.includes(CUSTOMER));
});

test('ACallbackWithAnotherStateOrNoSignInCookieIsRefused', async () => {
  const env = environment();
  const start = await send(env, 'GET', '/signin');
  const cookie = `__Host-d47-signin=${cookieValue(start, '__Host-d47-signin')}`;

  assert.equal((await send(env, 'GET', `/callback?code=c&state=${secret()}`, { cookie })).status, 400);
  assert.equal((await send(env, 'GET', '/callback?code=c', { cookie })).status, 400);

  const state = new URL(start.headers.get('location')).searchParams.get('state');
  assert.equal((await send(env, 'GET', `/callback?code=c&state=${state}`)).status, 400);
  assert.equal(frontier.calls.length, 0);
  assert.equal(rows(env, 'sessions').length, 0);
});

test('ADeclinedSignInStoresNothing', async () => {
  const env = environment();
  const start = await send(env, 'GET', '/signin');
  const state = new URL(start.headers.get('location')).searchParams.get('state');

  const declined = await send(env, 'GET', `/callback?error=access_denied&state=${state}`, {
    cookie: `__Host-d47-signin=${cookieValue(start, '__Host-d47-signin')}`,
  });

  assert.equal(declined.status, 403);
  assert.equal(cookieValue(declined, '__Host-d47-session'), undefined);
  assert.equal(frontier.calls.length, 0);
  assert.equal(rows(env, 'sessions').length, 0);
});

test('AFailedExchangeOrDecodeStoresNothing', async () => {
  for (const failure of [{ token: 400 }, { decode: 401 }, { customer: 'not-a-number' }, { customer: undefined }]) {
    const env = environment();
    Object.assign(frontier, failure);

    const { done } = await signIn(env);

    assert.equal(done.status, 502, JSON.stringify(failure));
    assert.equal(rows(env, 'sessions').length, 0);
    Object.assign(frontier, { token: 200, decode: 200, customer: CUSTOMER });
  }
});

// Sessions

test('ASessionLastsThirtyDaysFromItsLastUse', async () => {
  const env = environment();
  const { session } = await signIn(env);

  setTime(T0 + 20 * DAY);
  const used = await getSnapshot(env, session);
  assert.equal(used.status, 404);
  assert.match(setCookies(used).find((c) => c.startsWith('__Host-d47-session=')), /Max-Age=2592000$/);

  setTime(T0 + 49 * DAY);
  assert.equal((await getSnapshot(env, session)).status, 404);

  setTime(T0 + 49 * DAY + 30 * DAY + 1);
  assert.equal((await getSnapshot(env, session)).status, 401);
});

test('ASessionUnusedForThirtyDaysHasEnded', async () => {
  const env = environment();
  const { session } = await signIn(env);

  setTime(T0 + 30 * DAY);
  assert.equal((await getSnapshot(env, session)).status, 401);
});

test('ASessionIsRefreshedAtMostOnceADay', async () => {
  const env = environment();
  const { session } = await signIn(env);

  setTime(T0 + 12 * 60 * 60 * 1000);
  const used = await getSnapshot(env, session);

  assert.equal(setCookies(used).length, 0);
  assert.equal(rows(env, 'sessions')[0].expires, new Date(T0 + 30 * DAY).toISOString());
});

test('SigningOutDeletesTheSession', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const { session: other } = await signIn(env);

  const out = await send(env, 'POST', '/signout', { cookie: sessionCookie(session) });

  assert.equal(out.status, 204);
  assert.ok(setCookies(out).some((c) => c.startsWith('__Host-d47-session=;') && c.endsWith('Max-Age=0')));
  assert.equal((await getSnapshot(env, session)).status, 401);
  assert.equal((await getSnapshot(env, other)).status, 404);
});

test('ASnapshotNeedsASession', async () => {
  const env = environment();

  assert.equal((await send(env, 'GET', '/snapshot')).status, 401);
  assert.equal((await getSnapshot(env, secret())).status, 401);
  assert.equal((await send(env, 'DELETE', '/account')).status, 401);
});

// Desktop link

test('ALinkRedirectsToTheLoopbackAddressWithAShortLivedCode', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const verifier = secret();

  const linked = await send(env, 'GET', `/link?port=50123&challenge=${s256(verifier)}`, { cookie: sessionCookie(session) });

  assert.equal(linked.status, 303);
  const location = new URL(linked.headers.get('location'));
  assert.equal(location.origin, 'http://127.0.0.1:50123');
  assert.equal(location.pathname, '/linked');
  const code = location.searchParams.get('code');
  assert.deepEqual(rows(env, 'link_codes'), [
    { hash: sha256(code), customer: CUSTOMER, challenge: s256(verifier), expires: new Date(T0 + 2 * 60 * 1000).toISOString() },
  ]);
});

test('ALinkWithoutASessionSignsInFirstAndComesBack', async () => {
  const env = environment();
  const challenge = s256(secret());
  const path = `/link?port=50123&challenge=${challenge}`;

  const first = await send(env, 'GET', path);
  assert.equal(first.status, 303);
  assert.equal(first.headers.get('location'), `/signin?next=${encodeURIComponent(path)}`);

  const { session, done } = await signIn(env, path);
  assert.equal(done.headers.get('location'), path);

  const linked = await send(env, 'GET', path, { cookie: sessionCookie(session) });
  assert.equal(new URL(linked.headers.get('location')).origin, 'http://127.0.0.1:50123');
});

test('ASignInNextThatIsNotALinkGoesHome', async () => {
  const env = environment();

  for (const next of ['https://evil.example/', '//evil.example/', '/link?port=80&challenge=x', '/account']) {
    const { done } = await signIn(env, next);
    assert.equal(done.headers.get('location'), '/', next);
  }
});

test('ALinkWithABadPortOrChallengeIsRefused', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const challenge = s256(secret());

  for (const port of ['80@evil.example', '1234/x', '0', '1023', '65536', '', 'abc', '5012a', '-1']) {
    const response = await send(env, 'GET', `/link?port=${encodeURIComponent(port)}&challenge=${challenge}`, { cookie: sessionCookie(session) });
    assert.equal(response.status, 400, port);
  }
  for (const bad of ['', 'short', `${challenge}x`, challenge.slice(0, 42) + '+']) {
    const response = await send(env, 'GET', `/link?port=50123&challenge=${encodeURIComponent(bad)}`, { cookie: sessionCookie(session) });
    assert.equal(response.status, 400, bad);
  }
  assert.equal(rows(env, 'link_codes').length, 0);
});

test('ALinkCodeAndVerifierReturnAnUploadTokenOnce', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const verifier = secret();
  const linked = await send(env, 'GET', `/link?port=50123&challenge=${s256(verifier)}`, { cookie: sessionCookie(session) });
  const code = new URL(linked.headers.get('location')).searchParams.get('code');

  const first = await send(env, 'POST', '/link/token', { body: JSON.stringify({ code, verifier }) });
  const again = await send(env, 'POST', '/link/token', { body: JSON.stringify({ code, verifier }) });

  assert.equal(first.status, 200);
  const { token, customerId } = await first.json();
  assert.equal(customerId, CUSTOMER);
  assert.match(token, /^[A-Za-z0-9_-]{43}$/);
  assert.deepEqual(rows(env, 'upload_tokens'), [{ hash: sha256(token), customer: CUSTOMER }]);
  assert.equal(rows(env, 'link_codes').length, 0);
  assert.equal(again.status, 400);
});

test('ALinkCodeExpiresAfterTwoMinutes', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const verifier = secret();
  const linked = await send(env, 'GET', `/link?port=50123&challenge=${s256(verifier)}`, { cookie: sessionCookie(session) });
  const code = new URL(linked.headers.get('location')).searchParams.get('code');

  setTime(T0 + 2 * 60 * 1000);
  const late = await send(env, 'POST', '/link/token', { body: JSON.stringify({ code, verifier }) });

  assert.equal(late.status, 400);
  assert.equal(rows(env, 'upload_tokens').length, 0);
});

test('AWrongVerifierIsRefusedAndUsesUpTheCode', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const verifier = secret();
  const linked = await send(env, 'GET', `/link?port=50123&challenge=${s256(verifier)}`, { cookie: sessionCookie(session) });
  const code = new URL(linked.headers.get('location')).searchParams.get('code');

  const wrong = await send(env, 'POST', '/link/token', { body: JSON.stringify({ code, verifier: secret() }) });
  const right = await send(env, 'POST', '/link/token', { body: JSON.stringify({ code, verifier }) });

  assert.equal(wrong.status, 400);
  assert.equal(right.status, 400);
  assert.equal(rows(env, 'upload_tokens').length, 0);
});

test('AMalformedLinkTokenRequestIsRefused', async () => {
  const env = environment();

  for (const body of ['nope', '{}', JSON.stringify({ code: secret() }), JSON.stringify({ code: 'short', verifier: secret() })]) {
    assert.equal((await send(env, 'POST', '/link/token', { body })).status, 400, body);
  }
  const big = JSON.stringify({ code: secret(), verifier: 'x'.repeat(600) });
  assert.equal((await send(env, 'POST', '/link/token', { body: big })).status, 413);
  assert.equal((await send(env, 'POST', '/link/token')).status, 411);
});

// Snapshot

test('ASnapshotIsStoredForTheTokensCustomerAndReplacesTheOneBefore', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);

  assert.equal((await putSnapshot(env, token, document(`F${CUSTOMER}`, { name: 'first' }))).status, 204);
  setTime(T0 + DAY);
  assert.equal((await putSnapshot(env, token, document(`F${CUSTOMER}`, { name: 'second' }))).status, 204);

  assert.deepEqual(rows(env, 'snapshots'), [
    { customer: CUSTOMER, body: document(`F${CUSTOMER}`, { name: 'second' }), at: new Date(T0 + DAY).toISOString() },
  ]);

  const read = await getSnapshot(env, session);
  assert.equal(read.status, 200);
  assert.equal(read.headers.get('content-type'), 'application/json');
  assert.equal(read.headers.get('cache-control'), 'no-store');
  assert.equal(await read.text(), document(`F${CUSTOMER}`, { name: 'second' }));
});

test('AGzipSnapshotIsStoredAsItsJson', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);

  const response = await putSnapshot(env, token, gzipSync(document()), { 'content-encoding': 'gzip' });

  assert.equal(response.status, 204);
  assert.equal(await (await getSnapshot(env, session)).text(), document());
});

test('AnOversizeSnapshotIsRefusedBeforeItIsRead', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);

  const request = new Request(`${ORIGIN}/snapshot`, {
    method: 'PUT',
    headers: { authorization: `Bearer ${token}`, 'content-length': '1000001' },
    body: document(),
  });
  let read = false;
  Object.defineProperty(request, 'body', { get: () => { read = true; return null; } });
  request.text = async () => { read = true; return ''; };

  const response = await worker.fetch(request, env);

  assert.equal(response.status, 413);
  assert.equal(read, false);
  assert.equal(rows(env, 'snapshots').length, 0);
});

test('AGzipSnapshotThatInflatesPastTheCapIsRefused', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  const huge = document(`F${CUSTOMER}`, { padding: 'x'.repeat(1_000_001) });

  const response = await putSnapshot(env, token, gzipSync(huge), { 'content-encoding': 'gzip' });

  assert.equal(response.status, 413);
  assert.equal(rows(env, 'snapshots').length, 0);
});

test('ASnapshotInAnotherEncodingIsRefused', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);

  assert.equal((await putSnapshot(env, token, document(), { 'content-encoding': 'br' })).status, 415);
  assert.equal(rows(env, 'snapshots').length, 0);
});

test('ASnapshotForAnotherAccountOrNotJsonIsRefused', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);

  for (const body of [document(`F${OTHER}`), document(CUSTOMER), document(`f${CUSTOMER}`), JSON.stringify({ format: 1 })]) {
    assert.equal((await putSnapshot(env, token, body)).status, 403, body);
  }
  for (const body of ['nope', '[]', 'null', '"F1234567"']) {
    assert.equal((await putSnapshot(env, token, body)).status, 400, body);
  }
  assert.equal(rows(env, 'snapshots').length, 0);
});

test('ASnapshotWithoutAValidUploadTokenIsRefused', async () => {
  const env = environment();
  const { session } = await signIn(env);
  await uploadToken(env, session);

  for (const authorization of [null, `Bearer ${secret()}`, `Bearer ${session}`, 'Basic abc']) {
    const headers = authorization ? { authorization } : {};
    assert.equal((await send(env, 'PUT', '/snapshot', { headers, body: document() })).status, 401, String(authorization));
    assert.equal((await send(env, 'DELETE', '/snapshot', { headers })).status, 401, String(authorization));
  }
  assert.equal(rows(env, 'snapshots').length, 0);
  assert.equal(rows(env, 'switch_offs').length, 0);
});

test('ASnapshotWithoutALengthIsRefused', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);

  const response = await send(env, 'PUT', '/snapshot', { headers: { authorization: `Bearer ${token}` } });

  assert.equal(response.status, 411);
});

// The three 404 states

test('WithNoSnapshotTheStateIsNoneUntilAnInstallIsLinked', async () => {
  const env = environment();
  const { session } = await signIn(env);

  const none = await getSnapshot(env, session);
  assert.equal(none.status, 404);
  assert.deepEqual(await none.json(), { state: 'none' });

  await uploadToken(env, session);
  const linked = await getSnapshot(env, session);
  assert.equal(linked.status, 404);
  assert.deepEqual(await linked.json(), { state: 'linked' });
});

test('DeletingTheSnapshotDeletesItAndItsTokenAndLeavesTheTimeItWasTurnedOff', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  await putSnapshot(env, token);

  setTime(T0 + DAY);
  const deleted = await send(env, 'DELETE', '/snapshot', { headers: { authorization: `Bearer ${token}` } });

  assert.equal(deleted.status, 204);
  assert.equal(rows(env, 'snapshots').length, 0);
  assert.equal(rows(env, 'upload_tokens').length, 0);
  assert.deepEqual(rows(env, 'switch_offs'), [{ customer: CUSTOMER, at: new Date(T0 + DAY).toISOString() }]);

  const off = await getSnapshot(env, session);
  assert.equal(off.status, 404);
  assert.deepEqual(await off.json(), { state: 'off', since: new Date(T0 + DAY).toISOString() });
  assert.equal((await putSnapshot(env, token)).status, 401);
});

test('ANewUploadTokenRemovesTheSwitchOffRecord', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  await send(env, 'DELETE', '/snapshot', { headers: { authorization: `Bearer ${token}` } });

  await uploadToken(env, session);

  assert.equal(rows(env, 'switch_offs').length, 0);
  assert.deepEqual(await (await getSnapshot(env, session)).json(), { state: 'linked' });
});

test('ASwitchOffIsReportedWhileAnotherInstallIsStillLinked', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const first = await uploadToken(env, session);
  await uploadToken(env, session);

  await send(env, 'DELETE', '/snapshot', { headers: { authorization: `Bearer ${first}` } });

  assert.equal(rows(env, 'upload_tokens').length, 1);
  assert.equal((await getSnapshot(env, session).then((r) => r.json())).state, 'off');
});

// Account

test('DeletingTheAccountDeletesEverythingForThatCustomerOnly', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const { session: second } = await signIn(env);
  const token = await uploadToken(env, session);
  await putSnapshot(env, token);
  const spare = await uploadToken(env, session);
  await send(env, 'DELETE', '/snapshot', { headers: { authorization: `Bearer ${spare}` } });
  await send(env, 'GET', `/link?port=50123&challenge=${s256(secret())}`, { cookie: sessionCookie(session) });

  frontier.customer = OTHER;
  const { session: other } = await signIn(env);
  const otherToken = await uploadToken(env, other);
  await putSnapshot(env, otherToken, document(`F${OTHER}`));

  const deleted = await send(env, 'DELETE', '/account', { cookie: sessionCookie(session) });

  assert.equal(deleted.status, 204);
  assert.ok(setCookies(deleted).some((c) => c.startsWith('__Host-d47-session=;')));
  for (const table of TABLES) {
    assert.deepEqual(rows(env, table).filter((r) => r.customer === CUSTOMER), [], table);
  }
  assert.equal((await getSnapshot(env, second)).status, 401);
  assert.equal((await getSnapshot(env, other)).status, 200);
});

// Expiry

test('ASnapshotNotReplacedForNinetyDaysIsDeletedWithItsTokens', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  await putSnapshot(env, token);

  frontier.customer = OTHER;
  const { session: other } = await signIn(env);
  const otherToken = await uploadToken(env, other);
  setTime(T0 + 10 * DAY);
  await putSnapshot(env, otherToken, document(`F${OTHER}`));

  setTime(T0 + 90 * DAY - 1);
  await worker.scheduled({}, env);
  assert.equal(rows(env, 'snapshots').length, 2);

  setTime(T0 + 90 * DAY + 1);
  await worker.scheduled({}, env);

  assert.deepEqual(rows(env, 'snapshots').map((r) => r.customer), [OTHER]);
  assert.deepEqual(rows(env, 'upload_tokens').map((r) => r.customer), [OTHER]);
});

test('ASwitchOffRecordIsDeletedNinetyDaysAfterItWasWritten', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  await send(env, 'DELETE', '/snapshot', { headers: { authorization: `Bearer ${token}` } });

  setTime(T0 + 90 * DAY - 1);
  await worker.scheduled({}, env);
  assert.equal(rows(env, 'switch_offs').length, 1);

  setTime(T0 + 90 * DAY + 1);
  await worker.scheduled({}, env);
  assert.equal(rows(env, 'switch_offs').length, 0);
});

test('EndedSessionsAndLinkCodesAreDeletedOnSchedule', async () => {
  const env = environment();
  const { session } = await signIn(env);
  await send(env, 'GET', `/link?port=50123&challenge=${s256(secret())}`, { cookie: sessionCookie(session) });

  setTime(T0 + 30 * DAY);
  await worker.scheduled({}, env);

  assert.equal(rows(env, 'sessions').length, 0);
  assert.equal(rows(env, 'link_codes').length, 0);
});

// Origins, methods, paths and files

test('EveryRouteThatChangesSomethingRefusesAnotherSite', async () => {
  const env = environment();
  const { session } = await signIn(env);
  const token = await uploadToken(env, session);
  await putSnapshot(env, token);
  const bearer = { authorization: `Bearer ${token}` };

  for (const origin of ['https://evil.example', 'null', 'http://web.invalid']) {
    const headers = { origin };
    const refusals = [
      await send(env, 'POST', '/signout', { headers, cookie: sessionCookie(session) }),
      await send(env, 'POST', '/link/token', { headers, body: JSON.stringify({ code: secret(), verifier: secret() }) }),
      await send(env, 'PUT', '/snapshot', { headers: { ...headers, ...bearer }, body: document() }),
      await send(env, 'DELETE', '/snapshot', { headers: { ...headers, ...bearer } }),
      await send(env, 'DELETE', '/account', { headers, cookie: sessionCookie(session) }),
      await send(env, 'GET', `/link?port=50123&challenge=${s256(secret())}`, { headers, cookie: sessionCookie(session) }),
    ];
    assert.deepEqual(refusals.map((r) => r.status), [403, 403, 403, 403, 403, 403], origin);
  }

  assert.equal(rows(env, 'link_codes').length, 0);
  assert.equal(rows(env, 'sessions').length, 1);
  assert.equal(rows(env, 'snapshots').length, 1);

  const own = await send(env, 'POST', '/signout', { headers: { origin: ORIGIN }, cookie: sessionCookie(session) });
  assert.equal(own.status, 204);
});

test('OtherMethodsAreRefusedAndOtherPathsAreFiles', async () => {
  const env = environment();

  assert.equal((await send(env, 'POST', '/signin')).status, 405);
  assert.equal((await send(env, 'GET', '/signout')).status, 405);
  assert.equal((await send(env, 'GET', '/link/token')).status, 405);
  assert.equal((await send(env, 'POST', '/snapshot')).status, 405);
  assert.equal((await send(env, 'GET', '/account')).status, 405);
  assert.equal((await send(env, 'POST', '/index.html')).status, 404);

  const page = await send(env, 'GET', '/index.html');
  assert.equal(await page.text(), 'asset /index.html');
});
