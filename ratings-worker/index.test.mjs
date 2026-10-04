// Run with `node --test` from this folder. Node's built-in SQLite stands in for D1, so the
// Worker's SQL runs for real. Not part of `dotnet test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import worker from './src/index.js';

const ALICE = 'a'.repeat(32);
const BOB = 'b'.repeat(32);

/** A D1-shaped wrapper over in-memory SQLite with the real migration applied. */
function database() {
  const sqlite = new DatabaseSync(':memory:');
  sqlite.exec(readFileSync(new URL('./migrations/0001_votes.sql', import.meta.url), 'utf8'));

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
        for (const s of statements) {
          out.push(/^\s*SELECT/i.test(s.sql) ? await s.all() : await s.run());
        }
        sqlite.exec('COMMIT');
      } catch (error) {
        sqlite.exec('ROLLBACK');
        throw error;
      }
      return out;
    },
  };
}

function send(env, method, path, { headers = {}, body } = {}) {
  const init = { method, headers: { 'd47-format': '1', ...headers } };
  if (body !== undefined) {
    init.body = body;
    init.headers['content-length'] = String(Buffer.byteLength(body));
  }
  for (const [name, value] of Object.entries(init.headers)) {
    if (value === null) delete init.headers[name];
  }
  return worker.fetch(new Request(`https://ratings.invalid${path}`, init), env, { waitUntil() {} });
}

const put = (env, story, stars, voter = ALICE) =>
  send(env, 'PUT', `/ratings/${story}`, {
    headers: { 'd47-voter': voter },
    body: JSON.stringify({ stars }),
  });

const del = (env, story, voter = ALICE) =>
  send(env, 'DELETE', `/ratings/${story}`, { headers: { 'd47-voter': voter } });

const rows = (env) => env.DB.sqlite.prepare('SELECT * FROM votes').all();

test('AVoteIsStoredAndTheNewAggregateReturned', async () => {
  const env = { DB: database() };
  const response = await put(env, 'story-1', 4);

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { average: 4, count: 1 });
  assert.deepEqual(rows(env).map((r) => [r.story, r.voter, r.stars]), [['story-1', ALICE, 4]]);
});

test('TheListAveragesEveryStoryThatHasAVote', async () => {
  const env = { DB: database() };
  await put(env, 'story-1', 4, ALICE);
  await put(env, 'story-1', 5, BOB);
  await put(env, 'story-2', 2, ALICE);

  const response = await send(env, 'GET', '/ratings');

  assert.equal(response.status, 200);
  assert.equal(response.headers.get('cache-control'), 'public, max-age=300');
  assert.deepEqual(await response.json(), {
    format: 1,
    stories: { 'story-1': { average: 4.5, count: 2 }, 'story-2': { average: 2, count: 1 } },
  });
});

test('ASecondVoteFromOneVoterReplacesTheFirst', async () => {
  const env = { DB: database() };
  await put(env, 'story-1', 1);
  const response = await put(env, 'story-1', 5);

  assert.deepEqual(await response.json(), { average: 5, count: 1 });
  assert.equal(rows(env).length, 1);
});

test('DeletingAVoteLowersTheCount', async () => {
  const env = { DB: database() };
  await put(env, 'story-1', 4, ALICE);
  await put(env, 'story-1', 2, BOB);

  const response = await del(env, 'story-1', BOB);

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { average: 4, count: 1 });
});

test('DeletingAVoteThatDoesNotExistIsStillOk', async () => {
  const env = { DB: database() };
  const response = await del(env, 'story-1');

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { average: 0, count: 0 });
});

test('AnotherFormatIsRefused', async () => {
  const env = { DB: database() };

  assert.equal((await send(env, 'GET', '/ratings', { headers: { 'd47-format': '2' } })).status, 400);
  assert.equal((await send(env, 'GET', '/ratings', { headers: { 'd47-format': null } })).status, 400);
});

test('ABadStoryIdIsRefused', async () => {
  const env = { DB: database() };
  const headers = { 'd47-voter': ALICE };

  for (const id of ['a%20b', 'a%2Fb', 'x'.repeat(81), 'a..b']) {
    const response = await send(env, 'PUT', `/ratings/${id}`, { headers, body: '{"stars":3}' });
    assert.equal(response.status, 400, id);
  }
  assert.equal(rows(env).length, 0);
});

test('ABadVoterIsRefused', async () => {
  const env = { DB: database() };

  for (const voter of [null, 'A'.repeat(32), 'a'.repeat(31), 'a'.repeat(33), 'g'.repeat(32)]) {
    assert.equal((await put(env, 'story-1', 3, voter)).status, 400, String(voter));
  }
  assert.equal((await del(env, 'story-1', 'nope')).status, 400);
  assert.equal(rows(env).length, 0);
});

test('StarsOutsideOneToFiveOrNotIntegersAreRefused', async () => {
  const env = { DB: database() };

  for (const stars of [0, 6, 2.5, '3', null]) {
    assert.equal((await put(env, 'story-1', stars)).status, 400, String(stars));
  }

  const notJson = await send(env, 'PUT', '/ratings/story-1', { headers: { 'd47-voter': ALICE }, body: 'nope' });
  assert.equal(notJson.status, 400);
  assert.equal(rows(env).length, 0);
});

test('AnOversizeBodyIsRefusedBeforeItIsRead', async () => {
  const env = { DB: database() };
  const request = new Request('https://ratings.invalid/ratings/story-1', {
    method: 'PUT',
    headers: { 'd47-format': '1', 'd47-voter': ALICE, 'content-length': '65' },
    body: '{"stars":3}',
  });
  let read = false;
  request.text = async () => {
    read = true;
    return '';
  };

  const response = await worker.fetch(request, env, { waitUntil() {} });

  assert.equal(response.status, 413);
  assert.equal(read, false);
});

test('ANewStoryIsRefusedOnceAThousandHaveVotes', async () => {
  const env = { DB: database() };
  const insert = env.DB.sqlite.prepare('INSERT INTO votes VALUES (?, ?, 3, ?)');
  for (let i = 0; i < 1000; i++) insert.run(`story-${i}`, ALICE, 'now');

  assert.equal((await put(env, 'story-new', 3)).status, 409);
  assert.equal((await put(env, 'story-5', 4, BOB)).status, 200);
  assert.equal(rows(env).length, 1001);
});

test('OtherMethodsAndPathsAreRefused', async () => {
  const env = { DB: database() };

  assert.equal((await send(env, 'POST', '/ratings')).status, 405);
  assert.equal((await send(env, 'GET', '/ratings/story-1')).status, 405);
  assert.equal((await send(env, 'PUT', '/ratings')).status, 405);
  assert.equal((await send(env, 'GET', '/')).status, 404);
  assert.equal((await send(env, 'GET', '/ratings/a/b')).status, 404);
});
