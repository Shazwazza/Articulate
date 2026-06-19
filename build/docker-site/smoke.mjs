#!/usr/bin/env node
//
// smoke.mjs — Umbraco dev automation smoke-test / publish / confirm / theme
//
// Modes:
//   publish   Full publish root + descendants via Management API (default; --no-descendants skips children)
//   confirm   Read-only: verify root + children are published and / returns 200
//   smoke     Wait for production root to return 200 (docker compose must already be running)
//   theme     Read current theme, change to a different theme, publish, verify theme CSS in HTML
//
// Env:
//   UMBRACO_PUBLIC_URL          default: https://localhost:18443
//   ARTICULATE_DEV_AUTOMATION_CLIENT_ID    default: articulate-dev-automation
//   ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET  required
//   TIMEOUT_SECONDS             default: 300
//
// Examples:
//   node build/docker-site/smoke.mjs publish
//   node build/docker-site/smoke.mjs confirm
//   node build/docker-site/smoke.mjs smoke
//   node build/docker-site/smoke.mjs theme

import https from 'node:https';
import http from 'node:http';

// --- helpers ----------------------------------------------------------------

function die(msg) {
  console.error(msg);
  process.exit(1);
}

function env(name, fallback) {
  return process.env[name] ?? fallback;
}

function requiredEnv(name) {
  const v = process.env[name];
  if (!v) die(`${name} must be set.`);
  return v;
}

function now() {
  return Math.floor(Date.now() / 1000);
}

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

// --- HTTP transport ---------------------------------------------------------

function isLocalhost(host) {
  return ['localhost', '127.0.0.1', '::1', '[::1]'].includes(host);
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
      rejectUnauthorized: isLocalhost(u.hostname) ? false : true,
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
  while (now() < deadline) {
    try {
      const result = await fn();
      if (result !== undefined) return result;
    } catch { /* retry */ }
    await sleep(2000);
  }
  die(`Timed out: ${label}`);
}

