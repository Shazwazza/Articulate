# Local Docker Site

The repository has two local Docker validation paths:

- `build/docker-lane.ps1 -Lane legacy -Action up` or `build/docker-lane.sh legacy up` starts an Umbraco 17 direct HTTP smoke container from the Articulate 6 package lane at `http://localhost:18017/umbraco`.
- `build/docker-lane.ps1 -Lane umbraco18 -Action up` or `build/docker-lane.sh umbraco18 up` starts an Umbraco 18 direct HTTP smoke container from the Articulate 7 package lane at `http://localhost:18018/umbraco`.
- `build/docker-lane.ps1 -Lane legacy -Action compose-up` or `build/docker-lane.sh legacy compose-up` starts the Umbraco 17 lane behind Caddy at `https://localhost:18443/umbraco`.
- `build/docker-lane.ps1 -Lane umbraco18 -Action compose-up` or `build/docker-lane.sh umbraco18 compose-up` starts the Umbraco 18 lane behind Caddy at `https://localhost:18443/umbraco`.

Use direct HTTP mode for fast package/install smoke checks only. In production mode, the backoffice OpenID Connect authorize endpoint requires HTTPS and rejects HTTP requests with OpenIddict `ID2083`.

Build the package lane before running Docker:

Running the local build script once produces one lane only. Run it once with `ARTICULATE_PACKAGE_LANE=legacy` and once with `ARTICULATE_PACKAGE_LANE=umbraco18` when you need both NuGet package sets locally.

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='legacy'
$env:ARTICULATE_PACKAGE_VERSION='6.0.0-rc.2'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1

pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-up
```

```powershell
$env:ARTICULATE_PACKAGE_LANE='umbraco18'
$env:ARTICULATE_PACKAGE_VERSION='7.0.0-rc.2'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1

pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action compose-up
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=legacy ARTICULATE_PACKAGE_VERSION=6.0.0-rc.2 PACK_SAMPLE_THEME=true ./build/build.sh
./build/docker-lane.sh legacy compose-up
```

```bash
ARTICULATE_PACKAGE_LANE=umbraco18 ARTICULATE_PACKAGE_VERSION=7.0.0-rc.2 PACK_SAMPLE_THEME=true ./build/build.sh
./build/docker-lane.sh umbraco18 compose-up
```

The Docker image builds from packaged NuGet artifacts in `build/<Configuration>/<Lane>`, not directly from project output. Keep `Articulate` and `Articulate.Theme.Sample` at the same package version in each lane folder. The Dockerfile ignores `.snupkg` files and theme packages when selecting the Articulate package version.

## Local HTTPS (Compose + Caddy)

The Compose path uses Caddy to terminate TLS for a single local Umbraco container at a time. The wrapper commands pass the selected package lane, Umbraco version, HTTPS public URLs, image tag, unattended user email, and lane-specific volume prefix to Compose.

The default `Caddyfile` uses `tls internal`, which generates a local CA and a server certificate. Browsers on Windows will show a TLS error until the local CA is trusted.

## Windows (PowerShell)

From the repo root:

1. Start containers:
   - `pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-up`
   - or `pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action compose-up`

2. Trust Caddy's local root CA (Current User by default):
   - `powershell -ExecutionPolicy Bypass -File .\build\docker-site\Trust-CaddyRootCA.ps1`

If you need machine-wide trust (admin required):

- `powershell -ExecutionPolicy Bypass -File .\build\docker-site\Trust-CaddyRootCA.ps1 -Scope LocalMachine`

Restart your browser and open `https://localhost:18443/umbraco`.

- The script runs on the host and uses `docker cp` to export Caddy's internal root CA from the running `caddy` container.
- No bind mounts are required for certificate export.

## Debian/Ubuntu/WSL

From the repo root:

1. Start containers:
   - `./build/docker-lane.sh legacy compose-up`
   - or `./build/docker-lane.sh umbraco18 compose-up`

2. Trust Caddy's local root CA (system store; requires sudo):
   - `./build/docker-site/trust-caddy-root-ca.sh`

3. Run the repeatable dev/prod smoke helpers:

   | Shell | Dev stack (publish/verify) | Prod smoke test |
   |---|---|---|
   | Bash / WSL | `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET=... ./build/docker-site/run-dev.sh` | `UMBRACO_RUNTIME_MODE=Production ./build/docker-site/run-prod-smoke.sh` |
   | PowerShell | `$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='...'; .\build\docker-site\run-dev.ps1` | `$env:UMBRACO_RUNTIME_MODE='Production'; .\build\docker-site\run-prod-smoke.ps1` |

   - `run-dev` flags:
     - Bash: `RESET_DOCKER_VOLUMES=true ./build/docker-site/run-dev.sh`
     - PowerShell: `.\build\docker-site\run-dev.ps1 -ResetDockerVolumes`
   - `run-dev` skips publish:
     - Bash: `SKIP_PUBLISH=true ./build/docker-site/run-dev.sh`
     - PowerShell: `.\build\docker-site\run-dev.ps1 -SkipPublish`

   Note: `run-prod-smoke` will set sensible defaults for `UMBRACO_PUBLIC_HOST` and `UMBRACO_PUBLIC_URL` if they are not provided. If running on a non-Linux host or using `host.docker.internal`, set `UMBRACO_PUBLIC_HOST`/`UMBRACO_PUBLIC_URL` explicitly before running the smoke script.
   The intended order is dev first, production second: dev mode bootstraps automation credentials and publishes/verifies content, then the production smoke recreates the container in `Production` and verifies that already-published content serves without automation bootstrap.
   The smoke helpers bypass TLS validation only for loopback HTTPS URLs (`localhost`, `127.0.0.1`, `::1`) used by Caddy's local certificate. Non-loopback URLs use normal certificate validation.

