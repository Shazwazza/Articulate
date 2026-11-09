#!/usr/bin/env node
//
// Shared preparation for native and Docker test sites; behaviour tests live in Client/e2e.
//
// Modes:
//   publish   Full publish root + descendants via Management API (default; --no-descendants skips children)
//   confirm   Read-only: verify root + children are published and / returns 200
//   smoke     Wait for the already-running Production host to return 200
//   index-ready  Candidate-only: publish/index/delete a temporary post before E2E
//
// Env:
//   UMBRACO_PUBLIC_URL          default: https://localhost:18443
//   ARTICULATE_TEST_SITE_CLIENT_SECRET  default: articulate-test-site-secret (matches docker-compose)
//   TIMEOUT_SECONDS             default: 300
//
// Examples:
//   node docker/smoke.mjs publish
//   node docker/smoke.mjs confirm
//   node docker/smoke.mjs smoke

import https from 'node:https';
import http from 'node:http';
import { randomUUID } from 'node:crypto';

// --- helpers ----------------------------------------------------------------

function die(msg) {
  console.error(msg);
  process.exit(1);
}

function env(name, fallback) {
  return process.env[name] ?? fallback;
}

function now() {
  return Math.floor(Date.now() / 1000);
}

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

// --- HTTP transport ---------------------------------------------------------

// Native test hosts and Caddy use local certificates that Node does not trust.
// Skip certificate validation for loopback and private-network test sites.
// Public hosts keep strict validation.
function isDevHost(host) {
  if (['localhost', '127.0.0.1', '::1', '[::1]'].includes(host)) return true;
  // Private IPv4 ranges: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16.
  const m = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.exec(host);
  if (!m) return false;
  const [a, b] = [Number(m[1]), Number(m[2])];
  return a === 10 || (a === 172 && b >= 16 && b <= 31) || (a === 192 && b === 168);
}

function request(url, opts = {}) {
  return new Promise((resolve, reject) => {
    const u = new URL(url);
    const mod = u.protocol === 'https:' ? https : http;
    const req = mod.request({
      hostname: u.hostname,
      port: u.port || (u.protocol === 'https:' ? 443 : 80),
      path: u.pathname + u.search,
      method: opts.method || 'GET',
      headers: opts.headers || {},
      rejectUnauthorized: isDevHost(u.hostname) ? false : true,
      timeout: opts.timeout || 30_000,
    }, res => {
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => {
        const body = Buffer.concat(chunks).toString('utf-8');
        resolve({ status: res.statusCode, headers: res.headers, body });
      });
    });
    req.on('error', reject);
    req.on('timeout', () => { req.destroy(); reject(new Error('request timeout')); });
    if (opts.body) req.write(opts.body);
    req.end();
  });
}

async function jsonGet(url, token) {
  const res = await request(url, {
    headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' },
  });
  return parseJsonResponse(res, url);
}