async function poll(fn, deadline, label) {
  while (now() < deadline) {
    if (await fn()) return;
    await sleep(2000);
  }
  die(`Timed out: ${label}`);
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

async function getDocument(base, token, id) {
  return jsonGet(`${base}/umbraco/management/api/v1/document/${id}`, token);
}

async function updateDocument(base, token, id, values, variantName) {
  // Read-modify-write: Management API PUT /document replaces the values
  // collection, so a partial payload would wipe required properties
  // (blogTitle, pageSize, ...) and cause publish to 400.
  const current = await getDocument(base, token, id);
  const merged = new Map((current.values ?? []).map(v => [v.alias, v]));
  for (const change of values) merged.set(change.alias, change);
  await jsonPut(`${base}/umbraco/management/api/v1/document/${id}`, token, {
    values: Array.from(merged.values()),
    variants: [{ culture: null, name: variantName }],
  });
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
  await poll(async () => {
    try {
      const res = await request(`${base}/`, { timeout: 15_000 });
      return res.status === 200;
    } catch { return false; }
  }, now() + timeoutSec, `/ to return 200`);
}

async function confirmChildren(base, token, rootId) {
  const data = await jsonGet(`${base}/umbraco/management/api/v1/tree/document/children?parentId=${rootId}&skip=0&take=100`, token);
  const items = data?.items ?? [];
  let unpublished = 0;
  for (const item of items) {
    const variant = item.variants?.[0] ?? {};
    const state = variant.state ?? 'unknown';
    const name = variant.name || item.name || item.id;
    const tag = item.hasChildren ? ' (has children)' : '';
    console.log(`Child '${name}' (${item.id}) state=${state}${tag}`);
    if (state !== 'Published') unpublished++;
  }
  return unpublished;
}

// --- Theme helpers ----------------------------------------------------------

function getThemeCssMarker(themeName) {
  const lower = (themeName || '').toLowerCase();
  return `/App_Plugins/Articulate/Themes/${lower}/assets/dist/css/${lower}.min.css`;
}

function getAltTheme(currentTheme) {
  const themes = ['Vapor', 'Material', 'Phantom', 'Mini'];
  const current = (currentTheme || 'Material').toLowerCase();
  return themes.find(t => t.toLowerCase() !== current) || 'Material';
}

async function verifyThemeInHtml(base, expectedTheme, timeoutSec) {
  // Theme folders are title-/upper-case on disk (Material, VAPOR, ...) so
  // match case-insensitively against the served HTML.
  const marker = getThemeCssMarker(expectedTheme).toLowerCase();
  await poll(async () => {
    try {
      const res = await request(`${base}/`, { timeout: 15_000 });
      if (res.status !== 200) return false;
      return res.body.toLowerCase().includes(marker);
    } catch { return false; }
  }, now() + timeoutSec, `HTML to contain theme CSS marker: ${marker}`);
}

// --- main -------------------------------------------------------------------

async function main() {
  const validModes = ['publish', 'confirm', 'smoke', 'theme'];
  const mode = process.argv[2] || 'publish';
  if (!validModes.includes(mode)) {
    die(`Usage: smoke.mjs <publish|confirm|smoke|theme> [--no-descendants]`);
  }

  const noDescendants = process.argv.includes('--no-descendants');
  const base = env('UMBRACO_PUBLIC_URL', 'https://localhost:18443').replace(/\/+$/, '');
  const timeoutSec = parseInt(env('TIMEOUT_SECONDS', '300'), 10);
  if (!Number.isInteger(timeoutSec) || timeoutSec <= 0) {
    die('TIMEOUT_SECONDS must be a positive integer.');
  }

  // --- smoke mode (no auth needed) ------------------------------------------
  if (mode === 'smoke') {
    console.log('Waiting for production root to return 200');
    await waitForRoot(base, timeoutSec);
    console.log('Production smoke passed: / returned 200.');
    return;
  }

  // --- confirm / publish / theme: shared setup (token + root) ---------------
  const clientId = env('ARTICULATE_DEV_AUTOMATION_CLIENT_ID', 'articulate-dev-automation');
  const clientSecret = requiredEnv('ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET');

  console.log('Requesting access token');
  const token = await requestToken(base, clientId, clientSecret, timeoutSec);

  console.log('Finding Articulate root');
  const rootId = await findArticulateRoot(base, token);
  console.log(`Root id: ${rootId}`);

  // --- confirm mode ---------------------------------------------------------
  if (mode === 'confirm') {
    console.log('Confirming published children');
    const missing = await confirmChildren(base, token, rootId);
    if (missing !== 0) die('One or more Articulate children are not published.');

    console.log('Verifying public root');
    await waitForRoot(base, timeoutSec);

    console.log('Confirmation passed');
    console.log('Dev automation confirmation passed: root and children are published and / returns 200.');
    return;
  }

  // --- theme mode -----------------------------------------------------------
  if (mode === 'theme') {
    console.log('Reading current document');
    const doc = await getDocument(base, token, rootId);
    const currentTheme = doc.values?.find(v => v.alias === 'theme')?.value ?? 'Material';
    const variantName = doc.variants?.[0]?.name ?? 'Blog';
    console.log(`Current theme: ${currentTheme}`);

    console.log('Verifying current theme renders');
    await verifyThemeInHtml(base, currentTheme, timeoutSec);
    console.log(`Confirmed: HTML contains ${getThemeCssMarker(currentTheme)}`);

    const newTheme = getAltTheme(currentTheme);
    console.log(`Changing theme to: ${newTheme}`);
    await updateDocument(base, token, rootId, [{ alias: 'theme', value: newTheme }], variantName);

    console.log('Publishing root');
    await publishRoot(base, token, rootId);
    await reloadCache(base, token);

    console.log('Verifying new theme renders');
    await verifyThemeInHtml(base, newTheme, timeoutSec);
    console.log(`Theme verification passed: HTML contains ${getThemeCssMarker(newTheme)}`);

    // Restore original theme so iterative dev runs don't drift
    console.log('Restoring original theme');
    await updateDocument(base, token, rootId, [{ alias: 'theme', value: currentTheme }], variantName);
    await publishRoot(base, token, rootId);
    await reloadCache(base, token);
    await verifyThemeInHtml(base, currentTheme, timeoutSec);
    console.log('Original theme restored.');
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
