# Local Docker site

Docker is optional. `docker-test` runs the same HTTP suite as native CI, behind Caddy in a container. There is no separate Docker behaviour suite.

`docker/docker-compose.yml` defines the containers. `docker/run.cs` starts and stops them. `docker/smoke.mjs` prepares content and checks startup readiness; native E2E uses it too.

## Commands

```text
dotnet run --file docker/run.cs -- help docker-dev
```

`docker/help.md` is the canonical command and option reference. The rest of this page documents runtime behavior, credentials, and direct Compose use. Examples use POSIX shell syntax; in PowerShell, replace `export NAME='value'` with `$env:NAME = 'value'`.

Publication and confirmation use `ARTICULATE_TEST_SITE_CLIENT_SECRET`. The runner supplies the local default when it is unset.

| Lane  | Image                  | HTTPS backoffice URL               | HTTP listener             |
|-------|------------------------|------------------------------------|---------------------------|
| `v17` | `articulate-local:v17` | `https://localhost:18443/umbraco/` | `http://localhost:8080/` |
| `v18` | `articulate-local:v18` | `https://localhost:18444/umbraco/` | `http://localhost:8081/` |

The runner uses host ports 18443/18444 for HTTPS and 8080/8081 for HTTP. Override either with `CADDY_HTTPS_PORT` / `CADDY_HTTP_PORT` when a port is already in use. Running `docker compose up` without the runner still requires the package-version variables in `docker/docker-compose.yml` and uses Compose's default ports (18443 HTTPS / 8080 HTTP). Prefer the per-lane runner for package values and port isolation.

The unattended install creates this default local Docker backoffice administrator:

- Email: `admin@localhost`
- Password: `@rticulate`
- Display name: `Jane Doe`

Use this account to sign in to either backoffice URL above. These are public, local-development defaults. A deployed site needs different credentials. Override the unattended user with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and `UMBRACO_USER_PASSWORD`.

## Trust Caddy's local CA once per machine

Caddy serves HTTPS with a local certificate. To avoid browser certificate warnings, add its root CA to your trust store:

```sh
dotnet run --file docker/run.cs -- docker-ca --lane v17
```

On Windows, this command adds the CA to the current user's root certificate store. Run it only if you want to trust that CA. Automated HTTP tests do not need it.

## Content preparation and readiness

For a running test site, `smoke.mjs` supports `publish`, `confirm`, and `smoke`. These prepare content and check readiness; the Playwright suite tests application behaviour:

```sh
export UMBRACO_PUBLIC_URL='https://localhost:18443/'
node docker/smoke.mjs publish
node docker/smoke.mjs confirm
node docker/smoke.mjs publish --no-descendants
```

`confirm` is read-only and checks all descendants under the Articulate root. Publication processes the root first, waits for the public route and published-content cache, then publishes descendants. Use `https://localhost:18444/` for the v18 lane. Set `NODE_BIN` if `node` is not on `PATH`. On Windows, invoke the script from PowerShell or cmd rather than passing `node.exe` through WSL or Git Bash.

`docker-test` runs the existing E2E suite after the fresh site enters Production. Before tests start, it publishes one temporary post, waits until Umbraco's Examine query API returns its exact key from `ExternalIndex`, then deletes it and verifies removal. This checks publication indexing within the existing 300-second preparation budget: Umbraco's startup rebuild can delay indexing after the root returns 200. The theme E2E changes and restores the root theme and checks the packaged stylesheet. The `publish` setup still reloads the published-content cache.

The smoke client bypasses certificate validation for loopback and RFC1918 private IPv4 hosts used by the development harness. Public hosts retain normal certificate validation.

## HTTP E2E tests

CI runs the shared 11-test suite on fresh native packaged hosts through `build/test.cs -- fresh`, one lane per matrix job. Docker is an optional deployment check: `docker-test` starts disposable containers behind Caddy and runs those same tests. No separate Docker behaviour suite is maintained.

Follow the [E2E guide](../src/Articulate.Web/Client/e2e/README.md) for manual tests using the separate `art_e2e_v17` and `art_e2e_v18` stacks. Do not use or reset `art_v17` or `art_v18`.

## LAN access

The site binds to loopback by default. To test the standalone editor from another machine, set the LAN origin consistently and reset the database so OpenIddict registers redirect URIs for that origin:

```sh
export ARTICULATE_TEST_SITE_CLIENT_SECRET='articulate-test-site-secret'
export CADDY_BIND_IP='0.0.0.0'
export CADDY_HTTPS_HOST='<LAN-IP>:18443'
export UMBRACO_PUBLIC_HOST='https://<LAN-IP>:18443'
export UMBRACO_PUBLIC_URL='https://<LAN-IP>:18443/'
export ARTICULATE_REDIRECT_URI='https://<LAN-IP>:18443/a-new/'
export ARTICULATE_LOGOUT_REDIRECT_URI='https://<LAN-IP>:18443/'
dotnet run --file docker/run.cs -- docker-dev --lane v17 --reset
```

Use port `18444` and `--lane v18` for the v18 lane. Browsers must accept Caddy's development certificate.

