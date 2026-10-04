// The story ratings endpoint (https://github.com/dseelinger/d47/issues/882).
//
// GET /ratings, PUT /ratings/<id>, DELETE /ratings/<id>. Every byte of a request is untrusted.
// The only data stored is story, voter, stars and at.

const FORMAT = '1';
const STORY = /^[A-Za-z0-9._-]{1,80}$/;
const VOTER = /^[0-9a-f]{32}$/;
const MOST_BODY_BYTES = 64;
const MOST_STORIES = 1000;
const CACHE_SECONDS = 300;

const AGGREGATE = 'SELECT AVG(stars) AS average, COUNT(*) AS count FROM votes WHERE story = ?';

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    const parts = url.pathname.split('/');
    const isList = url.pathname === '/ratings';
    const isStory = parts.length === 3 && parts[0] === '' && parts[1] === 'ratings';

    if (!isList && !isStory) {
      return said(404, 'Not found.');
    }

    const allowed = isList ? ['GET'] : ['PUT', 'DELETE'];
    if (!allowed.includes(request.method)) {
      return said(405, `This route takes ${allowed.join(' or ')}.`);
    }

    if (request.headers.get('d47-format') !== FORMAT) {
      return said(400, 'Unsupported d47-format.');
    }

    if (isList) {
      return list(request, env, ctx);
    }

    const story = parts[2];
    if (!STORY.test(story) || story.includes('..')) {
      return said(400, 'That is not a story id.');
    }

    const voter = request.headers.get('d47-voter');
    if (!VOTER.test(voter ?? '')) {
      return said(400, 'A vote needs a d47-voter of 32 lowercase hex digits.');
    }

    return request.method === 'PUT' ? vote(request, env, story, voter) : unvote(env, story, voter);
  },
};

async function list(request, env, ctx) {
  const cache = globalThis.caches?.default;
  const cached = cache ? await cache.match(request) : undefined;
  if (cached) return cached;

  const { results } = await env.DB
    .prepare('SELECT story, AVG(stars) AS average, COUNT(*) AS count FROM votes GROUP BY story')
    .all();

  const stories = {};
  for (const row of results) stories[row.story] = { average: row.average, count: row.count };

  const response = json({ format: Number(FORMAT), stories }, { 'cache-control': `public, max-age=${CACHE_SECONDS}` });
  if (cache) ctx?.waitUntil(cache.put(request, response.clone()));
  return response;
}

async function vote(request, env, story, voter) {
  const length = Number(request.headers.get('content-length'));
  if (!Number.isInteger(length) || length <= 0) {
    return said(411, 'A vote must declare its length.');
  }

  if (length > MOST_BODY_BYTES) {
    return said(413, 'That body is larger than a vote.');
  }

  let stars;
  try {
    stars = JSON.parse(await request.text())?.stars;
  } catch {
    return said(400, 'The body must be JSON.');
  }

  if (!Number.isInteger(stars) || stars < 1 || stars > 5) {
    return said(400, 'stars must be an integer from 1 to 5.');
  }

  const known = await env.DB.prepare('SELECT 1 AS known FROM votes WHERE story = ? LIMIT 1').bind(story).first();
  if (!known) {
    const { held } = await env.DB.prepare('SELECT COUNT(DISTINCT story) AS held FROM votes').first();
    if (held >= MOST_STORIES) {
      return said(409, 'This endpoint is not taking votes for new stories.');
    }
  }

  const [, aggregate] = await env.DB.batch([
    env.DB
      .prepare('INSERT OR REPLACE INTO votes (story, voter, stars, at) VALUES (?, ?, ?, ?)')
      .bind(story, voter, stars, new Date().toISOString()),
    env.DB.prepare(AGGREGATE).bind(story),
  ]);

  return json(summary(aggregate.results[0]));
}

async function unvote(env, story, voter) {
  const [, aggregate] = await env.DB.batch([
    env.DB.prepare('DELETE FROM votes WHERE story = ? AND voter = ?').bind(story, voter),
    env.DB.prepare(AGGREGATE).bind(story),
  ]);

  return json(summary(aggregate.results[0]));
}

function summary(row) {
  return { average: row.average ?? 0, count: row.count };
}

function json(body, headers = {}) {
  return new Response(JSON.stringify(body), { headers: { 'content-type': 'application/json', ...headers } });
}

function said(status, text) {
  return new Response(text + '\n', { status, headers: { 'content-type': 'text/plain' } });
}
