# Local Docker Site

`docker/docker-compose.yml` defines the containers. Cross-platform orchestration
lives in the .NET 10 file-based app at `docker/run.cs`; `smoke.mjs` contains
the host-side Management API assertions.

## Commands

```text
dotnet run docker/run.cs -- help docker-dev
```

`docker/help.md` is the canonical command and option reference. The rest of this
page documents runtime behavior, credentials, and direct Compose use. Examples
use POSIX shell syntax; in PowerShell, replace `export NAME='value'` with
`$env:NAME = 'value'`.

Full smoke tests use `ARTICULATE_TEST_SITE_CLIENT_SECRET` (defaults are applied if unset via `Env.RequireSecret()`).

| Lane  | Image                  | HTTPS backoffice URL               | HTTP listener             |
|-------|------------------------|------------------------------------|---------------------------|
| `v17` | `articulate-local:v17` | `https://localhost:44317/umbraco/` | `http://localhost:44380/` |
| `v18` | `articulate-local:v18` | `https://localhost:44318/umbraco/` | `http://localhost:44381/` |

HTTPS ports (44317 / 44318) match the Umbraco major. HTTP ports (44380 / 44381)
avoid the Windows port range reserved for the updater orchestrator (17000-18099).
Override either with `CADDY_HTTPS_PORT` / `CADDY_HTTP_PORT`. Bare
`docker compose up` without the runner still requires the package-version
variables in `docker/docker-compose.yml`; it only falls back to compose's own
ports (18443 HTTPS / 8080 HTTP). Prefer the per-lane runner, which supplies the
package values and port isolation.

The unattended install creates this default local Docker backoffice
administrator:

- Email: `admin@localhost`
- Password: `@rticulate`
- Display name: `Jane Doe`

Use this account to sign in to either backoffice URL above. These are public,
local-development defaults. A deployed site needs different credentials. Override
the unattended user with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and
`UMBRACO_USER_PASSWORD`.

## Trust Caddy's local CA once per machine

Caddy terminates TLS with a locally generated certificate. Trust Caddy's root
CA once per machine before opening the backoffice. The Docker runner exposes
the portable entrypoint:

```sh
dotnet run docker/run.cs -- docker-ca --lane v17
```

The Docker runner selects the platform-specific certificate-store helper internally;
keep that implementation detail behind `docker-ca`. On Windows, expect a user
confirmation prompt when the Caddy root is added to the current-user trust store.

## Smoke commands

Against an already healthy stack, `smoke.mjs` supports `publish`, `confirm`,
`smoke`, and `theme`:

```sh
export UMBRACO_PUBLIC_URL='https://localhost:44317/'
node docker/smoke.mjs publish
node docker/smoke.mjs confirm
node docker/smoke.mjs publish --no-descendants
```

`confirm` is read-only and checks every child and descendant under the
Articulate root. Publication processes the root first, waits for the public
route and published-content cache, then publishes descendants.
Use `https://localhost:44318/` for the v18 lane.
Set `NODE_BIN` if `node` is not on `PATH`. On Windows, invoke the script from
PowerShell or cmd rather than passing `node.exe` through WSL or Git Bash.

The smoke client bypasses certificate validation for loopback and RFC1918
private IPv4 hosts used by the development harness. Public hosts retain normal
certificate validation.

## LAN access

The harness remains loopback-only by default. To test the standalone editor
from another machine, set the LAN origin consistently and reset the database so
OpenIddict registers redirect URIs for that origin:

```sh
export ARTICULATE_TEST_SITE_CLIENT_SECRET='articulate-test-site-secret'
export CADDY_BIND_IP='0.0.0.0'
export CADDY_HTTPS_HOST='<LAN-IP>:44317'
export UMBRACO_PUBLIC_HOST='https://<LAN-IP>:44317'
export UMBRACO_PUBLIC_URL='https://<LAN-IP>:44317/'
export ARTICULATE_REDIRECT_URI='https://<LAN-IP>:44317/a-new/'
export ARTICULATE_LOGOUT_REDIRECT_URI='https://<LAN-IP>:44317/'
dotnet run docker/run.cs -- docker-dev --lane v17 --reset
```

Use port `44318` and `--lane v18` for the v18 lane. Browsers must accept
Caddy's development certificate.

> [!WARNING]
> LAN exposure makes the site and its fixed development credentials available
> to the local network. Never use this configuration on a public or untrusted
> network.

During first installation, Umbraco may log two warnings that an empty culture
was not found in configured localization sources. The starter package contains
valid invariant content and no language payload; these warnings are harmless
package-install noise and require no Articulate change.

## TinyMCE opt-in

Set `USE_TINYMCE_UMBRACO=true` before starting the Docker site when testing the
optional TinyMCE integration:

```sh
export USE_TINYMCE_UMBRACO='true'
dotnet run docker/run.cs -- docker-dev --lane v17
```

The runner passes the lane's `TinyMceUmbracoPackageVersion` floor from
`Directory.Packages.props` into the Docker build. The same environment variable
works with `docker-build` and `docker-test`.

## Runtime modes

The compose stack switches between two modes through `UMBRACO_RUNTIME_MODE`:

- `BackofficeDevelopment` (default) — auto-provisions the test-site API
  user + client credentials after install and migrations, then publishes and
  confirms content via `smoke.mjs`.