async function jsonPut(url, token, body) {
  const res = await request(url, {
    method: 'PUT',
    headers: { Authorization: `Bearer ${token}`, Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  return parseJsonResponse(res, url);
}

async function post(url, body) {
  const res = await request(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams(body).toString(),
  });
  return parseJsonResponse(res, url);
}

function parseJsonResponse(res, label) {
  if (res.status < 200 || res.status >= 300) {
    throw new Error(`${label} returned HTTP ${res.status}: ${res.body.slice(0, 240)}`);
  }

  if (res.status === 204 || !res.body) return null;

  try {
    return JSON.parse(res.body);
  } catch (err) {
    throw new Error(`${label} did not return valid JSON: ${err.message}`);
  }
}

// --- retry helpers ----------------------------------------------------------

async function retry(fn, deadline, label) {
  let last = '';
  while (now() < deadline) {
    try {
      const result = await fn();
      if (result !== undefined) return result;
    } catch (err) { last = err.message; }
    await sleep(2000);
  }
  die(`Timed out: ${label}${last ? ` (last error: ${last})` : ''}`);
}

async function poll(fn, deadline, label, last = () => '') {
  while (now() < deadline) {
    if (await fn()) return;
    await sleep(2000);
  }
  die(`Timed out: ${label}${last() ? ` (last: ${last()})` : ''}`);
}

// --- Token / Management API operations --------------------------------------

async function requestToken(base, clientId, clientSecret, timeoutSec) {
  const deadline = now() + timeoutSec;
  const tokenResp = await retry(
    () => post(`${base}/umbraco/management/api/v1/security/back-office/token`, {
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
    }),
    deadline,
    'token endpoint'
  );
  if (!tokenResp.access_token) die('Token endpoint did not return an access token.');
  return tokenResp.access_token;
}

async function findArticulateRoot(base, token) {
  const data = await jsonGet(`${base}/umbraco/management/api/v1/tree/document/root?skip=0&take=100`, token);
  const items = data?.items ?? [];
  const articulate = items.find(i => i.documentType?.alias === 'Articulate') ?? items[0];
  if (!articulate?.id) die('Could not find an Articulate root document in the document tree.');
  return articulate.id;
}

async function publishRoot(base, token, rootId) {
  console.log('Publishing root');
  await jsonPut(`${base}/umbraco/management/api/v1/document/${rootId}/publish`, token, {
    publishSchedules: [{ culture: null, schedule: null }],
  });
}

async function reloadCache(base, token) {
  const url = `${base}/umbraco/management/api/v1/published-cache/reload`;
  const res = await request(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' },
  });
  if (res.status < 200 || res.status >= 300) {
    throw new Error(`${url} returned HTTP ${res.status}: ${res.body.slice(0, 240)}`);
  }
}

async function publishWithDescendants(base, token, id, label, timeoutSec) {
  console.log(`Publishing with descendants: ${label} (${id})`);
  const result = await jsonPut(`${base}/umbraco/management/api/v1/document/${id}/publish-with-descendants`, token, {
    cultures: ['invariant'],
    includeUnpublishedDescendants: true,
  });
  if (result?.taskId) {
    const taskUrl = `${base}/umbraco/management/api/v1/document/${id}/publish-with-descendants/result/${result.taskId}`;
    await poll(async () => {
      const status = await jsonGet(taskUrl, token);
      return status?.isComplete === true;
    }, now() + timeoutSec, `publish-with-descendants task ${result.taskId} for ${label}`);
  }
}

async function waitForRoot(base, timeoutSec) {
  let last = '';
  await poll(async () => {
    try {
      const res = await request(`${base}/`, { timeout: 15_000 });
      last = `HTTP ${res.status}: ${res.body.slice(0, 120)}`;
      return res.status === 200;
    } catch (err) { last = err.message; return false; }
  }, now() + timeoutSec, `/ to return 200`, () => last);
}

async function confirmChildren(base, token, parentId, indent = '') {
  let unpublished = 0;
  let skip = 0;
  while (true) {
    const data = await jsonGet(`${base}/umbraco/management/api/v1/tree/document/children?parentId=${parentId}&skip=${skip}&take=100`, token);
    const items = data?.items ?? [];
    for (const item of items) {
      const variant = item.variants?.[0] ?? {};
      const state = variant.state ?? 'unknown';
      const name = variant.name || item.name || item.id;
      const tag = item.hasChildren ? ' (has children)' : '';
      console.log(`${indent}Child '${name}' (${item.id}) state=${state}${tag}`);
      if (state !== 'Published') unpublished++;
      if (item.hasChildren)
        unpublished += await confirmChildren(base, token, item.id, `${indent}  `);
    }
    if (items.length < 100) break;
    skip += items.length;
  }
  return unpublished;
}

async function waitForIndexing(base, token, rootId, timeoutSec) {
  const deadline = now() + timeoutSec;
  const api = `${base}/umbraco/management/api/v1`;
  const headers = { Authorization: `Bearer ${token}`, Accept: 'application/json', 'Content-Type': 'application/json' };
  const children = await jsonGet(`${api}/tree/document/children?parentId=${rootId}&skip=0&take=100`, token);
  let archiveId;
  for (const child of children.items ?? []) {
    const type = await jsonGet(`${api}/document-type/${child.documentType.id}`, token);
    if (type.alias === 'ArticulateArchive') archiveId = child.id;
  }
  if (!archiveId) throw new Error('Index readiness requires the Articles archive.');

  const marker = `ready${randomUUID().replaceAll('-', '')}`;
  const created = await request(`${api}/document`, {
    method: 'POST', headers,
    body: JSON.stringify({
      parent: { id: archiveId },
      documentType: { id: '9c2df3ea-74d9-41ef-8773-07960ac5a819' },
      template: null,
      variants: [{ culture: null, segment: null, name: `Index readiness ${marker}` }],
      values: [
        { alias: 'markdown', value: marker, culture: null, segment: null, editorAlias: 'Umbraco.MarkdownEditor', entityType: 'document-property-value' },
        { alias: 'umbracoUrlName', value: marker, culture: null, segment: null, editorAlias: 'Umbraco.TextBox', entityType: 'document-property-value' },
      ],
    }),
  });
  if (created.status !== 201) throw new Error(`Index readiness creation returned HTTP ${created.status}.`);
  const id = new URL(created.headers.location, base).pathname.split('/').at(-1);
  if (!/^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/i.test(id ?? ''))
    throw new Error('Index readiness creation did not return a document ID.');

  let original;
  try {
    await jsonPut(`${api}/document/${id}/publish`, token, { publishSchedules: [{ culture: null, schedule: null }] });
    const query = encodeURIComponent(`__Key:"${id}"`);
    // Articulate searches ExternalIndex; the CMS document-search helper checks InternalIndex.
    let hits = 0, indexed = false;
    while (!indexed && now() < deadline) {
      const result = await jsonGet(`${api}/searcher/ExternalIndex/query?term=${query}`, token);
      hits = result.items?.length ?? 0;
      indexed = result.items?.some(item => item.fields?.some(field => field.name === '__Key' && field.values?.includes(id))) ?? false;
      if (!indexed) await sleep(2000);
    }
    if (!indexed) throw new Error(`Timed out waiting for publication indexing before E2E (last search returned ${hits} items, none with __Key ${id}).`);
    console.log('Index readiness passed: CMS ExternalIndex contains the owned publication.');
  } catch (err) { original = err; }

  try {
    const deleted = await request(`${api}/document/${id}`, { method: 'DELETE', headers });
    if (![200, 204].includes(deleted.status)) throw new Error(`Index readiness cleanup returned HTTP ${deleted.status}.`);
    const absent = await request(`${api}/document/${id}`, { headers });
    if (absent.status !== 404) throw new Error('Index readiness post was not removed.');
    console.log('Index readiness post cleanup verified.');
  } catch (err) {
    if (!original) throw err;
    console.error(`Cleanup error: ${err.message}`);
  }
  if (original) throw original;
}

// --- main -------------------------------------------------------------------

async function main() {
  const validModes = ['publish', 'confirm', 'smoke', 'index-ready'];
  const mode = process.argv[2] || 'publish';
  if (!validModes.includes(mode)) {
    die(`Usage: smoke.mjs <publish|confirm|smoke|index-ready> [--no-descendants]`);
  }

  const noDescendants = process.argv.includes('--no-descendants');
  const base = env('UMBRACO_PUBLIC_URL', 'https://localhost:18443').replace(/\/+$/, '');
  const timeoutSec = parseInt(env('TIMEOUT_SECONDS', '300'), 10);
  if (!Number.isInteger(timeoutSec) || timeoutSec <= 0) {
    die('TIMEOUT_SECONDS must be a positive integer.');
  }

  if (mode === 'index-ready') {
    const project = env('ARTICULATE_E2E_CANDIDATE_PROJECT', env('COMPOSE_PROJECT_NAME', ''));
    const target = new URL(base);
    if (!/^art_e2e_(?:candidate|native)_(v17|v18)_[a-f0-9]{32}$/.test(project)
      || target.protocol !== 'https:' || target.hostname !== 'localhost'
      || !target.port || ['18443', '18444', '19443', '19444'].includes(target.port))
      throw new Error('Index readiness is restricted to disposable localhost E2E candidates.');
  }

  // --- smoke mode (no auth needed) ------------------------------------------
  if (mode === 'smoke') {
    console.log('Waiting for production root to return 200');
    await waitForRoot(base, timeoutSec);
    console.log('Production smoke passed: / returned 200.');
    return;
  }

  // --- confirm / publish: shared setup (token + root) -----------------------
  const clientId = 'articulate-test-site';
  const clientSecret = env('ARTICULATE_TEST_SITE_CLIENT_SECRET', 'articulate-test-site-secret');

  console.log('Requesting access token');
  const token = await requestToken(base, clientId, clientSecret, timeoutSec);

  console.log('Finding Articulate root');
  const rootId = await findArticulateRoot(base, token);
  console.log(`Root id: ${rootId}`);

  if (mode === 'index-ready') {
    await waitForIndexing(base, token, rootId, timeoutSec);
    return;
  }

  // --- confirm mode ---------------------------------------------------------
  if (mode === 'confirm') {
    console.log('Confirming published children and descendants');
    const missing = await confirmChildren(base, token, rootId);
    if (missing !== 0) die('One or more Articulate children are not published.');

    console.log('Verifying public root');
    await waitForRoot(base, timeoutSec);

    console.log('Confirmation passed');
    console.log('Test-site confirmation passed: root, children, and descendants are published and / returns 200.');
    return;
  }

  // --- publish mode ---------------------------------------------------------
  if (noDescendants) {
    console.log('Publishing root only');
    await publishRoot(base, token, rootId);
  } else {
    console.log('Publishing root before descendants');
    await publishRoot(base, token, rootId);

    console.log('Waiting for root cache');
    await reloadCache(base, token);
    await waitForRoot(base, timeoutSec);

    console.log('Publishing root children with descendants');
    const data = await jsonGet(`${base}/umbraco/management/api/v1/tree/document/children?parentId=${rootId}&skip=0&take=100`, token);
    const children = (data?.items ?? []).filter(c => c.hasChildren || c.variants?.[0]?.state !== 'Published');
    for (const child of children) {
      const name = child.variants?.[0]?.name ?? child.name ?? child.id;
      await publishWithDescendants(base, token, child.id, name, timeoutSec);
    }

    console.log('Publishing root with descendants');
    await publishWithDescendants(base, token, rootId, 'root', timeoutSec);
  }

  console.log('Reloading published cache');
  await reloadCache(base, token);

  console.log('Waiting for public root');
  await waitForRoot(base, timeoutSec);

  console.log('Root is live');
}

main().catch(err => {
  console.error(err.message || err);
  process.exit(1);
});