4. Run the smoke helper directly when you already have a healthy dev container:
   - `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET=... node build/docker-site/smoke.mjs publish`
   - `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET=... node build/docker-site/smoke.mjs confirm` (read-only, verifies content is published)
   - `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET=... node build/docker-site/smoke.mjs publish --no-descendants` (root only)
   - The helper publishes the Articulate root first, waits for the public root while the published-content cache catches up, then publishes descendants.

## Notes

- This is unrelated to NTLM/SMB hardening; it is normal browser PKI behavior.
- If your team policy disallows installing a local root CA, you will need a publicly trusted certificate/domain for local development.
- Default unattended backoffice credentials for the Docker site are:
  - Name: `Jane Doe`
  - Email: `admin17@localhost` for the legacy lane, or `admin18@localhost` for the Umbraco 18 lane
  - Password: `@rticulate`
- Override those defaults with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and `UMBRACO_USER_PASSWORD` before starting the stack if needed.
- The smoke wrapper scripts require Docker and Node.js. They resolve Node from `PATH`, then `mise which node`; if Node is installed somewhere else, set `NODE_BIN` to the executable path. On Windows, run `node build/docker-site/smoke.mjs ...` directly from PowerShell/cmd rather than pointing WSL/Git Bash at `node.exe`; that path does not reliably preserve environment variables.
- The Docker stack also bootstraps a dev-only automation API user and client credentials on startup.
  - Client id: `articulate-dev-automation`
  - Secret: defaults to `articulate-dev-local-secret` in `docker-compose.yml`; override with `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`.
  - Defaults: `ARTICULATE_DEV_AUTOMATION_ENABLED=true`, `ARTICULATE_DEV_AUTOMATION_USER_GROUP_ALIAS=admin`
  - Override with `ARTICULATE_DEV_AUTOMATION_CLIENT_ID`, `ARTICULATE_DEV_AUTOMATION_USER_NAME`, `ARTICULATE_DEV_AUTOMATION_USER_EMAIL`, and `ARTICULATE_DEV_AUTOMATION_USER_DISPLAY_NAME` if needed
- Select the .NET line with compose env vars:
  - `TARGET_FRAMEWORK=net10.0` for Umbraco 17 (Umbraco version range is centralized in `Directory.Build.props`)
  - Keep `DOTNET_SDK_VERSION=10.0` unless `global.json` changes; set `DOTNET_ASPNET_VERSION` to match the runtime line (e.g. `10.0`).
- Select the runtime mode with `UMBRACO_RUNTIME_MODE`:
  - `BackofficeDevelopment` for the dev benchmark path so automation credentials can be bootstrapped before publish/verify scripts run
  - `Production` for the production-style smoke test so no dev-only bootstrap runs
- The script entry points are:
  - `build/docker-site/run-dev.sh` / `run-dev.ps1` (dev stack + publish + confirm)
  - `build/docker-site/run-prod-smoke.sh` / `run-prod-smoke.ps1` (prod stack + smoke + theme switch check)
  - `build/docker-site/smoke.mjs` (cross-platform Management API publish/confirm/smoke, no shell duals)
- Standard smoke evidence is HTTP/DOM/log based. Screenshots are ad hoc evidence for manual review, not part of the normal test path.
- The Docker image builds from packaged NuGet artifacts in `build/<Configuration>/<Lane>`, not directly from project output.
- Regenerate the package inputs after client/static asset or packaged dependency changes:
  - `dotnet pack src/Articulate.Web/Articulate.Web.csproj -c Release`
  - `dotnet pack src/Articulate.Theme.Sample/Articulate.Theme.Sample.csproj -c Release`
- Alternatively, run the repo build script with `PACK_SAMPLE_THEME=true` to produce both packages for Docker validation.
- The Dockerfile ignores `.snupkg` files and theme packages when selecting the Articulate package version.
- Only one Compose HTTPS lane can run at a time because both lanes bind `18443`. Stop the active lane with `compose-down` before switching:
  - `pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-down`
  - `pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action compose-down`
- Compose uses lane-specific Umbraco data/media volumes via `COMPOSE_VOLUME_PREFIX`, so the v17 and v18 databases are not reused across lanes.
- Rebuilding the image is not enough on its own. A running Compose service can stay on an older container/image. Prefer:
  - `pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-up`
  - or `pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action compose-up`
- Or run the two steps explicitly:
  - `pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-up`
  - or `pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action compose-up`
- Or run the two steps explicitly:
  - `pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-build`
  - `pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-up`
- `articulate-local:umbraco17` and `articulate-local:umbraco18` are the lane image tags. Compose container names include the lane project name, for example `articulate-legacy-articulate-1`.
- If the Docker back office still serves older JavaScript, check the running container rather than only the image:
  - `docker compose -p articulate-legacy ps`
  - `docker compose -p articulate-umbraco18 ps`
  - `docker exec articulate-legacy-articulate-1 /bin/sh -c "find /app -path '*App_Plugins/Articulate/BackOffice/articulate-backoffice.js' -o -path '*App_Plugins/Articulate/umbraco-package.json'"`
  - `Invoke-WebRequest https://localhost:18443/App_Plugins/Articulate/BackOffice/articulate-backoffice.js -SkipCertificateCheck`
