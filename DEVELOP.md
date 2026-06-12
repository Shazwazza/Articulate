# Articulate Development

## Requirements

- .NET 9.0 SDK
- .NET 10.0 SDK
- Node.js 24+ with `corepack enable pnpm`
- Optional: Nerdbank.GitVersioning CLI (`dotnet tool install -g nbgv`), only needed for Release builds
- IDE: Visual Studio 2026, JetBrains Rider, or Visual Studio Code
- Shell: PowerShell 5+, PowerShell 7+, or Bash (WSL/Linux)

## First Run

1. Clone or fork the repository.
2. Prime the site and solution so the Back Office client extension and asset bundles are built.

PowerShell:

```powershell
$env:ENABLE_CLIENT_BUILD='true'; $env:BUILD_CONFIGURATION='Debug'; ./build/build.ps1
```

Bash:

```bash
ENABLE_CLIENT_BUILD=true BUILD_CONFIGURATION=Debug ./build/build.sh
```

This restores NuGet and npm packages, builds the Back Office client, builds the theme and Markdown editor dist bundles, builds the .NET solution, and produces NuGet packages.

3. Open `src/Articulate.sln`.
4. Set `Articulate.Tests.Website` as the startup project.
5. Start `Articulate.Tests.Website` and complete the Umbraco installer.
6. The Articulate package migrations will run and install the required schema and content items.
   - **Tip:** The test site's target framework selects the default Umbraco version: `net9.0` targets Umbraco 16 (`[16.5.1,17.0.0)`), and `net10.0` targets Umbraco 17 (`[17.4.0,18.0.0)`). Override `UmbracoCmsPackageVersion` (e.g. `[18.0.0-*,19.0.0)`) on `net10.0` to run Umbraco 18.

## Docker Modes

The Compose stack supports two explicit runtime states through `UMBRACO_RUNTIME_MODE`. The default is `BackofficeDevelopment`; switch to `Production` for the production-style check.

- `BackofficeDevelopment` (default) for local dev and agent runs. This enables the dev-only automation bootstrap so the API user and client credentials can be provisioned automatically after install and migrations.
- `Production` for the production-style check. This disables automation bootstrap and keeps the stack honest about what content was already published in the data volume.

Recommended benchmark flow:

1. Start with an empty Docker volume set in `BackofficeDevelopment` by running the dev script with `RESET_DOCKER_VOLUMES=true`.
2. Unattended install and package migrations run, automation bootstrap provisions credentials, and `build/docker-site/smoke.mjs` publishes/verifies the Articulate content tree.
3. Verify `/` returns `200` and record the timing.
4. Re-run the same volume set in `Production` to confirm the published content still serves without any dev-only automation.

- `RESET_DOCKER_VOLUMES=true` runs `docker compose down -v` before the dev script starts the stack. Use it for empty-DB QA, not for normal iterative runs.

## Client Development

From `src/Articulate.Web/Client`:

```bash
pnpm install
pnpm run build
pnpm run generate:api
```

`pnpm run build` runs `tsc && vite build`; the Vite sidecar also regenerates the built-in theme `assets/dist` bundles and the Markdown editor assets, not just the Back Office client.

`pnpm run generate:api` requires the Umbraco site to be running and regenerates the typed client after API changes.

## Build And Pack

| Shell | Command |
| --- | --- |
| Windows PowerShell | `./build/build.ps1` |
| Bash / WSL / Linux | `./build/build.sh` |

- For WSL/Linux, make the script executable first with `chmod u+x ./build/build.sh`.
- The scripts clean, restore, build, and pack one package lane at a time. The default lane is `legacy`.
- Running the local build script once does not produce both lanes; run it once with `ARTICULATE_PACKAGE_LANE=legacy` and once with `ARTICULATE_PACKAGE_LANE=umbraco18` when you need both NuGet package sets locally.
- The packable NuGet package is produced by `src/Articulate.Web/Articulate.Web.csproj` (`PackageId=Articulate`).
- Packages are written under `build/$(Configuration)`.

### Build Script Parameters

| Parameter | Default | Description |
| --- | --- | --- |
| `BUILD_CONFIGURATION` | `Release` | Build configuration: `Debug` or `Release`. Debug is typical for local development. |
| `ARTICULATE_PACKAGE_LANE` | `legacy` | Package lane: `legacy` (Articulate 6 for U16/17) or `umbraco18` (Articulate 7 for U18). The version is controlled by `version.json` for `legacy` and by `Directory.Build.props` for `umbraco18`. |
| `ENABLE_CLIENT_BUILD` | `true` (CI/Release) / `false` (Debug) | Enable TypeScript Back Office client build (Vite + tsc). Release builds enable by default; disable for faster local iteration. |
| `RUN_TESTS` | `true` (CI) / `false` (local) | Run dotnet test after build. Enabled in CI; disabled by default locally. |
| `PACK_SAMPLE_THEME` | `true` (local) / `false` (CI) | Pack `Articulate.Theme.Sample` NuGet package. Local builds include by default; CI skips unless explicitly set. |
| `FORCE_CLEAN` | `false` | Force `dotnet clean` before build. Useful to clear stale artifacts; skipped by default. |
| `MAXCPU` | *(auto-detected)* | Limit MSBuild parallelism to N CPUs. Example: `MAXCPU=4` limits to 4 parallel nodes. |

