import { randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import { expect, test, type APIRequestContext } from '@playwright/test';
import { XMLParser } from 'fast-xml-parser';

const clientId = 'articulate-test-site';
const clientSecret = process.env.ARTICULATE_TEST_SITE_CLIENT_SECRET ?? 'articulate-test-site-secret';
const parser = new XMLParser({ ignoreAttributes: false });
const apiRoot = '/umbraco/management/api/v1';

function required<T>(value: T | null | undefined | '', label: string): T {
  if (value === null || value === undefined || value === '') throw new Error(`${label} is missing`);
  return value;
}

async function token(request: APIRequestContext) {
  const response = await request.post(`${apiRoot}/security/back-office/token`, {
    form: { grant_type: 'client_credentials', client_id: clientId, client_secret: clientSecret },
  });
  expect(response.ok(), 'test-site client credentials are accepted').toBeTruthy();
  const body = await response.json() as { access_token: string };
  return body.access_token;
}

async function api(request: APIRequestContext, accessToken: string, path: string, init?: Parameters<APIRequestContext['fetch']>[1]) {
  return request.fetch(`${apiRoot}${path}`, {
    ...init,
    headers: { ...init?.headers, Authorization: `Bearer ${accessToken}` },
  });
}

async function getDocument(request: APIRequestContext, accessToken: string, id: string) {
  const response = await api(request, accessToken, `/document/${id}`);
  expect(response.ok(), 'Management API returns the document').toBeTruthy();
  return response.json() as Promise<{ documentType: { id: string }; values: Array<{ alias: string; value: unknown; [key: string]: unknown }>; variants: Array<{ culture: string | null; name: string; [key: string]: unknown }>; template?: unknown }>;
}

async function rootDocument(request: APIRequestContext, accessToken: string) {
  const response = await api(request, accessToken, '/tree/document/root?skip=0&take=100');
  expect(response.ok()).toBeTruthy();
  const tree = await response.json() as { items: Array<{ id: string }> };
  for (const item of tree.items) {
    const document = await getDocument(request, accessToken, item.id);
    const type = await api(request, accessToken, `/document-type/${document.documentType.id}`);
    const contentType = await type.json() as { alias: string };
    if (contentType.alias === 'Articulate') return { id: item.id, document };
  }
  throw new Error('No Articulate root document exists; refusing to fall back to another root.');
}

async function publish(request: APIRequestContext, accessToken: string, id: string, assertion = 'Management API publishes normally') {
  const response = await api(request, accessToken, `/document/${id}/publish`, {
    method: 'PUT', data: { publishSchedules: [{ culture: null, schedule: null }] },
  });
  expect(response.ok(), assertion).toBeTruthy();
}

async function articlesArchive(request: APIRequestContext, accessToken: string, rootId: string) {
  const childrenResponse = await api(request, accessToken, `/tree/document/children?parentId=${rootId}&skip=0&take=100`);
  expect(childrenResponse.ok(), 'Management API lists the Articulate root children').toBeTruthy();
  const children = (await childrenResponse.json() as { items: Array<{ id: string; documentType: { id: string } }> }).items;
  let archiveId: string | undefined;
  for (const child of children) {
    const response = await api(request, accessToken, `/document-type/${child.documentType.id}`);
    expect(response.ok(), 'Management API returns the child document type').toBeTruthy();
    const type = await response.json() as { alias: string };
    if (type.alias === 'ArticulateArchive') archiveId = child.id;
  }
  return required(archiveId, 'Articles archive');
}

function createMarkdownPost(request: APIRequestContext, accessToken: string, parentId: string, post: { title: string; body: string; slug: string; documentTypeId?: string; extraValues?: unknown[] }) {
  const value = (alias: string, content: unknown, editorAlias: string) => ({ alias, value: content, culture: null, segment: null, editorAlias, entityType: 'document-property-value' });
  return api(request, accessToken, '/document', {
    method: 'POST',
    data: {
      parent: { id: parentId },
      documentType: { id: post.documentTypeId ?? '9c2df3ea-74d9-41ef-8773-07960ac5a819' },
      template: null,
      variants: [{ culture: null, segment: null, name: post.title }],
      values: [value('markdown', post.body, 'Umbraco.MarkdownEditor'), value('umbracoUrlName', post.slug, 'Umbraco.TextBox'), ...(post.extraValues ?? [])],
    },
  });
}

async function deleteDocument(request: APIRequestContext, accessToken: string, id: string, assertion: string) {
  const deletion = await api(request, accessToken, `/document/${id}`, { method: 'DELETE' });
  expect([200, 204].includes(deletion.status()), assertion).toBeTruthy();
}

// Runs one restore step without letting its failure hide the others or the test's own failure.
async function attempt(recoveryErrors: string[], label: string, step: () => Promise<unknown>) {
  try {
    await step();
  } catch (error) {
    recoveryErrors.push(`${label}: ${error instanceof Error ? error.message : String(error)}`);
  }
}

function settle(name: string, testFailure: unknown, recoveryErrors: string[]) {
  if (recoveryErrors.length > 0) {
    const recoveryFailure = new Error(`${name} recovery requires attention: ${recoveryErrors.join('; ')}`);
    if (testFailure !== undefined) throw new AggregateError([testFailure, recoveryFailure], `${name} assertion failed and recovery needs attention`);
    throw recoveryFailure;
  }
  if (testFailure !== undefined) throw testFailure;
}

async function waitFor(request: APIRequestContext, path: string, predicate: (status: number, body: string) => boolean) {
  await expect.poll(async () => {
    const response = await request.get(path, { headers: { 'Cache-Control': 'no-cache' } });
    return predicate(response.status(), await response.text());
  }, { timeout: 20_000, intervals: [250, 500, 1000, 2000] }).toBe(true);
}

async function publicRouteSnapshot(request: APIRequestContext, path: string) {
  const response = await request.get(path, { maxRedirects: 0, headers: { 'Cache-Control': 'no-cache' } });
  const body = await response.text();
  const main = body.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? body;
  const title = body.match(/<title\b[^>]*>([\s\S]*?)<\/title>/i)?.[1]
    ?.replace(/<[^>]*>/g, '')
    .replace(/&amp;/g, '&')
    .trim() ?? '';
  // The tag view has no ordering contract, so compare visible main text as a sorted word multiset.
  const contentWords = main
    .replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1>/gi, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<a\b[^>]*>[\s\S]*?<\/a>/gi, ' ')
    .replace(/<[^>]*>/g, ' ')
    .replace(/&nbsp;|&#160;/gi, ' ')
    .replace(/&amp;/gi, '&')
    .replace(/&hellip;/gi, '…')
    .replace(/&(?:[a-z][a-z0-9]+|#x?[0-9a-f]+);/gi, ' ')
    .toLowerCase()
    .match(/[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*/gu) ?? [];
  contentWords.sort((left, right) => left < right ? -1 : left > right ? 1 : 0);
  const links = [...body.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/gi)]
    .map(([, attributes, content]) => {
      const href = attributes.match(/\bhref\s*=\s*(["'])(.*?)\1/i)?.[2];
      const text = content.replace(/<[^>]*>/g, '').replace(/&amp;/g, '&').replace(/\s+/g, ' ').trim();
      return href === undefined ? undefined : { href, text };
    })
    .filter((link): link is { href: string; text: string } => link !== undefined);
  links.sort((left, right) => left.href.localeCompare(right.href) || left.text.localeCompare(right.text));
  return {
    path,
    status: response.status(),
    contentType: response.headers()['content-type'] ?? null,
    title,
    contentWords,
    links,
  };
}

function searchResults(html: string): Array<{ title: string; href: string }> {
  const previews = [...html.matchAll(/<article\b([^>]*)>([\s\S]*?)<\/article>/gi)]
    .filter(([, attributes]) => attributes.match(/\bclass\s*=\s*(["'])(.*?)\1/i)?.[2]?.split(/\s+/).includes('preview'));
  return previews.flatMap(([, , article]) => {
    const heading = article.match(/<h1\b([^>]*)>([\s\S]*?)<\/h1>/i);
    if (!heading || !heading[1].match(/\bclass\s*=\s*(["'])(.*?)\1/i)?.[2]?.split(/\s+/).includes('post-title')) return [];
    const anchor = heading[2].match(/<a\b([^>]*)>([\s\S]*?)<\/a>/i);
    if (!anchor) return [];
    const href = anchor[1].match(/\bhref\s*=\s*(["'])(.*?)\1/i)?.[2];
    const title = anchor[2].replace(/<[^>]*>/g, '').replace(/&amp;/g, '&').trim();
    return href && title ? [{ href, title }] : [];
  });
}

function checkedThemeAssetUrl(href: string, siteUrl: string, expectedPath: string): URL {
  const assetUrl = new URL(href, siteUrl);
  if (assetUrl.origin !== new URL(siteUrl).origin || assetUrl.pathname !== expectedPath) {
    throw new Error('Rendered theme stylesheet must use the dedicated site and exact packaged asset path');
  }
  return assetUrl;
}

test('VAPOR search result extraction returns one post-title link per card', () => {
  const twoPreviewsAndSidebar = '<aside><a href="/first/">First post</a><a href="/second/">Second post</a></aside>' +
    '<article class="preview"><p>No results</p></article>' +
    '<article class="preview"><header><h1 class="post-title"><a href="/first/" title="Read this article">First post</a></h1></header><section><p>First excerpt <a href="/first/">excerpt link</a></p><p class="readmore"><a href="/first/" title="Read this article">Read this article <i class="fa-solid fa-circle-chevron-right"></i></a></p></section></article>' +
    '<article class="preview"><header><h1 class="post-title"><a href="/second/" title="Read this article">Second post</a></h1></header><section><p>Second excerpt</p><p class="readmore"><a href="/second/" title="Read this article">Read this article <i class="fa-solid fa-circle-chevron-right"></i></a></p></section></article>' +
    '<aside><a href="/first/">First post</a><a href="/second/">Second post</a></aside>';
  expect(searchResults(twoPreviewsAndSidebar), 'both VAPOR cards yield one title link; read-more, excerpt and outside links are excluded').toEqual([
    { href: '/first/', title: 'First post' },
    { href: '/second/', title: 'Second post' },
  ]);
});

async function rssItems(request: APIRequestContext, path: string) {
  const response = await request.get(path);
  expect(response.status(), 'RSS endpoint returns HTTP 200').toBe(200);
  const feed = parser.parse(await response.text()) as {
    rss?: { channel?: { item?: Array<{ title?: string; link?: string }> | { title?: string; link?: string } } };
  };
  expect(feed.rss?.channel, 'HTTP 200 response contains an RSS channel').toBeTruthy();
  const items = feed.rss?.channel?.item;
  return Array.isArray(items) ? items : items ? [items] : [];
}

test('draft publishes as rendered content and an RSS item, then unpublishes the route', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id: rootId } = await rootDocument(request, accessToken);
  const articlesId = await articlesArchive(request, accessToken, rootId);

  const marker = `e2e-${randomUUID()}`;
  const title = `E2E ${marker}`;
  const body = `Distinctive publishing body ${marker}`;
  const slug = marker.toLowerCase();
  const siteUrl = required(baseURL, 'configured isolated base URL');
  const archiveUrlsResponse = await api(request, accessToken, `/document/urls?id=${articlesId}`);
  expect(archiveUrlsResponse.ok()).toBeTruthy();
  const archiveUrls = await archiveUrlsResponse.json() as Array<{ urlInfos: Array<{ url: string }> }>;
  const archiveUrl = required(archiveUrls[0]?.urlInfos[0]?.url, 'published Articles archive URL');
  const archivePath = new URL(archiveUrl, siteUrl).pathname;
  const publicPath = `${archivePath.replace(/\/$/, '')}/${slug}/`;
  const rssPath = '/rss';
  let documentId: string | undefined;
  let testFailure: unknown;
  const recoveryErrors: string[] = [];

  try {
    const createResponse = await createMarkdownPost(request, accessToken, articlesId, { title, body, slug });
    expect(createResponse.status(), 'Management API creates the draft').toBe(201);
    const location = required(createResponse.headers().location, 'create response location');
    documentId = required(new URL(location, siteUrl).pathname.split('/').at(-1), 'created document id');

    const before = await request.get(publicPath);
    expect(before.status(), 'draft post is not publicly available').toBe(404);
    await publish(request, accessToken, documentId);
    const urlsResponse = await api(request, accessToken, `/document/urls?id=${documentId}`);
    expect(urlsResponse.ok()).toBeTruthy();
    const urls = await urlsResponse.json() as Array<{ urlInfos: Array<{ url: string }> }>;
    const publishedUrl = required(urls[0]?.urlInfos[0]?.url, 'published document URL');
    const expectedAbsoluteUrl = new URL(publishedUrl, siteUrl).href;
    expect(new URL(expectedAbsoluteUrl).pathname).toBe(publicPath);
    await waitFor(request, publicPath, (status, html) => status === 200 && html.includes(body));
    await expect.poll(async () => {
      return (await rssItems(request, rssPath)).some(item => item.title === title && item.link === expectedAbsoluteUrl);
    }, { timeout: 20_000 }).toBe(true);

    const unpublish = await api(request, accessToken, `/document/${documentId}/unpublish`, { method: 'PUT', data: { cultures: null } });
    expect(unpublish.ok(), 'Management API unpublishes normally').toBeTruthy();
    await waitFor(request, publicPath, status => status === 404);
  } catch (error) {
    testFailure = error;
  } finally {
    const createdId = documentId;
    if (createdId) await attempt(recoveryErrors, 'test content cleanup', () => deleteDocument(request, accessToken, createdId, 'test content is cleaned up'));
  }
  settle('Publishing', testFailure, recoveryErrors);
});

test('RSS maxItems binds to query values and clamps values below one', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id: rootId } = await rootDocument(request, accessToken);
  const articlesId = await articlesArchive(request, accessToken, rootId);
  const siteUrl = required(baseURL, 'configured isolated base URL');
  // Fixtures only guarantee at least two published posts; which posts fill the feed is not asserted.
  const fixtures = [
    { title: `E2E RSS ${randomUUID()}`, slug: `rss-${randomUUID()}` },
    { title: `E2E RSS ${randomUUID()}`, slug: `rss-${randomUUID()}` },
  ];
  const documentIds: string[] = [];
  let testFailure: unknown;
  const recoveryErrors: string[] = [];
  try {
    for (const fixture of fixtures) {
      const created = await createMarkdownPost(request, accessToken, articlesId, { title: fixture.title, body: `RSS fixture ${fixture.slug}`, slug: fixture.slug });
      expect(created.status(), 'Management API creates each RSS draft').toBe(201);
      const location = required(created.headers().location, 'create response location');
      const id = required(new URL(location, siteUrl).pathname.split('/').at(-1), 'created document id');
      documentIds.push(id);
      await publish(request, accessToken, id);
    }

    const readFeed = async (maxItems: number) => rssItems(request, `/rss?maxItems=${maxItems}`);
    await expect.poll(async () => (await readFeed(2)).length, { message: 'maxItems=2 returns two items', timeout: 20_000, intervals: [250, 500, 1000, 2000] }).toBe(2);
    expect(await readFeed(0), 'maxItems=0 is clamped to one item').toHaveLength(1);
  } catch (error) {
    testFailure = error;
  } finally {
    for (const id of documentIds) await attempt(recoveryErrors, `RSS fixture ${id} cleanup`, () => deleteDocument(request, accessToken, id, 'RSS fixture is cleaned up'));
  }
  settle('RSS maxItems', testFailure, recoveryErrors);
});

test('tag and category listings and scoped RSS contain only matching published posts', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const siteUrl = required(baseURL, 'configured isolated base URL');
  const info = test.info();
  const { id: rootId, document: root } = await rootDocument(request, accessToken);
  const tagsUrlName = root.values.find(value => value.alias === 'tagsUrlName')?.value;
  const categoriesUrlName = root.values.find(value => value.alias === 'categoriesUrlName')?.value;
  if (typeof tagsUrlName !== 'string' || !tagsUrlName) throw new Error('tagsUrlName is missing or not a string');
  if (typeof categoriesUrlName !== 'string' || !categoriesUrlName) throw new Error('categoriesUrlName is missing or not a string');

  const markdownTypeResponse = await api(request, accessToken, '/document-type/9c2df3ea-74d9-41ef-8773-07960ac5a819');
  expect(markdownTypeResponse.ok(), 'Management API returns the deployed Markdown document type').toBeTruthy();
  const markdownType = await markdownTypeResponse.json() as {
    id: string;
    alias: string;
    compositions: Array<{ compositionType: string; documentType?: { id: string } }>;
  };
  expect(markdownType.alias).toBe('ArticulateMarkdown');
  const postTypeId = markdownType.compositions.find(composition => composition.compositionType === 'Inheritance')?.documentType?.id;
  const articulatePostTypeId = required(postTypeId, 'ArticulatePost inheritance type id');
  const postTypeResponse = await api(request, accessToken, `/document-type/${articulatePostTypeId}`);
  expect(postTypeResponse.ok(), 'Management API returns the inherited ArticulatePost schema').toBeTruthy();
  const postType = await postTypeResponse.json() as {
    alias: string;
    properties: Array<{ alias: string; dataType?: { id: string } }>;
  };
  expect(postType.alias).toBe('ArticulatePost');

  const readTagField = async (alias: 'tags' | 'categories', expectedGroup: string) => {
    const property = required(postType.properties.find(item => item.alias === alias), `${alias} property`);
    const dataTypeId = required(property.dataType?.id, `${alias} data type id`);
    const response = await api(request, accessToken, `/data-type/${dataTypeId}`);
    expect(response.ok(), `Management API returns the ${alias} data type`).toBeTruthy();
    const dataType = await response.json() as {
      id: string;
      editorAlias: string;
      editorUiAlias: string;
      values: Array<{ alias: string; value: unknown }>;
    };
    const setting = (name: string) => dataType.values.find(value => value.alias === name)?.value;
    const group = setting('group');
    const storageType = setting('storageType');
    expect(dataType.editorAlias, `${alias} uses the installed Umbraco tag editor`).toBe('Umbraco.Tags');
    expect(dataType.editorUiAlias, `${alias} uses the installed tags value editor`).toBe('Umb.PropertyEditorUi.Tags');
    expect(group, `${alias} has its actual configured tag group`).toBe(expectedGroup);
    expect(storageType, `${alias} uses the configured JSON storage`).toBe('Json');
    if (group !== expectedGroup || storageType !== 'Json') throw new Error(`${alias} editor configuration does not match the actual Articulate tag schema`);
    return { alias, dataTypeId, editorAlias: dataType.editorAlias, group, storageType };
  };
  const tagsField = await readTagField('tags', 'ArticulateTags');
  const categoriesField = await readTagField('categories', 'ArticulateCategories');
  const schemaPath = info.outputPath('n4-live-taxonomy-schema.json');
  await mkdir(dirname(schemaPath), { recursive: true });
  await writeFile(schemaPath, JSON.stringify({ markdownTypeId: markdownType.id, postTypeId: articulatePostTypeId, tagsField, categoriesField }, null, 2));
  await info.attach('n4-live-taxonomy-schema.json', { path: schemaPath, contentType: 'application/json' });

  const archiveId = await articlesArchive(request, accessToken, rootId);
  const marker = `n4${randomUUID().replaceAll('-', '')}`;
  const sharedTag = `e2e-tag-${marker}-shared`;
  const otherTag = `e2e-tag-${marker}-other`;
  const onlyTag = `e2e-tag-${marker}-only`;
  const sharedCategory = `e2e-category-${marker}-shared`;
  const onlyCategory = `e2e-category-${marker}-only`;
  const fixtures = [
    { title: `E2E taxonomy ${marker} alpha`, slug: `taxonomy-${marker}-alpha`, body: `Taxonomy fixture ${marker} alpha`, tags: [sharedTag, onlyTag], categories: [onlyCategory] },
    { title: `E2E taxonomy ${marker} beta`, slug: `taxonomy-${marker}-beta`, body: `Taxonomy fixture ${marker} beta`, tags: [sharedTag], categories: [sharedCategory] },
    { title: `E2E taxonomy ${marker} gamma`, slug: `taxonomy-${marker}-gamma`, body: `Taxonomy fixture ${marker} gamma`, tags: [otherTag], categories: [sharedCategory] },
  ];
  const fixtureJournal = fixtures.map(fixture => ({
    ...fixture,
    state: 'intent-recorded' as string,
    status: undefined as number | undefined,
    location: undefined as string | undefined,
    id: undefined as string | undefined,
    persistedTags: undefined as string[] | undefined,
    persistedCategories: undefined as string[] | undefined,
    expectedUrl: undefined as string | undefined,
  }));
  const remainingTagRecords: Array<{ group: string; items: Array<{ text?: string | null; group?: string | null; nodeCount: number }> }> = [];
  const journalPath = info.outputPath('n4-taxonomy-fixture-journal.json');
  await mkdir(dirname(journalPath), { recursive: true });
  const writeJournal = async () => writeFile(journalPath, JSON.stringify({ fixtures: fixtureJournal, ownedTagRecordsAfterCleanup: remainingTagRecords }, null, 2));
  await writeJournal();
  await info.attach('n4-taxonomy-fixture-intent.json', { path: journalPath, contentType: 'application/json' });

  const cleanupErrors: string[] = [];
  let testFailure: unknown;
  try {
    for (const [index, fixture] of fixtures.entries()) {
      const journalEntry = fixtureJournal[index];
      if (!journalEntry) throw new Error('Taxonomy fixture journal lost its entry');
      journalEntry.state = 'creating';
      await writeJournal();
      const created = await createMarkdownPost(request, accessToken, archiveId, {
        title: fixture.title,
        body: fixture.body,
        slug: fixture.slug,
        documentTypeId: markdownType.id,
        extraValues: [
          { alias: tagsField.alias, value: fixture.tags, culture: null, segment: null, editorAlias: tagsField.editorAlias, entityType: 'document-property-value' },
          { alias: categoriesField.alias, value: fixture.categories, culture: null, segment: null, editorAlias: categoriesField.editorAlias, entityType: 'document-property-value' },
        ],
      });
      journalEntry.status = created.status();
      journalEntry.location = created.headers().location;
      if (journalEntry.location) {
        journalEntry.id = new URL(journalEntry.location, siteUrl).pathname.split('/').filter(Boolean).at(-1);
      }
      journalEntry.state = journalEntry.id ? 'created' : 'create-response-without-id';
      await writeJournal();
      expect(created.status(), 'Management API creates each owned taxonomy fixture').toBe(201);
      const documentId = required(journalEntry.id, 'created taxonomy fixture id');

      const persisted = await getDocument(request, accessToken, documentId);
      const persistedTags = persisted.values.find(value => value.alias === tagsField.alias)?.value;
      const persistedCategories = persisted.values.find(value => value.alias === categoriesField.alias)?.value;
      const readStringArray = (value: unknown, label: string) => {
        if (!Array.isArray(value)) throw new Error(`${label} is not stored as a JSON array`);
        const strings = value.filter((item): item is string => typeof item === 'string');
        if (strings.length !== value.length) throw new Error(`${label} contains a non-string value`);
        return strings;
      };
      journalEntry.persistedTags = readStringArray(persistedTags, 'tags property');
      journalEntry.persistedCategories = readStringArray(persistedCategories, 'categories property');
      expect(journalEntry.persistedTags, 'Management API persists tags as the configured JSON-backed array').toEqual(fixture.tags);
      expect(journalEntry.persistedCategories, 'Management API persists categories as the configured JSON-backed array').toEqual(fixture.categories);
      await writeJournal();
      await publish(request, accessToken, documentId);
      journalEntry.state = 'published';
      await writeJournal();
    }

    const expectedPairs: Array<{ title: string; url: string }> = [];
    for (const fixture of fixtures) {
      const journalEntry = fixtureJournal.find(entry => entry.title === fixture.title);
      const documentId = required(journalEntry?.id, 'published taxonomy fixture id');
      const urlsResponse = await api(request, accessToken, `/document/urls?id=${documentId}`);
      expect(urlsResponse.ok(), 'Management API returns each published taxonomy fixture URL').toBeTruthy();
      const urls = await urlsResponse.json() as Array<{ urlInfos: Array<{ url: string }> }>;
      const expectedUrl = new URL(required(urls[0]?.urlInfos[0]?.url, 'published taxonomy post URL'), siteUrl).href;
      expectedPairs.push({ title: fixture.title, url: expectedUrl });
      if (journalEntry) {
        journalEntry.expectedUrl = expectedUrl;
        await writeJournal();
      }
    }
    const fixtureKeys = new Set(expectedPairs.map(pair => JSON.stringify(pair)));
    for (const [index, fixture] of fixtures.entries()) {
      const expectedUrl = expectedPairs[index]?.url;
      const publicPath = new URL(required(expectedUrl, 'expected public taxonomy post URL')).pathname;
      await waitFor(request, publicPath, (status, html) => status === 200 && html.includes(fixture.body));
    }
    await expect.poll(async () => {
      const items = await rssItems(request, '/rss');
      return expectedPairs.every(pair => items.some(item => item.title === pair.title && item.link === pair.url));
    }, { timeout: 20_000, intervals: [250, 500, 1000, 2000] }).toBe(true);

    const assertFixturePairs = (observed: Array<{ title: string; url: string }>, expected: Array<{ title: string; url: string }>, output: string) => {
      const owned = observed.filter(pair => expectedPairs.some(expected => expected.title === pair.title || expected.url === pair.url));
      const actualKeys = owned.map(pair => JSON.stringify(pair)).sort();
      const expectedKeys = expected.map(pair => JSON.stringify(pair)).sort();
      expect(actualKeys, `${output} contains exactly its owned matching title and URL pairs`).toEqual(expectedKeys);
      const excluded = fixtures.filter(fixture => !expected.some(pair => pair.title === fixture.title));
      for (const fixture of excluded) {
        const excludedPair = expectedPairs.find(pair => pair.title === fixture.title);
        expect(observed.some(pair => pair.title === fixture.title || pair.url === excludedPair?.url), `${output} excludes nonmatching published fixture ${fixture.title}`).toBe(false);
      }
    };
    const tagExpected = expectedPairs.filter(pair => fixtures.find(fixture => fixture.title === pair.title)?.tags.includes(sharedTag));
    const categoryExpected = expectedPairs.filter(pair => fixtures.find(fixture => fixture.title === pair.title)?.categories.includes(sharedCategory));
    const readList = async (path: string, expected: Array<{ title: string; url: string }>, label: string) => {
      const response = await request.get(path);
      expect(response.status(), `${label} returns HTTP 200`).toBe(200);
      const observed = searchResults(await response.text()).map(result => ({ title: result.title, url: new URL(result.href, siteUrl).href }));
      assertFixturePairs(observed, expected, label);
    };
    const readScopedFeed = async (path: string, expected: Array<{ title: string; url: string }>, label: string) => {
      const items = await rssItems(request, path);
      const observed = items.flatMap(item => typeof item.title === 'string' && typeof item.link === 'string'
        ? [{ title: item.title, url: new URL(item.link, siteUrl).href }]
        : []);
      assertFixturePairs(observed, expected, label);
    };

    await readList(`/${tagsUrlName}/${sharedTag}`, tagExpected, 'shared-tag listing');
    await readList(`/${categoriesUrlName}/${sharedCategory}`, categoryExpected, 'shared-category listing');
    await readScopedFeed(`/${tagsUrlName}/${sharedTag}/rss`, tagExpected, 'shared-tag RSS');
    await readScopedFeed(`/${categoriesUrlName}/${sharedCategory}/rss`, categoryExpected, 'shared-category RSS');
    expect(tagExpected.map(pair => pair.title).sort()).not.toEqual(categoryExpected.map(pair => pair.title).sort());
    expect(fixtureKeys.size).toBe(fixtures.length);
  } catch (error) {
    testFailure = error;
  } finally {
    for (const entry of fixtureJournal) {
      if (!entry.id) {
        if (entry.state !== 'intent-recorded') cleanupErrors.push(`fixture ${entry.title}: create was attempted but returned no document id; inspect ${journalPath}`);
        continue;
      }
      try {
        const deletion = await api(request, accessToken, `/document/${entry.id}`, { method: 'DELETE' });
        if (![200, 204].includes(deletion.status())) throw new Error(`DELETE returned ${deletion.status()}`);
        const verification = await api(request, accessToken, `/document/${entry.id}`);
        if (verification.status() !== 404) throw new Error(`post-delete Management API read returned ${verification.status()}`);
        entry.state = 'deleted-and-verified';
        await writeJournal();
      } catch (error) {
        cleanupErrors.push(`fixture ${entry.id}: ${error instanceof Error ? error.message : String(error)}`);
        entry.state = 'cleanup-failed';
        try { await writeJournal(); } catch (journalError) {
          cleanupErrors.push(`fixture journal: ${journalError instanceof Error ? journalError.message : String(journalError)}`);
        }
      }
    }
    for (const [group, prefix, names] of [
      [tagsField.group, `e2e-tag-${marker}`, [sharedTag, otherTag, onlyTag]],
      [categoriesField.group, `e2e-category-${marker}`, [sharedCategory, onlyCategory]],
    ] as const) {
      try {
        const response = await api(request, accessToken, `/tag?query=${encodeURIComponent(prefix)}&tagGroup=${encodeURIComponent(group)}&skip=0&take=100`);
        if (!response.ok()) throw new Error(`tag record lookup returned ${response.status()}`);
        const body = await response.json() as { items: Array<{ text?: string | null; group?: string | null; nodeCount: number }> };
        const items = body.items.filter(item => names.includes(item.text ?? '') && item.group === group);
        remainingTagRecords.push({ group, items });
        if (items.some(item => item.nodeCount > 0)) cleanupErrors.push(`${group}: owned tag still has published-content relationships`);
      } catch (error) {
        cleanupErrors.push(`${group} owned tag record verification: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    try {
      await writeJournal();
      const tagRecordsPath = info.outputPath('n4-taxonomy-tag-records-after-cleanup.json');
      await writeFile(tagRecordsPath, JSON.stringify(remainingTagRecords, null, 2));
      await info.attach('n4-taxonomy-fixture-journal.json', { path: journalPath, contentType: 'application/json' });
      await info.attach('n4-taxonomy-tag-records-after-cleanup.json', { path: tagRecordsPath, contentType: 'application/json' });
    } catch (error) {
      cleanupErrors.push(`cleanup evidence attachment: ${error instanceof Error ? error.message : String(error)}`);
    }
  }
  if (cleanupErrors.length > 0) {
    const recoveryFailure = new Error(`N4 recovery needs attention: ${cleanupErrors.join('; ')}`);
    if (testFailure !== undefined) throw new AggregateError([testFailure, recoveryFailure], 'N4 test failed and fixture recovery needs attention');
    throw recoveryFailure;
  }
  if (testFailure !== undefined) throw testFailure;
});

test('public search discovers a published Markdown post and excludes drafts and non-matching queries', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id: rootId, document: root } = await rootDocument(request, accessToken);
  expect(root.values.find(value => value.alias === 'theme')?.value, 'VAPOR List.cshtml owns the dedicated fixture search-result article markup').toBe('VAPOR');
  const searchValue = root.values.find(value => value.alias === 'searchUrlName')?.value;
  if (typeof searchValue !== 'string' || !searchValue) throw new Error('searchUrlName is missing or not a string');

  const archiveId = await articlesArchive(request, accessToken, rootId);
  const marker = `e2e${randomUUID().replaceAll('-', '')}`;
  const titleToken = `title${randomUUID().replaceAll('-', '')}`;
  const title = `E2E Search ${titleToken}`;
  const body = `SearchableMarkdown ${marker} searchable fixture`;
  const slug = `search-${randomUUID()}`;
  const siteUrl = required(baseURL, 'configured isolated base URL');
  const archiveUrlsResponse = await api(request, accessToken, `/document/urls?id=${archiveId}`);
  expect(archiveUrlsResponse.ok()).toBeTruthy();
  const archiveUrls = await archiveUrlsResponse.json() as Array<{ urlInfos: Array<{ url: string }> }>;
  const archiveUrl = required(archiveUrls[0]?.urlInfos[0]?.url, 'published Articles archive URL');
  const archivePath = new URL(archiveUrl, siteUrl).pathname;
  const publicPath = `${archivePath.replace(/\/$/, '')}/${slug}/`;
  const resultPath = `/${searchValue}/?term=${encodeURIComponent(marker)}`;
  const titleOnlyPath = `/${searchValue}/?term=${encodeURIComponent(titleToken)}`;
  const multiTermPath = `/${searchValue}/?term=${encodeURIComponent(`${titleToken} ${marker}`)}`;
  const characterFiller = `charfiller${randomUUID().replaceAll('-', '')}`;
  const characterInRangeFiller = `${characterFiller}${'x'.repeat(164 - characterFiller.length)}`;
  const characterBoundaryFiller = `${characterFiller}${'x'.repeat(199 - characterFiller.length)}`;
  const characterInRangeQuery = `${characterInRangeFiller} ${marker}`;
  const characterBoundaryQuery = `${characterBoundaryFiller} ${marker}`;
  const tokenFillers = Array.from({ length: 10 }, (_, index) => `f${randomUUID().replaceAll('-', '').slice(0, 8)}${index}`);
  const tokenTenQuery = `${tokenFillers.slice(0, 9).join(' ')} ${marker}`;
  const tokenElevenQuery = `${tokenFillers.join(' ')} ${marker}`;
  expect(characterInRangeQuery.length).toBe(200);
  expect(characterInRangeQuery.indexOf(marker)).toBe(165);
  expect(characterInRangeQuery.indexOf(marker) + marker.length).toBe(200);
  expect(characterBoundaryQuery.length).toBe(200 + marker.length);
  expect(characterBoundaryQuery.indexOf(marker)).toBe(200);
  expect(characterInRangeQuery.split(' ').length).toBe(2);
  expect(characterBoundaryQuery.split(' ').length).toBe(2);
  expect(tokenTenQuery.length).toBe(134);
  expect(tokenElevenQuery.length).toBe(145);
  expect(tokenTenQuery.split(' ').length).toBe(10);
  expect(tokenElevenQuery.split(' ').length).toBe(11);
  for (const filler of [characterFiller, characterInRangeFiller, characterBoundaryFiller, ...tokenFillers]) {
    expect([title, body, slug].some(value => value.includes(filler)), 'boundary filler is absent from the owned document').toBe(false);
  }
  const characterInRangePath = `/${searchValue}/?term=${encodeURIComponent(characterInRangeQuery)}`;
  const characterBoundaryPath = `/${searchValue}/?term=${encodeURIComponent(characterBoundaryQuery)}`;
  const tokenTenPath = `/${searchValue}/?term=${encodeURIComponent(tokenTenQuery)}`;
  const tokenElevenPath = `/${searchValue}/?term=${encodeURIComponent(tokenElevenQuery)}`;
  const punctuationPath = `/${searchValue}/?term=${encodeURIComponent(`${marker} "`)}`;
  const nonmatchingPath = `/${searchValue}/?term=${encodeURIComponent(`absent${randomUUID().replaceAll('-', '')}`)}`;
  let documentId: string | undefined;
  let testFailure: unknown;
  const recoveryErrors: string[] = [];

  try {
    const createResponse = await createMarkdownPost(request, accessToken, archiveId, { title, body, slug });
    expect(createResponse.status(), 'Management API creates the draft').toBe(201);
    const location = required(createResponse.headers().location, 'create response location');
    documentId = required(new URL(location, siteUrl).pathname.split('/').at(-1), 'created document id');

    for (const queryPath of [resultPath, titleOnlyPath, multiTermPath, nonmatchingPath]) {
      const response = await request.get(queryPath);
      expect(response.status(), 'public search returns HTTP 200').toBe(200);
      expect(searchResults(await response.text()).some(result => new URL(result.href, siteUrl).href === new URL(publicPath, siteUrl).href), 'draft URL is absent from rendered search links').toBe(false);
    }

    await publish(request, accessToken, documentId);
    const urlsResponse = await api(request, accessToken, `/document/urls?id=${documentId}`);
    expect(urlsResponse.ok()).toBeTruthy();
    const urls = await urlsResponse.json() as Array<{ urlInfos: Array<{ url: string }> }>;
    const publishedUrl = required(urls[0]?.urlInfos[0]?.url, 'published document URL');
    const expectedUrl = new URL(publishedUrl, siteUrl).href;
    expect(new URL(expectedUrl).pathname).toBe(publicPath);
    const includesOwnedResult = async (path: string) => {
      const response = await request.get(path);
      expect(response.status(), 'public search returns HTTP 200 while indexing propagates').toBe(200);
      return searchResults(await response.text()).some(result =>
        result.title === title && new URL(result.href, siteUrl).href === expectedUrl);
    };
    // MembersIndex contains no blog posts; this non-redirect request proves normal searches fall back to ExternalIndex.
    for (const queryPath of [resultPath, titleOnlyPath, multiTermPath, tokenTenPath, characterInRangePath, punctuationPath, `${resultPath}&indexName=MembersIndex`]) {
      await expect.poll(() => includesOwnedResult(queryPath), {
        message: `the published fixture appears in ${queryPath}`,
        timeout: 20_000, intervals: [250, 500, 1000, 2000],
      }).toBe(true);
    }

    expect(await includesOwnedResult(characterBoundaryPath), 'a marker wholly beyond character 200 is excluded').toBe(false);
    expect(await includesOwnedResult(tokenElevenPath), 'the matching marker in token 11 is excluded').toBe(false);
    expect(await includesOwnedResult(nonmatchingPath), 'non-matching search excludes the fixture URL from rendered links').toBe(false);
  } catch (error) {
    testFailure = error;
  } finally {
    const createdId = documentId;
    if (createdId) await attempt(recoveryErrors, 'search fixture cleanup', () => deleteDocument(request, accessToken, createdId, 'search fixture is cleaned up'));
  }
  settle('Search', testFailure, recoveryErrors);
});

test('public search paginates every matching published post exactly once', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id: rootId, document: root } = await rootDocument(request, accessToken);
  expect(root.values.find(value => value.alias === 'theme')?.value, 'VAPOR List.cshtml owns the dedicated fixture search-result article markup').toBe('VAPOR');
  const searchValue = root.values.find(value => value.alias === 'searchUrlName')?.value;
  if (typeof searchValue !== 'string' || !searchValue) throw new Error('searchUrlName is missing or not a string');
  const originalPageSize = root.values.find(value => value.alias === 'pageSize');
  if (!originalPageSize) throw new Error('pageSize is missing from the dedicated root values');
  expect(originalPageSize.value, 'the dedicated root publishes the source-owned pageSize property').toBe(10);

  const archiveId = await articlesArchive(request, accessToken, rootId);
  const siteUrl = required(baseURL, 'configured isolated base URL');
  const fixtures = Array.from({ length: 3 }, () => {
    const id = randomUUID().replaceAll('-', '');
    return { title: `E2E paged search ${id}`, slug: `paged-search-${id}` };
  });
  const marker = `pagination${randomUUID().replaceAll('-', '')}`;
  const searchPath = `/${searchValue}/?term=${encodeURIComponent(marker)}`;
  const rootBackup = {
    id: rootId,
    values: root.values,
    variants: root.variants,
    template: root.template ?? null,
    pageSize: originalPageSize.value,
  };
  const info = test.info();
  const rootBackupPath = info.outputPath('n3-root-before-mutation.json');
  const journalPath = info.outputPath('n3-fixture-journal.json');
  await mkdir(dirname(rootBackupPath), { recursive: true });
  await writeFile(rootBackupPath, JSON.stringify(rootBackup, null, 2));
  await info.attach('n3-root-before-mutation.json', { path: rootBackupPath, contentType: 'application/json' });

  const fixtureJournal: Array<{ title: string; slug: string; state: string; id?: string; location?: string }> = [];
  const documentIds: string[] = [];
  const expectedPairs: Array<{ title: string; url: string }> = [];
  let rootMutationAttempted = false;
  let publishedPageSize = 0;
  let latestSearchObservation: {
    searchPath: string;
    publishedPageSize: number;
    pages: Array<{ status: number; count: number; results: Array<{ title: string; href: string }>; html: string }>;
  } | undefined;
  let testFailure: unknown;
  const cleanupErrors: string[] = [];
  const persistRecoveryJournal = async () => {
    try {
      await writeFile(journalPath, JSON.stringify(fixtureJournal, null, 2));
    } catch (error) {
      cleanupErrors.push(`fixture journal: ${error instanceof Error ? error.message : String(error)}`);
    }
  };
  try {
    const changedValues = root.values.map(value => value.alias === 'pageSize' ? { ...value, value: 2 } : value);
    rootMutationAttempted = true;
    const update = await api(request, accessToken, `/document/${rootId}`, {
      method: 'PUT', data: { values: changedValues, variants: root.variants, template: root.template ?? null },
    });
    expect(update.ok(), 'Management API saves the actual root pageSize property as 2').toBeTruthy();
    await publish(request, accessToken, rootId);
    const updatedRoot = await getDocument(request, accessToken, rootId);
    const pageSizeValue = updatedRoot.values.find(value => value.alias === 'pageSize')?.value;
    expect(pageSizeValue, 'published root pageSize is 2').toBe(2);
    if (typeof pageSizeValue !== 'number') throw new Error('Published root pageSize is not numeric');
    publishedPageSize = pageSizeValue;

    for (const fixture of fixtures) {
      fixtureJournal.push({ ...fixture, state: 'intent-recorded-before-create' });
      await writeFile(journalPath, JSON.stringify(fixtureJournal, null, 2));
      const created = await createMarkdownPost(request, accessToken, archiveId, {
        title: fixture.title, body: `Pagination fixture ${marker} ${fixture.slug}`, slug: fixture.slug,
      });
      expect(created.status(), 'Management API creates each Markdown draft').toBe(201);
      const location = required(created.headers().location, 'created document location');
      const documentId = required(new URL(location, siteUrl).pathname.split('/').at(-1), 'created document id');
      documentIds.push(documentId);
      const journalEntry = fixtureJournal.at(-1);
      if (!journalEntry) throw new Error('Fixture journal lost its latest entry');
      Object.assign(journalEntry, { state: 'created', id: documentId, location });
      await writeFile(journalPath, JSON.stringify(fixtureJournal, null, 2));
      await publish(request, accessToken, documentId);
      const urlsResponse = await api(request, accessToken, `/document/urls?id=${documentId}`);
      expect(urlsResponse.ok(), 'Management API returns each published fixture URL').toBeTruthy();
      const urls = await urlsResponse.json() as Array<{ urlInfos: Array<{ url: string }> }>;
      expectedPairs.push({
        title: fixture.title,
        url: new URL(required(urls[0]?.urlInfos[0]?.url, 'published Markdown post URL'), siteUrl).href,
      });
    }

    const readPages = async () => {
      const [first, second] = await Promise.all([
        request.get(searchPath),
        request.get(`${searchPath}&p=2`),
      ]);
      const [firstHtml, secondHtml] = await Promise.all([first.text(), second.text()]);
      const firstResults = searchResults(firstHtml);
      const secondResults = searchResults(secondHtml);
      latestSearchObservation = {
        searchPath,
        publishedPageSize,
        pages: [
          { status: first.status(), count: firstResults.length, results: firstResults.map(result => ({ title: result.title, href: new URL(result.href, siteUrl).href })), html: firstHtml },
          { status: second.status(), count: secondResults.length, results: secondResults.map(result => ({ title: result.title, href: new URL(result.href, siteUrl).href })), html: secondHtml },
        ],
      };
      if (first.status() !== 200 || second.status() !== 200) return undefined;
      return [firstResults, secondResults] as const;
    };
    const expectedKeys = new Set(expectedPairs.map(pair => JSON.stringify(pair)));
    let pages: Awaited<ReturnType<typeof readPages>>;
    await expect.poll(async () => {
      pages = await readPages();
      if (!pages) return false;
      const pairs = pages.flatMap(results => results.map(result => ({
        title: result.title,
        url: new URL(result.href, siteUrl).href,
      })));
      const ownedKeys = new Set(pairs.filter(pair => expectedKeys.has(JSON.stringify(pair))).map(pair => JSON.stringify(pair)));
      return pages[0].length === 2 && pages[1].length === 1 && ownedKeys.size === expectedPairs.length;
    }, { timeout: 20_000, intervals: [250, 500, 1000, 2000] }).toBe(true);

    const [firstPage, secondPage] = required(pages, 'both search pages rendered');
    expect(firstPage, 'page one contains exactly the configured capacity').toHaveLength(2);
    expect(secondPage, 'page two contains the one remaining owned post').toHaveLength(1);
    const firstPairs = firstPage.map(result => ({ title: result.title, url: new URL(result.href, siteUrl).href }));
    const secondPairs = secondPage.map(result => ({ title: result.title, url: new URL(result.href, siteUrl).href }));
    const firstKeys = firstPairs.map(pair => JSON.stringify(pair));
    const secondKeys = secondPairs.map(pair => JSON.stringify(pair));
    expect(firstKeys.filter(key => secondKeys.includes(key)), 'pages contain disjoint exact title and public URL pairs').toEqual([]);
    const allKeys = [...firstKeys, ...secondKeys];
    expect(allKeys, 'the complete two-page union has exactly three results').toHaveLength(expectedPairs.length);
    expect(new Set(allKeys).size, 'no result is duplicated across or within pages').toBe(expectedPairs.length);
    for (const key of expectedKeys) expect(allKeys, 'the complete two-page union contains every exact owned pair').toContain(key);
  } catch (error) {
    testFailure = error;
    if (latestSearchObservation) {
      try {
        const observationPath = info.outputPath('n3-last-search-observation.json');
        await writeFile(observationPath, JSON.stringify({
          searchPath: latestSearchObservation.searchPath,
          publishedPageSize: latestSearchObservation.publishedPageSize,
          pages: latestSearchObservation.pages.map(({ status, count, results }) => ({ status, count, results })),
        }, null, 2));
        await info.attach('n3-last-search-observation.json', { path: observationPath, contentType: 'application/json' });
        for (const [index, page] of latestSearchObservation.pages.entries()) {
          await info.attach(`n3-last-search-page-${index + 1}.html`, { body: page.html, contentType: 'text/html' });
        }
      } catch (diagnosticError) {
        cleanupErrors.push(`search diagnostics: ${diagnosticError instanceof Error ? diagnosticError.message : String(diagnosticError)}`);
      }
    }
  } finally {
    for (const id of documentIds) {
      try {
        const deletion = await api(request, accessToken, `/document/${id}`, { method: 'DELETE' });
        if (![200, 204].includes(deletion.status())) throw new Error(`DELETE returned ${deletion.status()}`);
        const verification = await api(request, accessToken, `/document/${id}`);
        if (verification.status() !== 404) throw new Error(`post-delete Management API read returned ${verification.status()}`);
        const entry = fixtureJournal.find(fixture => fixture.id === id);
        if (entry) entry.state = 'deleted-and-verified';
        await persistRecoveryJournal();
      } catch (error) {
        cleanupErrors.push(`fixture ${id}: ${error instanceof Error ? error.message : String(error)}`);
        const entry = fixtureJournal.find(fixture => fixture.id === id);
        if (entry) entry.state = 'cleanup-failed';
        await persistRecoveryJournal();
      }
    }
    if (rootMutationAttempted) {
      try {
        const restore = await api(request, accessToken, `/document/${rootId}`, {
          method: 'PUT', data: { values: rootBackup.values, variants: rootBackup.variants, template: rootBackup.template },
        });
        if (!restore.ok()) throw new Error(`root restore returned ${restore.status()}`);
        await publish(request, accessToken, rootId);
        const restored = await getDocument(request, accessToken, rootId);
        expect(restored.values, 'root property values are restored exactly').toEqual(rootBackup.values);
        const withoutPublishTimestamps = (variants: typeof rootBackup.variants) => variants.map(variant =>
          Object.fromEntries(Object.entries(variant).filter(([field]) => field !== 'publishDate' && field !== 'updateDate')),
        );
        expect(
          withoutPublishTimestamps(restored.variants),
          'all root variant fields are restored except publish-managed timestamps',
        ).toEqual(withoutPublishTimestamps(rootBackup.variants));
        expect(restored.template ?? null, 'root template is restored exactly, including null').toEqual(rootBackup.template);
        await info.attach('n3-root-restored.json', {
          body: JSON.stringify({ id: rootId, values: restored.values, variants: restored.variants, template: restored.template ?? null }, null, 2),
          contentType: 'application/json',
        });
      } catch (error) {
        cleanupErrors.push(`root ${rootId} restoration: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    await info.attach('n3-fixture-journal.json', { path: journalPath, contentType: 'application/json' });
  }
  if (cleanupErrors.length > 0) {
    const recoveryFailure = new Error(`N3 recovery needs attention: ${cleanupErrors.join('; ')}`);
    if (testFailure !== undefined) throw new AggregateError([testFailure, recoveryFailure], 'N3 test failed and recovery needs attention');
    throw recoveryFailure;
  }
  if (testFailure !== undefined) throw testFailure;
});

test('root theme changes serve the alternate packaged stylesheet and restore the original configuration', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id, document } = await rootDocument(request, accessToken);
  const themeValue = document.values.find(value => value.alias === 'theme');
  const originalTheme = themeValue?.value;
  if (typeof originalTheme !== 'string' || !originalTheme) throw new Error('The Articulate root theme is missing or not a string');
  const alternateTheme = originalTheme.toLowerCase() === 'vapor' ? 'Material' : 'VAPOR';
  const siteUrl = required(baseURL, 'configured isolated base URL');
  const previousValues = document.values;
  const previousVariants = document.variants;
  const previousTemplate = document.template ?? null;
  await test.info().attach('theme-before-mutation.json', {
    body: JSON.stringify({ id, theme: originalTheme, template: previousTemplate }),
    contentType: 'application/json',
  });

  const updateTheme = async (theme: string) => {
    const values = previousValues.map(value => value.alias === 'theme' ? { ...value, value: theme } : value);
    const update = await api(request, accessToken, `/document/${id}`, {
      method: 'PUT', data: { values, variants: previousVariants, template: previousTemplate },
    });
    expect(update.ok(), `Management API saves the root theme ${theme}`).toBeTruthy();
    await publish(request, accessToken, id);
  };
  const verifyTheme = async (theme: string) => {
    let href = '';
    await expect.poll(async () => {
      const page = await request.get('/');
      if (page.status() !== 200) return false;
      const html = await page.text();
      const link = [...html.matchAll(/<link\b([^>]*?)>/gi)].map(([, attributes]) => attributes.match(/\bhref\s*=\s*(["'])(.*?)\1/i)?.[2]).find(value =>
        value?.toLowerCase().includes(`/app_plugins/articulate/themes/${theme.toLowerCase()}/assets/dist/css/${theme.toLowerCase()}.min.css`));
      if (!link) return false;
      href = link;
      return true;
    }, { timeout: 20_000, intervals: [250, 500, 1000, 2000] }).toBe(true);
    const assetPaths: Record<string, string> = {
      vapor: '/App_Plugins/Articulate/Themes/VAPOR/assets/dist/css/vapor.min.css',
      material: '/App_Plugins/Articulate/Themes/Material/assets/dist/css/material.min.css',
    };
    const assetPath = assetPaths[theme.toLowerCase()];
    expect(assetPath, 'theme is one of the two packaged smoke themes').toBeTruthy();
    const assetUrl = checkedThemeAssetUrl(href, siteUrl, required(assetPath, 'allowlisted packaged theme stylesheet path'));
    expect(assetUrl.origin, 'theme stylesheet stays on the dedicated site').toBe(new URL(siteUrl).origin);
    const asset = await request.get(assetUrl.href);
    expect(asset.status(), `${theme} stylesheet is served`).toBe(200);
    expect(asset.headers()['content-type'], `${theme} asset has CSS MIME type`).toMatch(/^text\/css(?:;|$)/i);
    expect((await asset.text()).trim(), `${theme} packaged stylesheet has a body`).not.toBe('');
  };

  let mutationAttempted = false;
  let testFailure: unknown;
  const recoveryErrors: string[] = [];
  try {
    await verifyTheme(originalTheme);
    mutationAttempted = true;
    await updateTheme(alternateTheme);
    await verifyTheme(alternateTheme);
  } catch (error) {
    testFailure = error;
  } finally {
    if (mutationAttempted) {
      await attempt(recoveryErrors, 'theme restore', () => updateTheme(originalTheme));
      await attempt(recoveryErrors, 'theme restore verification', () => verifyTheme(originalTheme));
    }
  }
  settle('Theme', testFailure, recoveryErrors);
});

test('public search redirects page one canonically and reserved indexes cannot bypass published-content search', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id: rootId, document: root } = await rootDocument(request, accessToken);
  const searchValue = root.values.find(value => value.alias === 'searchUrlName')?.value;
  if (typeof searchValue !== 'string' || !searchValue) throw new Error('searchUrlName is missing or not a string');
  const term = `routing-${randomUUID()}`;
  const rootUrls = await api(request, accessToken, `/document/urls?id=${rootId}`);
  expect(rootUrls.ok()).toBeTruthy();
  const rootUrlData = await rootUrls.json() as Array<{ urlInfos: Array<{ url: string }> }>;
  const rootPath = new URL(required(rootUrlData[0]?.urlInfos[0]?.url, 'published root URL'), required(baseURL, 'configured isolated base URL')).pathname;
  // InternalIndex also holds published posts, so only the redirect's rewritten selector proves the substitution.
  for (const indexName of ['ExternalIndex', 'InternalIndex', 'MembersIndex']) {
    const redirect = await request.fetch(`/${searchValue}/?term=${encodeURIComponent(term)}&indexName=${indexName}&p=1`, { maxRedirects: 0 });
    expect(redirect.status(), `p=1 with ${indexName} uses the controller canonical redirect`).toBe(302);
    expect(redirect.headers().location, `${indexName} is served from the public external index at the published root URL`).toBe(`${rootPath}?term=${encodeURIComponent(term)}&indexName=ExternalIndex`);
  }
});

test('publishing a real search route configuration refreshes a warmed route without reload', async ({ request }) => {
  const accessToken = await token(request);
  const { id, document } = await rootDocument(request, accessToken);
  const original = document.values.find(value => value.alias === 'searchUrlName');
  expect(original?.value, 'searchUrlName is configured on the real Articulate root').toBeTruthy();
  if (!original || typeof original.value !== 'string' || !original.value) throw new Error('searchUrlName is missing or not a string');
  const oldSegment = original.value;
  const newSegment = `search-${randomUUID().slice(0, 8)}`;
  const oldPath = `/${oldSegment}/`;
  const newPath = `/${newSegment}/`;
  const searchTerm = `route-${randomUUID()}`;
  const expectedSearchOutput = 'No blog posts found';
  const oldUrl = `${oldPath}?term=${encodeURIComponent(searchTerm)}`;
  const newUrl = `${newPath}?term=${encodeURIComponent(searchTerm)}`;
  const warmed = await request.get(oldUrl);
  expect(warmed.status(), 'the original dynamic search route is warm and responds').toBe(200);
  expect(await warmed.text(), 'the warmed route renders the expected search result page').toContain(expectedSearchOutput);
  const previousValues = document.values;
  const previousVariants = document.variants;
  const previousTemplate = document.template ?? null;
  await test.info().attach('search-route-before-mutation.json', {
    body: JSON.stringify({ id, searchUrlName: oldSegment, template: previousTemplate }),
    contentType: 'application/json',
  });
  let testFailure: unknown;
  const recoveryErrors: string[] = [];
  try {
    const values = previousValues.map(value => value.alias === 'searchUrlName' ? { ...value, value: newSegment } : value);
    const update = await api(request, accessToken, `/document/${id}`, {
      method: 'PUT', data: { values, variants: previousVariants, template: previousTemplate },
    });
    expect(update.ok(), 'Management API saves the real route property').toBeTruthy();
    await publish(request, accessToken, id);
    await waitFor(request, newUrl, (status, html) => status === 200 && html.includes(expectedSearchOutput));
    await waitFor(request, oldUrl, status => status === 404);
  } catch (error) {
    testFailure = error;
  } finally {
    await attempt(recoveryErrors, 'route property restore', async () => {
      const restore = await api(request, accessToken, `/document/${id}`, {
        method: 'PUT', data: { values: previousValues, variants: previousVariants, template: previousTemplate },
      });
      expect(restore.ok(), 'fixture route property is restored').toBeTruthy();
    });
    // Republishing an unrestored draft would publish the temporary route.
    if (recoveryErrors.length === 0) {
      await attempt(recoveryErrors, 'root republish', () => publish(request, accessToken, id));
      await attempt(recoveryErrors, 'original route verification', () => waitFor(request, oldUrl, (status, html) => status === 200 && html.includes(expectedSearchOutput)));
      await attempt(recoveryErrors, 'temporary route removal verification', () => waitFor(request, newUrl, status => status === 404));
    }
  }
  settle('Search route', testFailure, recoveryErrors);
});

test('publish veto preserves existing public routes when configured route segments collide', async ({ request, baseURL }) => {
  const accessToken = await token(request);
  const { id: rootId, document: root } = await rootDocument(request, accessToken);
  const siteUrl = required(baseURL, 'configured isolated base URL');
  const routeValues = ['searchUrlName', 'categoriesUrlName', 'tagsUrlName'].map(alias => {
    const value = root.values.find(item => item.alias === alias)?.value;
    if (typeof value !== 'string' || !value) throw new Error(`${alias} is missing or not a string`);
    return { alias, value };
  });
  const rootUrls = await api(request, accessToken, `/document/urls?id=${rootId}`);
  expect(rootUrls.ok(), 'Management API returns the published root URL').toBeTruthy();
  const rootUrlData = await rootUrls.json() as Array<{ urlInfos: Array<{ url: string }> }>;
  const rootUrl = required(rootUrlData[0]?.urlInfos[0]?.url, 'published root URL');
  const publishedRootUrl = new URL(rootUrl, siteUrl);
  expect(publishedRootUrl.origin, 'published root URL remains on the dedicated site').toBe(new URL(siteUrl).origin);
  const publicPaths = [
    publishedRootUrl.pathname,
    ...routeValues.map(({ value }) => new URL(`${value.replace(/^\/+|\/+$/g, '')}/`, publishedRootUrl).pathname),
  ];
  const baselineRoutes = [];
  for (const path of publicPaths) {
    const snapshot = await publicRouteSnapshot(request, path);
    expect(snapshot.status, `published route ${path} responds before the mutation`).toBe(200);
    baselineRoutes.push(snapshot);
  }

  const marker = `e2e-route-collision-${randomUUID().replaceAll('-', '')}`;
  const collisionPath = new URL(`${marker}/`, publishedRootUrl).pathname;
  const rootBackup = {
    id: rootId,
    values: root.values,
    variants: root.variants,
    template: root.template ?? null,
    baselineRoutes,
    intendedCollision: {
      segment: marker,
      aliases: ['categoriesUrlName', 'tagsUrlName'],
    },
  };
  const info = test.info();
  const backupPath = info.outputPath('n5-root-before-mutation.json');
  await mkdir(dirname(backupPath), { recursive: true });
  await writeFile(backupPath, JSON.stringify(rootBackup, null, 2));
  await info.attach('n5-root-before-mutation.json', { path: backupPath, contentType: 'application/json' });

  let mutationAttempted = false;
  let testFailure: unknown;
  const recoveryErrors: string[] = [];
  try {
    expect(
      root.variants.every(variant => variant.state === 'Published'),
      'every original root variant is published with no pending changes before the control publish',
    ).toBe(true);
    await publish(request, accessToken, rootId, 'the valid root route configuration publishes before the collision draft');
    const validPublishedRoot = await getDocument(request, accessToken, rootId);
    expect(
      validPublishedRoot.variants.map(({ culture, state }) => ({ culture, state })),
      'the valid root remains published before the collision draft',
    ).toEqual(root.variants.map(({ culture }) => ({ culture, state: 'Published' })));

    const changedValues = root.values.map(value =>
      value.alias === 'categoriesUrlName' || value.alias === 'tagsUrlName'
        ? { ...value, value: marker }
        : value,
    );
    mutationAttempted = true;
    const update = await api(request, accessToken, `/document/${rootId}`, {
      method: 'PUT', data: { values: changedValues, variants: root.variants, template: root.template ?? null },
    });
    expect(update.ok(), 'Management API saves the owned route-collision draft').toBeTruthy();

    const collisionDraft = await getDocument(request, accessToken, rootId);
    expect(
      collisionDraft.values
        .filter(value => value.alias === 'categoriesUrlName' || value.alias === 'tagsUrlName')
        .map(({ alias, value }) => ({ alias, value }))
        .sort((left, right) => left.alias.localeCompare(right.alias)),
      'the exact conflicting route draft values were saved',
    ).toEqual([
      { alias: 'categoriesUrlName', value: marker },
      { alias: 'tagsUrlName', value: marker },
    ]);
    expect(
      collisionDraft.variants.map(({ culture, state }) => ({ culture, state })),
      'the conflicting values are a saved pending publish, not a malformed request',
    ).toEqual(root.variants.map(({ culture }) => ({ culture, state: 'PublishedPendingChanges' })));

    const rejection = await api(request, accessToken, `/document/${rootId}/publish`, {
      method: 'PUT', data: { publishSchedules: [{ culture: null, schedule: null }] },
    });
    const rejectionBody: unknown = await rejection.json();
    expect(rejection.status(), 'Umbraco reports the cancelled publish operation with its source-backed HTTP status').toBe(400);
    expect(rejectionBody, 'the normal publish endpoint reports cancellation by an event, not malformed input').toMatchObject({
      type: 'Error',
      status: 400,
      operationStatus: 'CancelledByEvent',
    });
    await info.attach('n5-publish-cancellation.json', {
      body: JSON.stringify({ status: rejection.status(), body: rejectionBody }, null, 2),
      contentType: 'application/json',
    });

    const routesAfterVeto = [];
    for (const path of publicPaths) routesAfterVeto.push(await publicRouteSnapshot(request, path));
    expect(routesAfterVeto, 'published route status, content type, title, main text and links are unchanged by the veto').toEqual(baselineRoutes);
    const collision = await request.get(collisionPath, { maxRedirects: 0, headers: { 'Cache-Control': 'no-cache' } });
    expect(collision.status(), 'the rejected collision never becomes a new public route').toBe(404);
  } catch (error) {
    testFailure = error;
  } finally {
    if (mutationAttempted) {
      try {
        const restore = await api(request, accessToken, `/document/${rootId}`, {
          method: 'PUT', data: { values: rootBackup.values, variants: rootBackup.variants, template: rootBackup.template },
        });
        if (!restore.ok()) throw new Error(`full root data restore returned ${restore.status()}`);
      } catch (error) {
        recoveryErrors.push(`root ${rootId} data restore: ${error instanceof Error ? error.message : String(error)}`);
      }

      if (recoveryErrors.length === 0) {
        try {
          await publish(request, accessToken, rootId, 'the original route configuration publishes normally after the veto');
        } catch (error) {
          recoveryErrors.push(`root ${rootId} publish-state restore: ${error instanceof Error ? error.message : String(error)}`);
        }
      }

      try {
        const restored = await getDocument(request, accessToken, rootId);
        expect(restored.values, 'all original root property values are restored').toEqual(rootBackup.values);
        const withoutPublishTimestamps = (variants: typeof rootBackup.variants) => variants.map(variant =>
          Object.fromEntries(Object.entries(variant).filter(([field]) => field !== 'publishDate' && field !== 'updateDate')),
        );
        expect(
          withoutPublishTimestamps(restored.variants),
          'all original variant fields are restored except server-managed publish timestamps',
        ).toEqual(withoutPublishTimestamps(rootBackup.variants));
        expect(restored.template ?? null, 'the original template is restored exactly, including null').toEqual(rootBackup.template);
      } catch (error) {
        recoveryErrors.push(`root ${rootId} verification: ${error instanceof Error ? error.message : String(error)}`);
      }

      try {
        await expect.poll(async () => {
          const routesAfterRestore = [];
          for (const path of publicPaths) routesAfterRestore.push(await publicRouteSnapshot(request, path));
          return routesAfterRestore;
        }, {
          message: 'all original public routes are restored with identical status, type, title, main text and links',
          timeout: 20_000, intervals: [250, 500, 1000, 2000],
        }).toEqual(baselineRoutes);
      } catch (error) {
        recoveryErrors.push(`public route restoration verification: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
  }

  if (recoveryErrors.length > 0) {
    const recoveryFailure = new Error(`N5 recovery requires attention: ${recoveryErrors.join('; ')}`);
    if (testFailure !== undefined) throw new AggregateError([testFailure, recoveryFailure], 'N5 assertion failed and recovery needs attention');
    throw recoveryFailure;
  }
  if (testFailure !== undefined) throw testFailure;
});
