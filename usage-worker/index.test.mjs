// Run with `node --test` from this folder. Node's built-in SQLite stands in for D1, so the
// Worker's SQL runs for real. Not part of `dotnet test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import worker from './src/index.js';

const DAY_MS = 86_400_000;

/** A day as YYYY-MM-DD, offset from today UTC. */
const day = (offset = 0) => new Date(Date.now() + offset * DAY_MS).toISOString().slice(0, 10);

/** A D1-shaped wrapper over in-memory SQLite with the real migration applied. */
function database() {
  const sqlite = new DatabaseSync(':memory:');
  sqlite.exec(readFileSync(new URL('./migrations/0001_usage.sql', import.meta.url), 'utf8'));

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
  return worker.fetch(new Request(`https://usage.invalid${path}`, init), env);
}

const report = (env, body, headers) =>
  send(env, 'POST', '/usage', { headers, body: typeof body === 'string' ? body : JSON.stringify(body) });

const use = (tool, caller = 'commander', count = 1) => ({ tool, caller, count });

const counts = (env) =>
  env.DB.sqlite.prepare('SELECT day, tool, caller, uses FROM counts ORDER BY day, tool, caller').all()
    .map((r) => ({ ...r }));
const reports = (env) => env.DB.sqlite.prepare('SELECT day, reports FROM reports').all().map((r) => ({ ...r }));

async function refused(env, body, status = 400, headers) {
  const response = await report(env, body, headers);
  assert.equal(response.status, status, JSON.stringify(body));
  assert.deepEqual(counts(env), []);
  assert.deepEqual(reports(env), []);
}

test('AValidReportAddsEachCountAndOneReport', async () => {
  const env = { DB: database() };
  const response = await report(env, {
    day: day(),
    uses: [use('plot_route', 'commander', 3), use('plot_route', 'model', 2), use('get_data_egress')],
  });

  assert.equal(response.status, 204);
  assert.deepEqual(counts(env), [
    { day: day(), tool: 'get_data_egress', caller: 'commander', uses: 1 },
    { day: day(), tool: 'plot_route', caller: 'commander', uses: 3 },
    { day: day(), tool: 'plot_route', caller: 'model', uses: 2 },
  ]);
  assert.deepEqual(reports(env), [{ day: day(), reports: 1 }]);
});

test('ASecondReportForTheSameDayAddsToTheFirst', async () => {
  const env = { DB: database() };
  await report(env, { day: day(), uses: [use('plot_route', 'commander', 3)] });
  await report(env, { day: day(), uses: [use('plot_route', 'commander', 4), use('other_tool')] });

  assert.deepEqual(counts(env), [
    { day: day(), tool: 'other_tool', caller: 'commander', uses: 1 },
    { day: day(), tool: 'plot_route', caller: 'commander', uses: 7 },
  ]);
  assert.deepEqual(reports(env), [{ day: day(), reports: 2 }]);
});

test('ReportsForDifferentDaysAreKeptApart', async () => {
  const env = { DB: database() };
  await report(env, { day: day(-1), uses: [use('plot_route')] });
  await report(env, { day: day(), uses: [use('plot_route')] });

  assert.equal(counts(env).length, 2);
  assert.equal(reports(env).length, 2);
});

test('TheBoundaryValuesAreAccepted', async () => {
  const env = { DB: database() };

  assert.equal((await report(env, { day: day(1), uses: [use('plot_route')] })).status, 204);
  assert.equal((await report(env, { day: day(-30), uses: [use('plot_route')] })).status, 204);
  assert.equal((await report(env, { day: day(), uses: [use('plot_route', 'commander', 10000)] })).status, 204);
});

test('AnotherMethodOrPathIsRefused', async () => {
  const env = { DB: database() };

  assert.equal((await send(env, 'GET', '/usage')).status, 405);
  assert.equal((await send(env, 'PUT', '/usage')).status, 405);
  assert.equal((await send(env, 'DELETE', '/usage')).status, 405);
  assert.equal((await send(env, 'POST', '/')).status, 404);
  assert.equal((await send(env, 'POST', '/usage/x')).status, 404);
  assert.equal((await send(env, 'GET', '/ratings')).status, 404);
});

test('AnotherFormatIsRefused', async () => {
  const env = { DB: database() };
  const body = { day: day(), uses: [use('plot_route')] };

  await refused(env, body, 400, { 'd47-format': '2' });
  await refused(env, body, 400, { 'd47-format': null });
});

