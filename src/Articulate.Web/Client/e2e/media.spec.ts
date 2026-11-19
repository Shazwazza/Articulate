import { expect, test } from '@playwright/test';

// Guards picker value conversion: Umbraco 19's MediaPicker3 yields a list, which left every theme image empty.
test('starter blog logo renders as a served, signed image URL', async ({ request }) => {
  const page = await request.get('/');
  expect(page.status(), 'the starter blog root renders').toBe(200);
  const html = (await page.text()).replaceAll('&#x27;', "'").replaceAll('&amp;', '&');
  const logoUrl = /--blog-logo-image:\s*url\('([^']+)'\)/.exec(html)?.[1];
  expect(logoUrl, 'VAPOR outputs the starter blog logo crop URL').toBeTruthy();
  const image = await request.get(logoUrl!);
  expect(image.status(), `the logo crop URL ${logoUrl} is served`).toBe(200);
  expect(image.headers()['content-type'], 'the logo crop URL returns an image').toMatch(/^image\//);
});
