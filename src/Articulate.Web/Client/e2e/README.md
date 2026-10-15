# HTTP E2E tests

One Playwright suite tests the packaged application through HTTP, without a browser. It runs on native hosts in CI and can run unchanged behind Caddy in Docker. Tests delete their own posts and restore complete root configuration after each test.

## Run on a fresh native site

Build the selected Release packages first, including the sample theme, then run:

```sh
dotnet run --file build/test.cs -- fresh --lane v17
dotnet run --file build/test.cs -- fresh --lane v18
```

`--lane all` runs both sequentially. CI runs the suite in separate jobs after package builds. See [CI jobs and E2E](../../../../BUILD.md#ci-jobs-and-e2e) for workflow selection and uploads.

The runner reuses `docker/src/ArticulateDockerSite.csproj` as a normal .NET application outside the checkout. It inspects the exact packages and verifies their restored hashes. Each host has an owned temporary database, self-signed certificate, random secret and free loopback HTTPS port. No Docker client, browser installation or certificate-store change is required.

Preparation publishes and confirms starter content, restarts the host in Production, then publishes one temporary post. It waits for that post's exact key in `ExternalIndex` through Umbraco's Examine query API, deletes it and verifies removal. Each native readiness wait uses a 180-second timeout. The runner stops its owned process and deletes its temporary directory on success or failure. Host logs remain in `.temp/art_e2e_native_<lane>_<id>/`; Playwright reports and test artefacts use the candidate name in `e2e-report-*` and `e2e-results-*`.

`NODE_BIN` and `DOTNET_BIN` can select existing executables.

## Optional Docker deployment check

`dotnet run --file docker/run.cs -- docker-test --lane all --reuse-packages` runs these same 11 tests on disposable containers behind Caddy. Omit `--reuse-packages` to build packages first. Each lane gets a unique Compose project, volumes, image tag and available loopback ports. Cleanup removes only those candidate resources. There are no separate theme or Giscus smoke assertions; publication, confirmation and startup readiness remain preparation.

Do not point E2E at `art_v17`, `art_v18`, or any site other than an owned fresh candidate or the dedicated manual-test projects below.

## Optional manual Docker sites

Run from the repository root. Use only these test resources, not `art_v17` or `art_v18`:

| Lane | Project and volume prefix | Image | HTTPS / HTTP ports |
| --- | --- | --- | --- |
| v17 | `art_e2e_v17` | `articulate-e2e:v17` | 19443 / 9080 |
| v18 | `art_e2e_v18` | `articulate-e2e:v18` | 19444 / 9081 |

```sh
env COMPOSE_PROJECT_NAME=art_e2e_v17 COMPOSE_VOLUME_PREFIX=art_e2e_v17 IMAGE_TAG=articulate-e2e:v17 CADDY_HTTPS_PORT=19443 CADDY_HTTP_PORT=9080 ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 pnpm@11.19.0 -- dotnet run --file docker/run.cs -- docker-dev --lane v17 --skip-smoke --clean
env COMPOSE_PROJECT_NAME=art_e2e_v18 COMPOSE_VOLUME_PREFIX=art_e2e_v18 IMAGE_TAG=articulate-e2e:v18 CADDY_HTTPS_PORT=19444 CADDY_HTTP_PORT=9081 ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 pnpm@11.19.0 -- dotnet run --file docker/run.cs -- docker-dev --lane v18 --skip-smoke --clean
```

Build one lane at a time. `--clean` is required when switching lanes. For a fresh site, publish starter content once before testing:

```sh
env UMBRACO_PUBLIC_URL=https://localhost:19443/ ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 pnpm@11.19.0 -- node docker/smoke.mjs publish
```

For v18, use `https://localhost:19444/`. This setup reloads Umbraco's cache. Run it before tests, never during a test.

## Install and run

Install dependencies:

```sh
mise exec node@24.18.1 pnpm@11.19.0 -- pnpm --dir src/Articulate.Web/Client install --frozen-lockfile
```

Run both lanes sequentially or select one:

```sh
env ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 -- dotnet run --file build/test.cs -- e2e --lane all
env ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 -- dotnet run --file build/test.cs -- e2e --lane v17
env ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 -- dotnet run --file build/test.cs -- e2e --lane v18
```

To run Playwright directly, set the test URL and secret:

```sh
env ARTICULATE_E2E_BASE_URL=https://localhost:19443 ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 pnpm@11.19.0 -- pnpm --dir src/Articulate.Web/Client run test:e2e
env ARTICULATE_E2E_BASE_URL=https://localhost:19444 ARTICULATE_TEST_SITE_CLIENT_SECRET=articulate-test-site-secret mise exec node@24.18.1 pnpm@11.19.0 -- pnpm --dir src/Articulate.Web/Client run test:e2e
```

Reports are in `src/Articulate.Web/Client/e2e-report-<lane>`; test artefacts are in `e2e-results-<lane>`. Open a report with `dotnet run --file build/test.cs -- report --lane v17` or `--lane v18`. Playwright prints the local URL. Stop it with Ctrl+C.

The standalone config accepts HTTPS localhost ports 19443 and 19444. The integrated runner also supplies a candidate project name and its verified loopback HTTPS port; arbitrary URLs are rejected. Certificate checks are disabled only for Playwright requests. Tests run with one worker and no retries. The suite does not reload the app cache or restart the site; candidate setup and Production readiness happen before E2E starts.

Check the test types:

```sh
mise exec node@24.18.1 pnpm@11.19.0 -- pnpm --dir src/Articulate.Web/Client/v17 exec tsc --ignoreConfig --noEmit --strict --target ES2022 --module NodeNext --moduleResolution NodeNext --types node ../e2e/publishing-rss.spec.ts ../e2e/giscus-theme.spec.ts
```

## PowerShell setup

Set the same test resources before starting v17:

```powershell
$env:COMPOSE_PROJECT_NAME = 'art_e2e_v17'; $env:COMPOSE_VOLUME_PREFIX = 'art_e2e_v17'; $env:IMAGE_TAG = 'articulate-e2e:v17'; $env:CADDY_HTTPS_PORT = '19443'; $env:CADDY_HTTP_PORT = '9080'; $env:ARTICULATE_TEST_SITE_CLIENT_SECRET = 'articulate-test-site-secret'
mise exec node@24.18.1 pnpm@11.19.0 -- dotnet run --file docker/run.cs -- docker-dev --lane v17 --skip-smoke --clean
$env:UMBRACO_PUBLIC_URL = 'https://localhost:19443/'
mise exec node@24.18.1 pnpm@11.19.0 -- node docker/smoke.mjs publish
mise exec node@24.18.1 -- dotnet run --file build/test.cs -- e2e --lane v17
```

For v18, use the table's v18 values, set `UMBRACO_PUBLIC_URL` to `https://localhost:19444/`, and change `--lane v17` to `--lane v18`.

## Coverage

The site needs a published Articulate root, its Articles archive and the VAPOR theme.

| Area | Checks |
| --- | --- |
| Publishing | Draft returns 404; published post appears in HTML and RSS; unpublished route returns 404. |
| RSS | `maxItems=2` returns two items; `maxItems=0` is clamped to one. |
| Taxonomy | Tags and categories use their live JSON datatypes and separate groups. Listings and scoped RSS contain exactly the matching published title/URL pairs and exclude nonmatching posts. |
| Search | Title/body matches; drafts and non-matches excluded; 200-character and 10-token limits; quote escaping. Every matching post appears exactly once across both pages. |
| Search routing | Page-one redirect; reserved index names use published content. |
| Route collisions | A valid root publishes. A `categoriesUrlName`/`tagsUrlName` collision returns `CancelledByEvent`, leaves the invalid draft saved and keeps published routes unchanged. The original root is restored. |
| Route refresh | Publishing `searchUrlName` changes a warmed route without a reload. |
| Themes | Publishing a theme change serves the rendered CSS URL; original configuration restored. |
| Giscus | Packaged CSS, one-hour cache, reflected allowed-origin CORS, anonymous wildcard CORS without `Vary: Origin`, and missing-theme fallback. |

One helper test checks that VAPOR extraction returns only each preview's `h1.post-title` link, not read-more, excerpt or sidebar links. Search tests do not cover other themes or distinguish AND from OR queries. RSS tests do not check feed freshness after unpublish.

## Teardown

Only remove the dedicated project and volumes. Repeat with `--lane v18` as needed.

```sh
env COMPOSE_PROJECT_NAME=art_e2e_v17 COMPOSE_VOLUME_PREFIX=art_e2e_v17 IMAGE_TAG=articulate-e2e:v17 CADDY_HTTPS_PORT=19443 CADDY_HTTP_PORT=9080 dotnet run --file docker/run.cs -- docker-down --lane v17 --volumes
env COMPOSE_PROJECT_NAME=art_e2e_v18 COMPOSE_VOLUME_PREFIX=art_e2e_v18 IMAGE_TAG=articulate-e2e:v18 CADDY_HTTPS_PORT=19444 CADDY_HTTP_PORT=9081 dotnet run --file docker/run.cs -- docker-down --lane v18 --volumes
```