test('AMissingLengthIsRefused', async () => {
  const env = { DB: database() };
  const request = new Request('https://usage.invalid/usage', {
    method: 'POST',
    headers: { 'd47-format': '1' },
    body: JSON.stringify({ day: day(), uses: [use('plot_route')] }),
  });
  request.headers.delete('content-length');

  assert.equal((await worker.fetch(request, env)).status, 411);
  assert.deepEqual(counts(env), []);
});

test('AnOversizeBodyIsRefusedBeforeItIsRead', async () => {
  const env = { DB: database() };
  const request = new Request('https://usage.invalid/usage', {
    method: 'POST',
    headers: { 'd47-format': '1', 'content-length': '32769' },
    body: '{}',
  });
  let read = false;
  request.text = async () => {
    read = true;
    return '';
  };

  const response = await worker.fetch(request, env);

  assert.equal(response.status, 413);
  assert.equal(read, false);
});

test('ABodyOfExactlyTheLimitIsRead', async () => {
  const env = { DB: database() };
  const request = new Request('https://usage.invalid/usage', {
    method: 'POST',
    headers: { 'd47-format': '1', 'content-length': '32768' },
    body: '{}',
  });

  assert.equal((await worker.fetch(request, env)).status, 400);
});

test('ABodyThatIsNotTheShapeIsRefused', async () => {
  const env = { DB: database() };
  const good = [use('plot_route')];

  for (const body of [
    'nope',
    'null',
    '[]',
    '"x"',
    {},
    { day: day() },
    { uses: good },
    { day: day(), uses: 'x' },
    { day: day(), uses: [] },
    { day: day(), uses: [1] },
    { day: day(), uses: [null] },
    { day: day(), uses: good, extra: 1 },
    { day: day(), uses: [{ tool: 'plot_route', caller: 'commander' }] },
    { day: day(), uses: [{ tool: 'plot_route', caller: 'commander', count: 1, extra: 1 }] },
  ]) {
    await refused(env, body);
  }
});

test('ABadDayIsRefused', async () => {
  const env = { DB: database() };
  const uses = [use('plot_route')];

  for (const bad of [
    day(2),
    day(-31),
    '2026-13-01',
    '2026-02-30',
    '2026-1-01',
    '20261022',
    '2026-10-22T00:00:00Z',
    '',
    20261022,
    null,
  ]) {
    await refused(env, { day: bad, uses });
  }
});

test('ABadToolNameIsRefused', async () => {
  const env = { DB: database() };

  for (const tool of ['Plot_Route', 'plot-route', '_plot', 'plot_', 'plot__route', 'plot route', '', 'a'.repeat(65), 7, null]) {
    await refused(env, { day: day(), uses: [use(tool)] });
  }
  assert.equal((await report(env, { day: day(), uses: [use('a'.repeat(64))] })).status, 204);
});

test('ACallerOtherThanCommanderOrModelIsRefused', async () => {
  const env = { DB: database() };

  for (const caller of ['Commander', 'system', '', 1, null]) {
    await refused(env, { day: day(), uses: [use('plot_route', caller)] });
  }
});

test('ACountOutsideOneToTenThousandIsRefused', async () => {
  const env = { DB: database() };

  for (const count of [0, -1, 10001, 1.5, '3', null]) {
    await refused(env, { day: day(), uses: [use('plot_route', 'commander', count)] });
  }
});

test('TheSameToolAndCallerTwiceInOneReportIsRefused', async () => {
  const env = { DB: database() };

  await refused(env, { day: day(), uses: [use('plot_route', 'commander', 1), use('plot_route', 'commander', 2)] });
  assert.equal(
    (await report(env, { day: day(), uses: [use('plot_route', 'commander'), use('plot_route', 'model')] })).status,
    204);
});

test('ANewToolNameIsRefusedOnceAThousandAreHeld', async () => {
  const env = { DB: database() };
  const insert = env.DB.sqlite.prepare('INSERT INTO counts VALUES (?, ?, ?, 1)');
  for (let i = 0; i < 1000; i++) insert.run(day(), `tool_${i}`, 'commander');

  assert.equal((await report(env, { day: day(), uses: [use('brand_new')] })).status, 409);
  assert.equal((await report(env, { day: day(), uses: [use('tool_5'), use('brand_new')] })).status, 409);
  assert.equal((await report(env, { day: day(), uses: [use('tool_5', 'model', 2)] })).status, 204);
  assert.equal(counts(env).length, 1001);
  assert.deepEqual(reports(env), [{ day: day(), reports: 1 }]);
});