- `Production` — disables that bootstrap so the only content served is what
  was already published in the data volume. Use `docker-prod` to flip the
  existing stack into this mode and re-verify.

Typical flow: start with empty volumes in `BackofficeDevelopment`, publish
and confirm content, then re-run `docker-prod` against the same volumes to
confirm that published content survives a `Production`-mode restart. See the
release notes for the loopback-binding change.

## Cookie isolation between lanes

Both v17 and v18 run on the same `localhost` authority but different ports.
Browser cookies are domain-scoped (port is ignored), so the default Umbraco
back-office cookie (`UMB_UCONTEXT` in Umbraco 17 and 18, plus the new OAuth
cookies `umbAccessToken` / `umbRefreshToken` / `umbPkceCode` in v17.3+) would
normally clash and log you out of one lane when signing into the other.

`docker/run.cs` `ConfigureLane` sets two per-lane config values to fix this:

- `Umbraco__CMS__Security__AuthCookieName=UMB_UCONTEXT-{lane}` — renames the
  legacy `UMB_UCONTEXT` cookie. (`Security:AuthCookieName` is the supported
  config key; the docker harness just plumbs it through.)
- `Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName=-{lane}` — appends
  a suffix to the new OAuth cookie names per Umbraco PR #22057 (shipped in
  Umbraco 17.3+).

You stay logged into both lanes simultaneously without browser juggling.

## Test-site switches

The test-site identity is fixed for deterministic smoke and MCP credentials. Only
bootstrap enablement and the client secret are configurable per run:

| Variable                                      | Default                               | Purpose                                                                  |
|-----------------------------------------------|---------------------------------------|--------------------------------------------------------------------------|
| `ARTICULATE_TEST_SITE_ENABLED`           | `true`                                | Toggle the bootstrap service entirely.                                   |
| `ARTICULATE_TEST_SITE_CLIENT_SECRET`     | `articulate-test-site-secret`         | OAuth client secret. Consumed by `smoke.mjs`; defaults are applied if unset via `Env.RequireSecret()`. |

The unattended backoffice administrator (the human sign-in) is configured
separately via `UMBRACO_USER_NAME` / `UMBRACO_USER_EMAIL` /
`UMBRACO_USER_PASSWORD`.

## Package and container diagnostics

Package inputs come from `build/Release/<lane>` and must include Articulate and
the sample theme. Docker installs those `.nupkg` files; it does not consume
project output directly. Docker commands invoke the package runner by default;
`docker-dev --reuse-packages` skips it and reuses the existing lane output.
Same-lane builds are incremental and `--clean` is required when switching lanes.

The Docker runner resolves `UmbracoCmsPackageVersion` and
`TinyMceUmbracoPackageVersion` from `Directory.Packages.props` via
`dotnet msbuild -getProperty`, so the site uses the same dependency floors.

Umbraco startup migrations are forward-only: `UpgradeUnattended=true` applies
pending migrations, while a database newer than the running code fails startup
instead of being downgraded. To test an upgrade, start an older package/image
with `docker-dev --reset`, keep the lane's volumes, then rebuild and run
`docker-dev` without `--reset`. For the local test site, use `site --reset` only
for the baseline and then restart `site` without `--reset`; use
`build --clean --preserve-site` when build outputs need cleaning without deleting
the migration database.

Rebuilding an image does not replace an already running container. The Docker
utility uses `--force-recreate` where required. If a site still serves stale
assets, inspect the running stack and its packaged Backoffice files:

```sh
dotnet run docker/run.cs -- docker-status --lane v17
```

Use `--lane v18` for the v18 lane.

Standard smoke evidence is HTTP, DOM, state, and container-log based.
Screenshots are optional manual-review evidence.

## Umbraco MCP Dev

`@umbraco-cms/mcp-dev` is Umbraco's official Model Context Protocol server.
[Model Context Protocol (MCP)](https://modelcontextprotocol.io/) is an open
standard that lets AI clients (Claude Desktop, Codex, Cursor, etc.) call
external tools through a uniform interface. The Umbraco package authenticates
as an API user (OAuth client credentials) and exposes the Management API as
MCP tools — documents, media, data types, document types, and the rest of
the backoffice become callable through natural conversation.

The docker harness auto-provisions exactly the API user this server expects.
Configure your MCP client with:

- `UMBRACO_CLIENT_ID` = `articulate-test-site` (fixed test-site client ID)
- `UMBRACO_CLIENT_SECRET` = `ARTICULATE_TEST_SITE_CLIENT_SECRET`
- `UMBRACO_BASE_URL` = the lane's public URL (e.g. `https://localhost:44317`)

Install with the lane-matched tag (`@umbraco-cms/mcp-dev@17` for the v17 lane,
`@18` for v18). See the [Umbraco MCP documentation](https://docs.umbraco.com/umbraco-developer-mcp)
for the full tool list, permissions model, and Claude Desktop config snippet.

MCP complements `smoke.mjs`; it does not replace the deterministic publish,
state, front-end, and theme assertions used by the Docker test command.

> The `umbraco-articulate` OpenID client is **not** the right credential here.
> It is the Markdown Editor's browser-side OAuth client (see
> [Markdown Editor Authentication](https://github.com/Shazwazza/Articulate/wiki/Markdown-Editor-Authentication)),
> not an API user client-credentials identity.
