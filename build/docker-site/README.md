# Local Docker Site

All Docker workflows use `docker-compose.yml` at the repo root as the authoritative container definition.
No `docker run` is used directly. Lane and port parameters are passed via environment variables.

## Scripts

| Script | Purpose |
|--------|---------|
| `run-dev.ps1` / `run-dev.sh` | Phase 1: start compose in BackofficeDevelopment mode, wait for Umbraco, publish and confirm Articulate content |
| `run-prod-smoke.ps1` / `run-prod-smoke.sh` | Phase 2: force-recreate compose in Production mode, run smoke + theme checks |
| `test.ps1` / `test.sh` | Multi-lane orchestrator: runs Phase 1 + Phase 2 for umbraco17 and/or umbraco18 on isolated ports |
| `smoke.mjs` | Management API smoke runner (publish / confirm / smoke / theme modes) |
| `Trust-CaddyRootCA.ps1` / `trust-caddy-root-ca.sh` | Trust Caddy's local CA in the system store (run once per machine) |
| `docker-build.ps1` / `docker-build.sh` | Standalone image build helper (wraps `docker buildx build`) |

## Quick start — dev workflow (single lane)

Build packages first, then start the stack. The dev script waits for the unattended install to finish,
then publishes Articulate content via the Management API.

```powershell
# Build the legacy lane
$env:ARTICULATE_PACKAGE_LANE = 'legacy'; $env:PACK_SAMPLE_THEME = 'true'; ./build/build.ps1

# Start dev stack (BackofficeDevelopment + publish/confirm)
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'
.\build\docker-site\run-dev.ps1

# Production smoke on same volume set
.\build\docker-site\run-prod-smoke.ps1
```

```bash
ARTICULATE_PACKAGE_LANE=legacy PACK_SAMPLE_THEME=true ./build/build.sh

ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
  ./build/docker-site/run-dev.sh

./build/docker-site/run-prod-smoke.sh
```

The stack exposes Caddy at `https://localhost:18443/`. OpenIddict requires HTTPS — Production mode
rejects plain HTTP authorize requests with `ID2083`.

Trust the local CA once per machine:

- **Windows:** `powershell -ExecutionPolicy Bypass -File .\build\docker-site\Trust-CaddyRootCA.ps1`
- **Linux/WSL:** `sudo ./build/docker-site/trust-caddy-root-ca.sh`

## Multi-lane testing (`test.ps1` / `test.sh`)

Runs both umbraco17 and umbraco18 lanes end-to-end. Each lane uses its own compose project, volume
prefix, and port pair so both can stay alive simultaneously when `--keep` is used.

| Lane | Image | HTTPS port |
|------|-------|-----------|
| `umbraco17` | `articulate-local:umbraco17` | `17017` |
| `umbraco18` | `articulate-local:umbraco18` | `18018` |

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'

# Test both lanes; leave containers running for inspection
pwsh -File build/docker-site/test.ps1 -Target all -Keep

# Test one lane
pwsh -File build/docker-site/test.ps1 -Target umbraco18 -Keep

# Skip smoke steps (fast boot validation only)
pwsh -File build/docker-site/test.ps1 -Target all -SkipSmoke
```

```bash
ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
  ./build/docker-site/test.sh all --keep

./build/docker-site/test.sh umbraco18 --keep
./build/docker-site/test.sh all --skip-smoke
```

The orchestrator calls `run-dev` then `run-prod-smoke` per lane and runs `docker compose down -v`
after each lane unless `--keep` is set.

## Compose environment variables

`docker-compose.yml` supplies defaults. Override any variable before calling compose scripts.

| Variable | Default | Purpose |
|----------|---------|---------|
| `PACKAGE_SOURCE` | `build/Release` | NuGet package folder inside the repo |
| `UMBRACO_CMS_VERSION` | `[17.4.0,18.0.0)` | Umbraco version constraint used in Docker build |
| `TARGET_FRAMEWORK` | `net10.0` | .NET TFM for the Docker build |
| `IMAGE_TAG` | `articulate-local:chiseled` | Docker image tag |
| `COMPOSE_PROJECT_NAME` | *(directory name)* | Compose project; set per lane to isolate containers |
| `COMPOSE_VOLUME_PREFIX` | `articulate` | Prefix for named Umbraco data/media volumes |
| `CADDY_HTTPS_PORT` | `18443` | Host HTTPS port Caddy binds |
| `CADDY_HTTP_PORT` | `8080` | Host HTTP port Caddy binds |
| `UMBRACO_PUBLIC_URL` | `https://localhost:18443/` | Public URL passed to Umbraco and smoke scripts |
| `UMBRACO_RUNTIME_MODE` | `BackofficeDevelopment` | Umbraco runtime mode |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` | *(required for smoke)* | Dev automation API client secret |
| `ARTICULATE_REDIRECT_URI` | `https://localhost:18443/a-new/` | OpenIddict redirect URI for Markdown Editor client |
| `ARTICULATE_LOGOUT_REDIRECT_URI` | `https://localhost:18443/` | OpenIddict post-logout redirect URI |

## Volumes and cleanup

Named volumes follow the `${COMPOSE_VOLUME_PREFIX}_media` / `${COMPOSE_VOLUME_PREFIX}_db` pattern.
Volumes persist between Phase 1 and Phase 2 (SQLite DB carries published content). Tear down
volumes with:

```bash
docker compose down -v
# or with a specific project:
docker compose -p art_umbraco17 down -v
```

## Notes

- The Docker image builds from NuGet packages in `build/<Configuration>`, not from project output.
  Both `Articulate` and `Articulate.Theme.Sample` packages must be present. Build with
  `PACK_SAMPLE_THEME=true` to include the sample theme package.
- Default unattended backoffice credentials: Name `Jane Doe`, Email `admin@localhost`, Password `@rticulate`.
  Override with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, `UMBRACO_USER_PASSWORD`.
- The dev automation bootstrap registers a client (`articulate-dev-automation`) at startup.
  Secret defaults to `articulate-dev-local-secret`; override with `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`.
- Smoke helpers bypass TLS validation only for loopback HTTPS URLs (`localhost`, `127.0.0.1`, `::1`).
- `smoke.mjs` modes: `publish`, `confirm`, `smoke`, `theme`. Run directly when a healthy container
  is already up: `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET=... node build/docker-site/smoke.mjs publish`
