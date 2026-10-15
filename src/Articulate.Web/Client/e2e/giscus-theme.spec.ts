import { expect, test } from '@playwright/test';

test('Giscus theme proxy serves packaged CSS with Articulate CORS and cache behaviour', async ({ request }) => {
  const path = '/articulate/giscus-theme/VAPOR';
  const allowedOrigin = 'https://giscus.app';
  const allowed = await request.get(path, { headers: { Origin: allowedOrigin } });
  expect(allowed.status(), 'Articulate serves the packaged VAPOR Giscus stylesheet').toBe(200);
  expect(allowed.headers()['content-type'], 'Articulate identifies the proxied asset as CSS').toMatch(/^text\/css(?:;|$)/i);
  const css = await allowed.text();
  expect(css.trim(), 'the packaged VAPOR stylesheet is not empty').not.toBe('');
  expect(css, 'the response contains the packaged Articulate theme variables').toContain('--color-canvas-default');
  expect(allowed.headers()['cache-control'], 'Articulate caches the proxied stylesheet for one hour').toBe('public, max-age=3600');
  expect(allowed.headers()['access-control-allow-origin'], 'Articulate reflects the configured Giscus origin').toBe(allowedOrigin);
  expect(allowed.headers()['vary'], 'Articulate varies reflected CORS responses by Origin').toBe('Origin');

  const anonymous = await request.get(path);
  expect(anonymous.status(), 'Articulate serves the packaged stylesheet without an Origin header').toBe(200);
  expect(anonymous.headers()['content-type'], 'Articulate identifies the anonymous response as CSS').toMatch(/^text\/css(?:;|$)/i);
  expect((await anonymous.text()).trim(), 'the anonymous response contains the packaged theme CSS').toContain('--color-canvas-default');
  expect(anonymous.headers()['cache-control'], 'Articulate caches the anonymous stylesheet for one hour').toBe('public, max-age=3600');
  expect(anonymous.headers()['access-control-allow-origin'], 'Articulate allows anonymous requests with a wildcard').toBe('*');
  expect((anonymous.headers()['vary'] ?? '').split(',').map((token) => token.trim().toLowerCase()), 'Articulate does not vary anonymous responses by Origin').not.toContain('origin');

  const disallowed = await request.get(path, { headers: { Origin: 'https://evil.example.com' } });
  expect(disallowed.status(), 'Articulate still serves the stylesheet to inspect its CORS response').toBe(200);
  expect(disallowed.headers()['access-control-allow-origin'], 'Articulate omits allow-origin for an untrusted origin').toBeUndefined();

  const missingTheme = await request.get('/articulate/giscus-theme/NotARealTheme');
  expect(missingTheme.status(), 'Articulate keeps a valid theme key without an asset usable by Giscus').toBe(200);
  expect(missingTheme.headers()['content-type'], 'Articulate identifies the no-op response as CSS').toMatch(/^text\/css(?:;|$)/i);
  expect((await missingTheme.text()).trim(), 'Articulate returns its documented no-op CSS body').toContain('no giscus.css for theme');
});
