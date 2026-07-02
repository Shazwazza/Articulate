# Local Docker Site

`docker-compose.yml` defines the containers. Cross-platform orchestration lives in the .NET 10
file-based app at `build/build.cs`; `smoke.mjs` contains the Management API assertions.
Run `dotnet run --file build/build.cs -- help <command>` for canonical option
defaults and requirements.

## Commands

```text
dotnet run --file build/build.cs -- docker-build --lane v17
dotnet run --file build/build.cs -- docker-dev --lane v17
dotnet run --file build/build.cs -- docker-prod --lane v17
dotnet run --file build/build.cs -- docker-status --lane v17
dotnet run --file build/build.cs -- docker-test --lane all
dotnet run --file build/build.cs -- docker-ca
```

- `docker-build [--tag image:tag]` — build the standalone chiseled Docker image.
  Defaults the tag to `articulate-local:<lane>`.
- `docker-dev` — boot the compose stack in `BackofficeDevelopment` mode, wait
  for Umbraco, then publish and confirm Articulate content via the Management
  API.
- `docker-prod` — restart the existing lane stack in `Production` mode and
  re-verify that already-published content serves without the dev automation
  bootstrap. Run after `docker-dev` against the same volumes.
- `docker-test --lane v17|v18|all` — full validation: rebuild the image,
  install, migrate, run `smoke.mjs`, and exercise the Backoffice and theme
  routes. Use `--keep` to leave successful stacks running; `--skip-smoke` for
  faster build validation only.
- `docker-status` — show the lane's running containers and the packaged
  Backoffice files copied into the site image.
- `docker-ca` — export and trust the local Caddy root CA through the build
  runner.

Options:

- `docker-dev --lane v17|v18`: ensure packages, build, boot, publish, and confirm.
- `docker-dev --skip-smoke`: boot and readiness only.
- `docker-dev --reset`: remove volumes first.
- `docker-status --lane v17|v18`: show lane containers and verify packaged
  Backoffice files inside the running site.
- `docker-test --lane v17|v18|all`: choose lanes.
- `docker-test --keep`: leave successful stacks running.
- `docker-test --skip-smoke`: build, install, migrate, and check `/umbraco/`.

Full smoke tests require `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`.

| Lane  | Image                  | HTTPS backoffice URL               | HTTP listener             |
|-------|------------------------|------------------------------------|---------------------------|
| `v17` | `articulate-local:v17` | `https://localhost:44317/umbraco/` | `http://localhost:44380/` |
| `v18` | `articulate-local:v18` | `https://localhost:44318/umbraco/` | `http://localhost:44381/` |

HTTPS ports (44317 / 44318) match the Umbraco major. HTTP ports (44380 / 44381)
sit out of common dev-tool port-snatch ranges — Windows reserves 17000-18099
for the updater orchestrator and several dev tools grab ports in that span.
Override either with `CADDY_HTTPS_PORT` / `CADDY_HTTP_PORT`. Bare
`docker compose up` without `build.cs` falls back to compose's own defaults
(18443 HTTPS / 8080 HTTP); the per-lane script overrides those.

The unattended install creates this default local Docker backoffice
administrator:

- Email: `admin@localhost`
- Password: `@rticulate`
- Display name: `Jane Doe`

Use this account to sign in to either backoffice URL above. These are public,
local-development defaults. Do not reuse them in a deployed site. Override the
unattended user with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and
`UMBRACO_USER_PASSWORD`.

## Trust Caddy's local CA once per machine

Caddy terminates TLS with a locally generated certificate. Trust Caddy's root
CA once per machine before opening the backoffice. The build runner exposes the
portable entrypoint:

```powershell
dotnet run --file build/build.cs -- docker-ca
```

If you prefer to call the platform helper directly, use:

- Windows / PowerShell 7+: `pwsh -ExecutionPolicy Bypass -File build/docker-site/Trust-CaddyRootCA.ps1`
- Windows PowerShell 5: `powershell -ExecutionPolicy Bypass -File build/docker-site/Trust-CaddyRootCA.ps1`
- Linux/WSL: `sudo build/docker-site/trust-caddy-root-ca.sh`

Those helpers remain platform-specific because certificate stores are platform-specific.

## Smoke commands

Against an already healthy stack, `smoke.mjs` supports `publish`, `confirm`,
`smoke`, and `theme`:

```powershell
node build/docker-site/smoke.mjs publish
node build/docker-site/smoke.mjs confirm
node build/docker-site/smoke.mjs publish --no-descendants
```

`confirm` is read-only. Publication processes the Articulate root first, waits
for the public route and published-content cache, then publishes descendants.
Set `NODE_BIN` if `node` is not on `PATH`. On Windows, invoke the script from
PowerShell or cmd rather than passing `node.exe` through WSL or Git Bash.

The smoke client bypasses certificate validation for loopback and RFC1918
private IPv4 hosts used by the development harness. Public hosts retain normal
certificate validation.