**Common build commands:**

Legacy lane (U16/17) release with client build:

```powershell
$env:ARTICULATE_PACKAGE_LANE='legacy'
$env:ENABLE_CLIENT_BUILD='true'
$env:BUILD_CONFIGURATION='Release'
./build/build.ps1
```

Umbraco 18 lane package (pre-release):

```powershell
$env:ARTICULATE_PACKAGE_LANE='umbraco18'
$env:ENABLE_CLIENT_BUILD='true'
$env:BUILD_CONFIGURATION='Release'
./build/build.ps1
```

Local debug build (no client rebuild, no tests):

```powershell
$env:BUILD_CONFIGURATION='Debug'
./build/build.ps1
```

CI / Release build (all lanes):

```powershell
$env:BUILD_CONFIGURATION='Release'
$env:ENABLE_CLIENT_BUILD='true'
$env:RUN_TESTS='true'
$env:FORCE_CLEAN='true'
# Run once per lane. The umbraco18 lane version is set in Directory.Build.props.
$env:ARTICULATE_PACKAGE_LANE='legacy'; ./build/build.ps1
$env:ARTICULATE_PACKAGE_LANE='umbraco18'; ./build/build.ps1
```

### Package Lanes

The source tree supports two package lanes:

| Lane | Package line | Umbraco support | Target frameworks | Output folder |
| --- | --- | --- | --- | --- |
| `legacy` | Articulate 6.x | Umbraco 16/17 | `net9.0`, `net10.0` | `build/Release` |
| `umbraco18` | Articulate 7.x | Umbraco 18 | `net10.0` | `build/Release` |

The lanes produce separate NuGet packages because the compiled Umbraco 17 and Umbraco 18 extension points are not binary-compatible. Do not install an Articulate 6 package into Umbraco 18, or an Articulate 7 package into Umbraco 16/17.

The Articulate 6 version is declared in `version.json` at the repo root (Nerdbank.GitVersioning). The Articulate 7 / `umbraco18` lane version is declared in `Directory.Build.props` (`ArticulatePackageVersion`). Edit that property to bump the v7 package version.

> **Why v7 is hard-coded while v6 uses NBGV:** The `umbraco18` lane reuses the same `Articulate.Web.csproj` project as the legacy lane. NBGV only supports one version per project via `version.json`, so the v7 lane overrides the computed version with an explicit MSBuild property. This is deterministic and simple, but it means v7 does not get NBGV's automatic `-g<commit>` suffix on feature branches. If you want NBGV to drive the v7 version instead, the options are:
> 1. Maintain a separate `version.json` for the lane and swap the root `version.json` during the build (more complex, relies on NBGV reading from disk in a clean checkout).
> 2. Split `Articulate.Web.csproj` into two project files so each can have its own `version.json`.
> 3. Keep v7 development on its own branch where root `version.json` is `7.x`.
>
> For now we use option 1 (explicit override) for simplicity.

Build the Articulate 6 lane:

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='legacy'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=legacy \
PACK_SAMPLE_THEME=true \
./build/build.sh
```

Build the Articulate 7 / Umbraco 18 lane:

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='umbraco18'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=umbraco18 \
PACK_SAMPLE_THEME=true \
./build/build.sh
```

## Local Docker Validation

Docker is a local validation tool. GitHub Actions builds package artifacts but does not run Docker.

`docker-compose.yml` at the repo root is the authoritative container definition. All Docker workflows set environment variables and call `docker compose`; no `docker run` is used directly.

The Caddy reverse proxy terminates TLS and exposes the stack at `https://localhost:18443/` by default. OpenIddict requires HTTPS — direct HTTP containers cannot complete the backoffice authorize flow in Production mode.

### Dev workflow (single lane)

Build packages for the lane first, then start the compose stack. The dev script waits for Umbraco to finish the unattended install, then publishes Articulate content via the Management API.

Articulate 6 / Umbraco 16 & 17 (legacy lane):

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='legacy'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1

$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
.\build\docker-site\run-dev.ps1
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=legacy PACK_SAMPLE_THEME=true ./build/build.sh

ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
./build/docker-site/run-dev.sh
```

Articulate 7 / Umbraco 18:

```powershell
$env:ARTICULATE_PACKAGE_LANE='umbraco18'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1

$env:UMBRACO_CMS_VERSION='[18.0.0-*,19.0.0)'
$env:TARGET_FRAMEWORK='net10.0'
$env:IMAGE_TAG='articulate-local:umbraco18'
$env:PACKAGE_SOURCE='build/Release'
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
.\build\docker-site\run-dev.ps1
```

The compose stack exposes Caddy at `https://localhost:18443/umbraco`. Trust the local CA once per machine:

- Windows: `powershell -ExecutionPolicy Bypass -File .\build\docker-site\Trust-CaddyRootCA.ps1`
- Linux/WSL: `sudo ./build/docker-site/trust-caddy-root-ca.sh`

