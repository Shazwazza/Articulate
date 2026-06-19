# Local Docker Site

`docker-compose.yml` defines the containers. Cross-platform orchestration lives in the .NET 10
file-based app at `build/build.cs`; `smoke.mjs` contains the Management API assertions.

## Commands

```text
dotnet run --file build/build.cs -- docker-build --lane v17
dotnet run --file build/build.cs -- docker-dev --lane v17
dotnet run --file build/build.cs -- docker-prod --lane v17
dotnet run --file build/build.cs -- docker-status --lane v17
dotnet run --file build/build.cs -- docker-test --lane all
```

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

| Lane  | Image                    | Backoffice URL                       |
|-------|--------------------------|--------------------------------------|
| `v17` | `articulate-local:v17`   | `https://localhost:17017/umbraco/`   |
| `v18` | `articulate-local:v18`   | `https://localhost:18018/umbraco/`   |

The unattended install creates this default local Docker backoffice
administrator:

- Email: `admin@localhost`
- Password: `@rticulate`
- Display name: `Jane Doe`

Use this account to sign in to either backoffice URL above. These are public,
local-development defaults. Do not reuse them in a deployed site. Override the
unattended user with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and
`UMBRACO_USER_PASSWORD`.

Trust Caddy's local CA once per machine:

- Windows: `powershell -ExecutionPolicy Bypass -File build/docker-site/Trust-CaddyRootCA.ps1`
- Linux/WSL: `sudo build/docker-site/trust-caddy-root-ca.sh`

Those helpers remain platform-specific because certificate stores are platform-specific.

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

The smoke client bypasses certificate validation only for loopback HTTPS hosts
(`localhost`, `127.0.0.1`, and `::1`) used by Caddy's local certificate.
Non-loopback URLs use normal certificate validation.

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

For interactive inspection, `@umbraco-cms/mcp-dev` can reuse the dev automation API user's
credentials:

- `UMBRACO_CLIENT_ID` = `ARTICULATE_DEV_AUTOMATION_CLIENT_ID`
- `UMBRACO_CLIENT_SECRET` = `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`
- `UMBRACO_BASE_URL` = the lane's public URL

Do not use the `umbraco-articulate` OpenID client: that is the Markdown Editor's browser client,
not an API user client-credentials identity. MCP complements `smoke.mjs`; it does not replace the
deterministic publish, state, front-end, and theme assertions used by the Docker test command.