## LAN access

The harness remains loopback-only by default. To test the standalone editor
from another machine, set the LAN origin consistently and reset the database so
OpenIddict registers redirect URIs for that origin:

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
$env:CADDY_BIND_IP='0.0.0.0'
$env:CADDY_HTTPS_HOST='<LAN-IP>:44317'
$env:UMBRACO_PUBLIC_HOST='https://<LAN-IP>:44317'
$env:UMBRACO_PUBLIC_URL='https://<LAN-IP>:44317/'
$env:ARTICULATE_REDIRECT_URI='https://<LAN-IP>:44317/a-new/'
$env:ARTICULATE_LOGOUT_REDIRECT_URI='https://<LAN-IP>:44317/'
dotnet run --file build/build.cs -- docker-dev --lane v17 --reset
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

## Runtime modes

The compose stack switches between two modes through `UMBRACO_RUNTIME_MODE`:

- `BackofficeDevelopment` (default) — auto-provisions the dev automation API
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
back-office cookie (`UMB_UCONTEXT` in v17.4 / v18.0.0-rc3, plus the new OAuth
cookies `umbAccessToken` / `umbRefreshToken` / `umbPkceCode` in v17.3+) would
normally clash and log you out of one lane when signing into the other.

`build/build.cs` `ConfigureLane` sets two per-lane config values to fix this:

- `Umbraco__CMS__Security__AuthCookieName=UMB_UCONTEXT-{lane}` — renames the
  legacy `UMB_UCONTEXT` cookie. (`Security:AuthCookieName` is the supported
  config key; the docker harness just plumbs it through.)
- `Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName=-{lane}` — appends
  a suffix to the new OAuth cookie names per Umbraco PR #22057 (shipped in
  Umbraco 17.3+).

You stay logged into both lanes simultaneously without browser juggling.

## Dev automation user overrides

The auto-provisioned API user takes its defaults from `docker-compose.yml` and
the `ArticulateDevAutomationBootstrapper` service. Override per-run with
environment variables:

| Variable                                      | Default                               | Purpose                                                                  |
|-----------------------------------------------|---------------------------------------|--------------------------------------------------------------------------|
| `ARTICULATE_DEV_AUTOMATION_ENABLED`           | `true`                                | Toggle the bootstrap service entirely.                                   |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_ID`         | `articulate-dev-automation`           | OAuth client ID used by `smoke.mjs` and MCP clients.                     |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`     | `articulate-dev-local-secret`         | OAuth client secret. Required by `smoke.mjs`; export before invoking it. |
| `ARTICULATE_DEV_AUTOMATION_USER_NAME`         | `articulate-dev-automation`           | Backoffice user name to provision.                                       |
| `ARTICULATE_DEV_AUTOMATION_USER_EMAIL`        | `articulate-dev-automation@localhost` | Backoffice user email.                                                   |
| `ARTICULATE_DEV_AUTOMATION_USER_DISPLAY_NAME` | `Articulate Dev Automation`           | Backoffice display name.                                                 |
| `ARTICULATE_DEV_AUTOMATION_USER_GROUP_ALIAS`  | `admin`                               | User-group alias granting management access.                             |

The unattended backoffice administrator (the human sign-in) is configured
separately via `UMBRACO_USER_NAME` / `UMBRACO_USER_EMAIL` /
`UMBRACO_USER_PASSWORD`.

## Package and container diagnostics

Package inputs come from `build/Release/<lane>` and must include Articulate and
the sample theme. Docker installs those `.nupkg` files; it does not consume
project output directly. Regenerate packages after changing packaged
dependencies, client assets, or static assets.

Rebuilding an image does not replace an already running container. The build
utility uses `--force-recreate` where required. If a site still serves stale
assets, inspect the running stack and its packaged Backoffice files:

```powershell
dotnet run --file build/build.cs -- docker-status --lane v17
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

- `UMBRACO_CLIENT_ID` = `ARTICULATE_DEV_AUTOMATION_CLIENT_ID` (= `articulate-dev-automation`)
- `UMBRACO_CLIENT_SECRET` = `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`
- `UMBRACO_BASE_URL` = the lane's public URL (e.g. `https://localhost:44317`)

Install with the lane-matched tag (`@umbraco-cms/mcp-dev@17` for the v17 lane,
`@18` for v18). See the [Umbraco MCP documentation](https://docs.umbraco.com/umbraco-developer-mcp)
for the full tool list, permissions model, and Claude Desktop config snippet.

MCP complements `smoke.mjs`; it does not replace the deterministic publish,
state, front-end, and theme assertions used by the Docker test command.

> The `umbraco-articulate` OpenID client is **not** the right credential here.
> It is the Markdown Editor's browser-side OAuth client (see
> [Markdown editor authentication](../docs/configuration.md#markdown-editor-authentication)),
> not an API user client-credentials identity.