> [!WARNING]
> LAN exposure makes the site and its fixed development credentials available to the local network. Never use this configuration on a public or untrusted network.

During first installation, Umbraco may log two warnings that an empty culture was not found in configured localization sources. The starter package contains valid invariant content and no language payload; these warnings are harmless package-install noise and require no Articulate change.

## TinyMCE opt-in

Set `USE_TINYMCE_UMBRACO=true` before starting the Docker site when testing the optional TinyMCE integration:

```sh
export USE_TINYMCE_UMBRACO='true'
dotnet run --file docker/run.cs -- docker-dev --lane v17
```

The runner passes the lane's `TinyMceUmbracoPackageVersion` floor from `Directory.Packages.props` into the Docker build. The same environment variable works with `docker-build` and `docker-test`.

## Runtime modes

The compose stack switches between two modes through `UMBRACO_RUNTIME_MODE`:

- `BackofficeDevelopment` (default) — auto-provisions the test-site API user + client credentials after install and migrations, then publishes and confirms content via `smoke.mjs`.
- `Production` — disables that bootstrap so the only content served is what was already published in the data volume. `docker-prod` switches an existing stack into this mode and checks root readiness; `docker-test` also runs the full HTTP E2E suite against a fresh disposable project after this transition.

Typical flow: start with empty volumes in `BackofficeDevelopment`, publish and confirm content, then re-run `docker-prod` against the same volumes to confirm that published content survives a `Production`-mode restart. To run the shared suite in fresh containers, use `dotnet run --file docker/run.cs -- docker-test --lane all`.

## Cookie isolation between lanes

Browsers share `localhost` cookies across ports. The runner gives each lane separate backoffice cookie names:

- `Umbraco__CMS__Security__AuthCookieName=UMB_UCONTEXT-{lane}`
- `Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName=-{lane}`

This keeps the v17 and v18 backoffice sessions separate.

## Test-site switches

Content preparation, E2E and MCP use the same test-site API identity. Only bootstrap enablement and the client secret are configurable per run:

| Variable                                      | Default                               | Purpose                                                                  |
|-----------------------------------------------|---------------------------------------|--------------------------------------------------------------------------|
| `ARTICULATE_TEST_SITE_ENABLED`           | `true`                                | Toggle the bootstrap service entirely.                                   |
| `ARTICULATE_TEST_SITE_CLIENT_SECRET`     | `articulate-test-site-secret`         | OAuth client secret. Consumed by `smoke.mjs`; defaults are applied if unset via `Env.RequireSecret()`. |

The unattended backoffice administrator (the human sign-in) is configured separately via `UMBRACO_USER_NAME` / `UMBRACO_USER_EMAIL` / `UMBRACO_USER_PASSWORD`.

## Package and container diagnostics

Package inputs come from `build/Release/<lane>` and must include Articulate and the sample theme. Docker installs those `.nupkg` files. Docker commands invoke the package runner by default; `docker-dev --reuse-packages` skips it and reuses the existing lane output. Same-lane builds are incremental and `--clean` is required when switching lanes.

The Docker runner resolves `UmbracoCmsPackageVersion` and `TinyMceUmbracoPackageVersion` from `Directory.Packages.props` via `dotnet msbuild -getProperty`, so the site uses the same dependency floors.

Docker stores the database and media in named volumes. Rebuilding packages or images keeps that data. `docker-dev --reset` removes the selected lane's volumes. `build --clean` does not delete the database.

Rebuilding an image does not replace an already running container. The Docker utility uses `--force-recreate` where required. If a site still serves stale assets, inspect the running stack and its packaged Backoffice files:

```sh
dotnet run --file docker/run.cs -- docker-status --lane v17
```

Use `--lane v18` for the v18 lane.

## Umbraco MCP Dev

`@umbraco-cms/mcp-dev` is Umbraco's official Model Context Protocol server. [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) is an open standard that lets AI clients (Claude Desktop, Codex, Cursor, etc.) call external tools through a uniform interface. The Umbraco package authenticates as an API user (OAuth client credentials) and exposes Management API operations for documents, media, data types, document types, and other Back Office resources as MCP tools.

The Docker harness auto-provisions the API user this server expects. Configure your MCP client with:

- `UMBRACO_CLIENT_ID` = `articulate-test-site` (fixed test-site client ID)
- `UMBRACO_CLIENT_SECRET` = `ARTICULATE_TEST_SITE_CLIENT_SECRET`
- `UMBRACO_BASE_URL` = the lane's public URL (e.g. `https://localhost:18443`)

Install with the lane-matched tag (`@umbraco-cms/mcp-dev@17` for the v17 lane, `@18` for v18). See the [Umbraco MCP documentation](https://docs.umbraco.com/umbraco-developer-mcp) for the full tool list, permissions model, and Claude Desktop config snippet.

Use MCP for interactive Management API tasks. Use the shared E2E suite for repeatable behaviour checks.

> The `umbraco-articulate` OpenID client is **not** the right credential here. It is the Markdown Editor's browser-side OAuth client (see [Markdown Editor Authentication](https://github.com/Shazwazza/Articulate/wiki/Markdown-Editor-Authentication)), not an API user client-credentials identity.
