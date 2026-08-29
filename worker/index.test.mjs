// The Worker's refusals, driven rather than read (https://github.com/dseelinger/d47/issues/175).
//
// **Run it with `node --test` from this folder.** It uses Node's own test runner and nothing else:
// no framework, no package.json, no node_modules. A second language in the tree is the honest cost
// of the endpoint, and a second dependency tree on top of it would not be.
//
// **It is not in `dotnet test`, and that is a gap said out loud rather than papered over.** The
// three gates that run as tests do so because they must not drift from the code; this one cannot
// be one of them without putting Node in CI, which is a decision for the Commander rather than a
// side effect of this issue. What it does buy is that the hard stops below are exercised at all —
// and the one that matters most, size, is refused before a single byte of body is read.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import worker from './src/index.js';

const TOKEN = '0123456789abcdef0123456789abcdef';
const SHA = 'a'.repeat(64);

/** A bucket that records what it was asked to store, and can be asserted to have been asked nothing. */
function bucket() {
  const puts = [];
  return { puts, put: async (key, _body, options) => void puts.push({ key, options }) };
}

/** A donation, with any header overridable and any of them removable by passing null. */
function donation(overrides = {}, body = 'payload') {
  const headers = {
    'd47-format': '1',
    'd47-kind': 'excerpt',
    'd47-donor': TOKEN,
    'd47-sha256': SHA,
    'd47-build': '0.89.0+abcdef',
    'd47-taken-at': '20260829T142530Z',
    'd47-bytes': '7',
    'content-length': String(body.length),
    ...overrides,
  };

  for (const [name, value] of Object.entries(headers)) {
    if (value === null) delete headers[name];
  }

  return new Request('https://donate.invalid/donate', { method: 'POST', headers, body });
}

async function post(request, env = { DONATIONS: bucket() }) {
  return { response: await worker.fetch(request, env), env };
}

test('a well-formed donation is stored under its kind prefix', async () => {
  const { response, env } = await post(donation());

  assert.equal(response.status, 201);

  const stored = await response.json();

  assert.equal(stored.ok, true);
  assert.match(stored.key, new RegExp(`^excerpts/${TOKEN}/\\d{8}T\\d{6}Z-${SHA.slice(0, 16)}\\.md\\.gz$`));
  assert.equal(env.DONATIONS.puts[0].key, stored.key);
});

test('a corpus lands under the other prefix, because the retention rules are opposite', async () => {
  const { env } = await post(donation({ 'd47-kind': 'corpus' }));

  assert.ok(env.DONATIONS.puts[0].key.startsWith('corpus/'));
  assert.ok(env.DONATIONS.puts[0].key.endsWith('.jsonl.gz'));
});

test('the consent hash is kept beside the bytes', async () => {
  const { env } = await post(donation());

  assert.equal(env.DONATIONS.puts[0].options.customMetadata.sha256, SHA);
  assert.equal(env.DONATIONS.puts[0].options.httpMetadata.contentEncoding, 'gzip');
});

// **Refuse rather than guess.** A newer client's payload stored under this build's assumptions is
// a corpus nobody can trust and nobody can tell is wrong.
for (const [what, overrides] of [
  ['an unversioned envelope', { 'd47-format': null }],
  ['a format from the future', { 'd47-format': '2' }],
  ['an unknown kind', { 'd47-kind': 'everything' }],
  ['no donation identifier', { 'd47-donor': null }],
  ['an identifier that is a path', { 'd47-donor': '../../etc/passwd' }],
  ['an identifier of the wrong length', { 'd47-donor': 'abc' }],
  ['an uppercase identifier', { 'd47-donor': TOKEN.toUpperCase() }],
  ['no payload hash', { 'd47-sha256': null }],
  ['a hash that is not one', { 'd47-sha256': 'nope' }],
  ['no declared length', { 'content-length': null }],
]) {
  test(`${what} is refused, and nothing is written`, async () => {
    const { response, env } = await post(donation(overrides));

    assert.ok(response.status >= 400, `expected a refusal, got ${response.status}`);
    assert.equal(env.DONATIONS.puts.length, 0);
  });
}

// The second of the three hard stops, and the one the other two rest on: an oversized donation is
// refused from Content-Length, before any byte of it is read.
test('an oversized donation is refused before anything is written', async () => {
  const { response, env } = await post(donation({ 'content-length': String(64 * 1024 * 1024) }));

  assert.equal(response.status, 413);
  assert.equal(env.DONATIONS.puts.length, 0);
});

test('a corpus may be far larger than an excerpt, and still not unbounded', async () => {
  const big = String(8 * 1024 * 1024);

  assert.equal((await post(donation({ 'd47-kind': 'corpus', 'content-length': big }))).response.status, 201);
  assert.equal((await post(donation({ 'd47-kind': 'excerpt', 'content-length': big }))).response.status, 413);
});

// There is nothing here to browse, and a GET that answered with anything friendly would be an
// invitation to find out what else it answers.
test('there is nothing here but the one path and the one method', async () => {
  const get = await worker.fetch(
    new Request('https://donate.invalid/donate'), { DONATIONS: bucket() });
  assert.equal(get.status, 405);

  const elsewhere = await post(
    new Request('https://donate.invalid/', { method: 'POST', body: 'x' }));
  assert.equal(elsewhere.response.status, 404);
});

// A build stamp comes off a stranger's binary. It becomes object metadata under a cap that fails
// the write rather than truncating, so it is bounded here — and a newline in it would be a
// request-splitting attempt wearing a version number's clothes.
test('header values are bounded and stripped before they become metadata', async () => {
  const { env } = await post(donation({ 'd47-build': 'x'.repeat(5000) }));

  assert.equal(env.DONATIONS.puts[0].options.customMetadata.build.length, 200);
});

// What went wrong at the store is between the Worker and its bucket. A stranger learning which is
// not part of donating.
test('a store failure says nothing about the store', async () => {
  const response = await worker.fetch(donation(), {
    DONATIONS: { put: async () => { throw new Error('bucket d47-donations is over quota'); } },
  });

  assert.equal(response.status, 503);
  assert.doesNotMatch(await response.text(), /quota|bucket|d47-donations/);
});
