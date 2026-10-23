// The usage-count endpoint (https://github.com/dseelinger/d47/issues/1554, part of #1523).
//
// POST /usage only. Every byte of a request is untrusted. The only data stored is day, tool,
// caller and counts; no address, header or arrival time is kept.

const FORMAT = '1';
const MOST_BODY_BYTES = 32768;
const MOST_TOOLS = 1000;
const MOST_TOOL_LENGTH = 64;
const MOST_COUNT = 10000;
const DAYS_BACK = 30;
const DAYS_AHEAD = 1;
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const TOOL = /^[a-z0-9]+(_[a-z0-9]+)*$/;
const CALLERS = ['commander', 'model'];
const DAY_MS = 86_400_000;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname !== '/usage') {
      return said(404, 'Not found.');
    }

    if (request.method !== 'POST') {
      return said(405, 'This route takes POST.');
    }

    if (request.headers.get('d47-format') !== FORMAT) {
      return said(400, 'Unsupported d47-format.');
    }

    const length = Number(request.headers.get('content-length'));
    if (request.headers.get('content-length') === null || !Number.isInteger(length) || length <= 0) {
      return said(411, 'A report must declare its length.');
    }

    if (length > MOST_BODY_BYTES) {
      return said(413, 'That body is larger than a report.');
    }

    let body;
    try {
      body = JSON.parse(await request.text());
    } catch {
      return said(400, 'The body must be JSON.');
    }

    const problem = check(body, Date.now());
    if (problem) {
      return said(400, problem);
    }

    const names = [...new Set(body.uses.map((use) => use.tool))];
    const marks = names.map(() => '?').join(', ');
    const { known } = await env.DB
      .prepare(`SELECT COUNT(DISTINCT tool) AS known FROM counts WHERE tool IN (${marks})`)
      .bind(...names)
      .first();
    const { held } = await env.DB.prepare('SELECT COUNT(DISTINCT tool) AS held FROM counts').first();
    if (held + (names.length - known) > MOST_TOOLS) {
      return said(409, 'This endpoint is not taking new tool names.');
    }

    const addCount = env.DB.prepare(
      'INSERT INTO counts (day, tool, caller, uses) VALUES (?, ?, ?, ?) ' +
        'ON CONFLICT (day, tool, caller) DO UPDATE SET uses = uses + excluded.uses');
    const addReport = env.DB.prepare(
      'INSERT INTO reports (day, reports) VALUES (?, 1) ON CONFLICT (day) DO UPDATE SET reports = reports + 1');

    await env.DB.batch([
      ...body.uses.map((use) => addCount.bind(body.day, use.tool, use.caller, use.count)),
      addReport.bind(body.day),
    ]);

    return new Response(null, { status: 204 });
  },
};

/** Returns a refusal message, or null when the report is well formed. */
function check(body, now) {
  if (!isObject(body) || !sameKeys(body, ['day', 'uses'])) {
    return 'The body must be {"day","uses"}.';
  }

  if (!validDay(body.day, now)) {
    return `day must be YYYY-MM-DD, at most ${DAYS_BACK} days back and ${DAYS_AHEAD} ahead (UTC).`;
  }

  if (!Array.isArray(body.uses) || body.uses.length === 0) {
    return 'uses must be a list with at least one entry.';
  }

  const seen = new Set();
  for (const use of body.uses) {
    if (!isObject(use) || !sameKeys(use, ['tool', 'caller', 'count'])) {
      return 'Each use must be {"tool","caller","count"}.';
    }

    if (typeof use.tool !== 'string' || use.tool.length > MOST_TOOL_LENGTH || !TOOL.test(use.tool)) {
      return 'That is not a tool name.';
    }

    if (!CALLERS.includes(use.caller)) {
      return 'caller must be commander or model.';
    }

    if (!Number.isInteger(use.count) || use.count < 1 || use.count > MOST_COUNT) {
      return `count must be an integer from 1 to ${MOST_COUNT}.`;
    }

    const key = `${use.tool}\n${use.caller}`;
    if (seen.has(key)) {
      return 'A tool and caller may appear once per report.';
    }
    seen.add(key);
  }

  return null;
}

function validDay(day, now) {
  if (typeof day !== 'string' || !DAY.test(day)) return false;

  const at = Date.parse(`${day}T00:00:00Z`);
  if (Number.isNaN(at) || new Date(at).toISOString().slice(0, 10) !== day) return false;

  const today = Math.floor(now / DAY_MS) * DAY_MS;
  return at <= today + DAYS_AHEAD * DAY_MS && at >= today - DAYS_BACK * DAY_MS;
}

function isObject(value) {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function sameKeys(value, keys) {
  const own = Object.keys(value);
  return own.length === keys.length && keys.every((key) => own.includes(key));
}

function said(status, text) {
  return new Response(text + '\n', { status, headers: { 'content-type': 'text/plain' } });
}
