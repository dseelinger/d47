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

/**
 * A bucket that records what it was asked to store, and can be asserted to have been asked
 * nothing. It also holds what it was given, so an erasure can be driven against objects that
 * actually exist rather than against an empty store that would pass either way.
 */
function bucket(held = []) {
  const puts = [];
  const objects = new Map(held.map((key) => [key, true]));

  return {
    puts,
    objects,
    put: async (key, _body, options) => {
      objects.set(key, true);
      puts.push({ key, options });
    },

    // R2's own shape: a page of objects and a truncated flag saying there is more behind it.
    // No cursor, because the Worker deliberately does not use one — it re-lists what is left
    // after each delete rather than paging through a listing it is invalidating as it goes.
    list: async ({ prefix, limit }) => {
      const matching = [...objects.keys()].filter((key) => key.startsWith(prefix)).sort();
      const page = matching.slice(0, limit);

      return { objects: page.map((key) => ({ key })), truncated: page.length < matching.length };
    },

    delete: async (keys) => {
      for (const key of [].concat(keys)) objects.delete(key);
    },
  };
}

/** An erasure, with any header overridable and any of them removable by passing null. */
function erasure(overrides = {}) {
  const headers = { 'd47-format': '1', 'd47-donor': TOKEN, ...overrides };

  for (const [name, value] of Object.entries(headers)) {
    if (value === null) delete headers[name];
  }

  return new Request('https://donate.invalid/forget', { method: 'POST', headers });
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
test('there are two paths and one method, and nothing else answers', async () => {
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

// **Erasure on request** (https://github.com/dseelinger/d47/issues/167). The half of the design
// that lets the other half exist: GitHub was ruled out as a destination because a public
// transport cannot honour "ask and it is deleted", so a store that could not honour it either
// would have moved the problem rather than solved it.

test('forgetting an installation deletes everything it ever sent, and nothing else', async () => {
  const other = 'fedcba9876543210fedcba9876543210';

  const env = {
    DONATIONS: bucket([
      `excerpts/${TOKEN}/20260829T142530Z-aaaa.md.gz`,
      `excerpts/${TOKEN}/20260830T090000Z-bbbb.md.gz`,
      `corpus/${TOKEN}/20260829T142530Z-cccc.jsonl.gz`,
      `corpus/${other}/20260829T142530Z-dddd.jsonl.gz`,
    ]),
  };

  const response = await worker.fetch(erasure(), env);
  const said = await response.json();

  assert.equal(response.status, 200);
  assert.equal(said.deleted, 3);
  assert.equal(said.more, false);

  // Both prefixes, because a donor asking to be forgotten means both retention classes — the
  // permanent one included, which is the only way "kept for ever" stays honest.
  assert.deepEqual([...env.DONATIONS.objects.keys()], [`corpus/${other}/20260829T142530Z-dddd.jsonl.gz`]);
});

test('the keys it deleted are named, so a receipt can say what went', async () => {
  const key = `corpus/${TOKEN}/20260829T142530Z-cccc.jsonl.gz`;
  const { keys } = await (await worker.fetch(erasure(), { DONATIONS: bucket([key]) })).json();

  assert.deepEqual(keys, [key]);
});

// An unknown token answers nothing-to-do rather than a refusal: there is nothing to hide from
// somebody already holding the only handle to it, and a different answer for a token that exists
// would make this a way of testing whether one does.
test('an identifier that donated nothing is forgotten just the same', async () => {
  const env = { DONATIONS: bucket([`corpus/fedcba9876543210fedcba9876543210/x.jsonl.gz`]) };
  const said = await (await worker.fetch(erasure(), env)).json();

  assert.equal(said.deleted, 0);
  assert.equal(env.DONATIONS.objects.size, 1);
});

for (const [what, overrides] of [
  ['an unversioned erasure', { 'd47-format': null }],
  ['a format from the future', { 'd47-format': '2' }],
  ['no identifier at all', { 'd47-donor': null }],
  ['an identifier that is a path', { 'd47-donor': '../../corpus' }],
  ['an identifier of the wrong length', { 'd47-donor': 'abc' }],
]) {
  test(`${what} is refused, and nothing is deleted`, async () => {
    const key = `corpus/${TOKEN}/20260829T142530Z-cccc.jsonl.gz`;
    const env = { DONATIONS: bucket([key]) };
    const response = await worker.fetch(erasure(overrides), env);

    assert.ok(response.status >= 400, `expected a refusal, got ${response.status}`);
    assert.equal(env.DONATIONS.objects.size, 1);
  });
}

// A ceiling rather than an assumption. A donor who somehow passed it is told there is more, rather
// than being quietly left with half a deletion.
test('past the ceiling it says there is more rather than stopping quietly', async () => {
  const many = Array.from(
    { length: 10_001 },
    (_, nth) => `corpus/${TOKEN}/${String(nth).padStart(6, '0')}.jsonl.gz`);

  const env = { DONATIONS: bucket(many) };
  const said = await (await worker.fetch(erasure(), env)).json();

  assert.equal(said.more, true);
  assert.equal(said.deleted, 10_000);
  assert.equal(env.DONATIONS.objects.size, 1);
});

// The same rule a failed write follows: what went wrong at the store is between the Worker and its
// bucket, and a donor needs to know only that nothing is confirmed gone.
test('a store that will not answer does not report a deletion', async () => {
  const response = await worker.fetch(erasure(), {
    DONATIONS: { list: async () => { throw new Error('bucket d47-donations is unreachable'); } },
  });

  assert.equal(response.status, 503);
  assert.doesNotMatch(await response.text(), /bucket|d47-donations|unreachable/);
});

// And the donation road is untouched by any of it: /forget is a second path, not a mode.
test('a donation still lands while erasure exists beside it', async () => {
  assert.equal((await post(donation())).response.status, 201);

  const elsewhere = await worker.fetch(
    new Request('https://donate.invalid/forgetting', { method: 'POST', headers: { 'd47-format': '1' } }),
    { DONATIONS: bucket() });

  assert.equal(elsewhere.status, 404);
});