The default unattended Docker backoffice credentials are:

- Name: `Jane Doe`, Email: `admin@localhost`, Password: `@rticulate`

Override with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, `UMBRACO_USER_PASSWORD`.

### Production smoke (single lane)

After the dev script finishes, run the production smoke to confirm published content survives a `Production`-mode restart:

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
.\build\docker-site\run-prod-smoke.ps1
```

```bash
ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
./build/docker-site/run-prod-smoke.sh
```

### Compose env vars

All lane parameters are passed via environment variables; `docker-compose.yml` supplies defaults.

| Variable | Default | Purpose |
| --- | --- | --- |
| `PACKAGE_SOURCE` | `build/Release` | NuGet package folder inside the repo |
| `UMBRACO_CMS_VERSION` | `[17.4.0,18.0.0)` | Umbraco version constraint for the Docker build |
| `TARGET_FRAMEWORK` | `net10.0` | .NET TFM for the Docker build |
| `IMAGE_TAG` | `articulate-local:chiseled` | Docker image tag |
| `COMPOSE_VOLUME_PREFIX` | `articulate` | Prefix for named Umbraco data/media volumes |
| `CADDY_HTTPS_PORT` | `18443` | Host port Caddy listens on for HTTPS |
| `UMBRACO_PUBLIC_URL` | `https://localhost:18443/` | Public URL passed to Umbraco and smoke scripts |
| `UMBRACO_RUNTIME_MODE` | `BackofficeDevelopment` | Umbraco runtime mode |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` | *(required)* | Secret for the dev automation API client |

## End-to-End Docker Testing (v17 & v18)

Run comprehensive Docker validation for both Umbraco versions. Tests build images, perform unattended install, and validate backoffice and frontend readiness.

**Test v17 and v18** (separate ports, isolated databases):

PowerShell:
```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'
pwsh -File build/docker-site/test.ps1 -Target all -Keep
# Containers stay running at https://localhost:17017/ (v17) and https://localhost:18018/ (v18)
```

Bash:
```bash
ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
./build/docker-site/test.sh all --keep
```

**Test individual version:**
```powershell
pwsh -File build/docker-site/test.ps1 -Target umbraco17 -Keep
# or
pwsh -File build/docker-site/test.ps1 -Target umbraco18 -Keep
```

Each test validates:
- ✅ Docker image builds (with correct Umbraco version and package lane)
- ✅ Unattended Umbraco install completes
- ✅ Backoffice `/umbraco` returns 200 OK
- ✅ Dev automation API ready for smoke tests (phase 1)
- ✅ Production mode validation ready (phase 2)

Remove `-Keep` flag to clean up containers after testing. Use `-SkipSmoke` to skip API publish/confirm tests (faster build validation).

## Opt-in Umbraco 18 validation (net10 only)

Default source validation lanes: `net9.0` targets Umbraco 16 (`[16.5.1,17.0.0)`), `net10.0` targets Umbraco 17 (`[17.4.0,18.0.0)`). Override `UmbracoCmsPackageVersion` on the command line to test against a specific Umbraco 18 pre-release.

OpenAPI note:

- Umbraco 16/17 lane uses legacy SwaggerGen/operation filter registration.
- Umbraco 18 lane uses native OpenAPI transformers for Articulate operation IDs and security requirements.

Baseline (`net10.0` + Umbraco 17 — minimum supported):

PowerShell:

```powershell
dotnet restore .\src\Articulate.sln -p:UmbracoCmsPackageVersion=17.4.0
dotnet build .\src\Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=17.4.0
dotnet test .\src\Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=17.4.0
```

Bash:

```bash
dotnet restore ./src/Articulate.sln -p:UmbracoCmsPackageVersion=17.4.0
dotnet build ./src/Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=17.4.0
dotnet test ./src/Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=17.4.0
```

Umbraco 18 pre-release (`[18.0.0-*,19.0.0)` picks up the latest available pre-release):

PowerShell:

```powershell
dotnet restore .\src\Articulate.sln -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
dotnet build .\src\Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
dotnet test .\src\Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
dotnet run -f net10.0 --project .\src\Articulate.Tests.Website\Articulate.Tests.Website.csproj -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
```

Bash:

```bash
dotnet restore ./src/Articulate.sln -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
dotnet build ./src/Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
dotnet test ./src/Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
dotnet run -f net10.0 --project ./src/Articulate.Tests.Website/Articulate.Tests.Website.csproj -p:UmbracoCmsPackageVersion=[18.0.0-*,19.0.0)
```

## Back Office Client Builds

`EnableClientBuild` defaults to `false` so Visual Studio background builds do not clash with Vite output. When you need to rebuild the client during packaging or local validation, set `ENABLE_CLIENT_BUILD=true` inline with the build command:

PowerShell:

```powershell
$env:ENABLE_CLIENT_BUILD='true'; ./build/build.ps1
```

Bash:

```bash
ENABLE_CLIENT_BUILD=true ./build/build.sh
```

## Schema And Data

If you change the underlying Umbraco schema, installed content, or media, recreate the Articulate package in the back office with its dependencies, then resave `package.zip` and commit it.
