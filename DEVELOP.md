# Articulate Development

## Requirements

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
dotnet run --file build/build.cs -- build --configuration Debug --client true
```

Bash:

```bash
dotnet run --file build/build.cs -- build --configuration Debug --client true
```

This restores NuGet and npm packages, builds the Back Office client, builds the theme and Markdown editor dist bundles, builds the .NET solution, and produces NuGet packages.

1. Open `src/Articulate.sln`.
2. Set `Articulate.Tests.Website` as the startup project.
3. Start `Articulate.Tests.Website` and complete the Umbraco installer.
4. The Articulate package migrations will run and install the required schema and content items.
   - **Tip:** The test site defaults to the Umbraco 17 lane. Pass `-p:ArticulatePackageLane=v18` to run Umbraco 18.

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

Install once from `src/Articulate.Web/Client`, then work in the required lane:

```bash
pnpm install
cd v17 # or v18
pnpm run build
pnpm run generate:api
```

`pnpm run build` runs `tsc && vite build`; the Vite sidecar also regenerates the built-in theme `assets/dist` bundles and the Markdown editor assets, not just the Back Office client.

`pnpm run generate:api` requires the matching Umbraco site to be running and regenerates that lane's typed client after API changes.

## Build And Pack

| Shell                 | Command                                     |
|-----------------------|---------------------------------------------|
| Windows, Linux, macOS | `dotnet run --file build/build.cs -- build` |

- The scripts clean, restore, build, and pack one package lane at a time. The default lane is `v17`.
- Run once with `ARTICULATE_PACKAGE_LANE=v17` and once with `ARTICULATE_PACKAGE_LANE=v18` when you need both package sets.
- The packable NuGet package is produced by `src/Articulate.Web/Articulate.Web.csproj` (`PackageId=Articulate`).
- Packages are written under `build/$(Configuration)/v17` or `build/$(Configuration)/v18`.

### Build Script Parameters

| Parameter                    | Default                               | Description                                                                                                                                                    |
|------------------------------|---------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `BUILD_CONFIGURATION`        | `Release`                             | Build configuration: `Debug` or `Release`. Debug is typical for local development.                                                                             |
| `ARTICULATE_PACKAGE_LANE`    | `v17`                                 | Package lane: `v17` (Articulate 6.1 for Umbraco 17) or `v18` (Articulate 7.0 for Umbraco 18).                                                                  |
| `ARTICULATE_PACKAGE_VERSION` | Calculated                            | Optional explicit package-version override. Normally v17 comes from NBGV and v18 comes from `build/v18-version.txt` plus NBGV development metadata.            |
| `ENABLE_CLIENT_BUILD`        | `true` (CI/Release) / `false` (Debug) | Enable TypeScript Back Office client build (Vite + tsc). Release builds enable by default; disable for faster local iteration.                                 |
| `RUN_TESTS`                  | `true` (CI) / `false` (local)         | Run dotnet test after build. Enabled in CI; disabled by default locally.                                                                                       |
| `PACK_SAMPLE_THEME`          | `true` (local) / `false` (CI)         | Pack `Articulate.Theme.Sample` NuGet package. Local builds include by default; CI skips unless explicitly set.                                                 |
| `SKIP_CLEAN`                 | `false`                               | Reuse build outputs from the same lane. Cleaning is the default because both lanes write to the same Backoffice asset path. Do not use between lanes or in CI. |
| `MAXCPU`                     | *(auto-detected)*                     | Limit parallel restore to N MSBuild nodes. Build and pack remain sequential.                                                                                   |

**Common build commands:**

v17 release with client build:

```powershell
$env:ARTICULATE_PACKAGE_LANE='v17'
$env:ENABLE_CLIENT_BUILD='true'
$env:BUILD_CONFIGURATION='Release'
dotnet run --file build/build.cs -- build
```

Umbraco 18 lane package (pre-release):

```powershell
$env:ENABLE_CLIENT_BUILD='true'
$env:BUILD_CONFIGURATION='Release'
dotnet run --file build/build.cs -- build --lane v18
```

Local debug build (no client rebuild, no tests):

```powershell
$env:BUILD_CONFIGURATION='Debug'
dotnet run --file build/build.cs -- build
```

CI / Release build (all lanes):

```powershell
$env:BUILD_CONFIGURATION='Release'
$env:ENABLE_CLIENT_BUILD='true'
$env:RUN_TESTS='true'
# Run once per lane; each run cleans shared outputs first.
dotnet run --file build/build.cs -- build --lane v17
dotnet run --file build/build.cs -- build --lane v18
```

### Package Lanes

The source tree supports two package lanes:

| Lane  | Package line     | Umbraco support | Target frameworks | Output folder       |
|-------|------------------|-----------------|-------------------|---------------------|
| `v17` | Articulate 6.1.x | Umbraco 17      | `net10.0`         | `build/Release/v17` |
| `v18` | Articulate 7.0.x | Umbraco 18      | `net10.0`         | `build/Release/v18` |

The lanes produce separate NuGet packages because the compiled Umbraco 17 and Umbraco 18 extension points are not binary-compatible. Do not install an Articulate 6 package into Umbraco 18, or an Articulate 7 package into Umbraco 17.

`version.json` defines the Articulate 6.1 version through NBGV.
`build/v18-version.txt` defines the Articulate 7 base version. Build scripts
append NBGV development metadata when present, so `6.1.0-gabcdef` and a v18
base of `7.0.0-rc.1` produce `7.0.0-rc.1.gabcdef`. Change the text file to
advance the v18 release candidate; callers do not normally pass a version.

Build the Articulate 6 lane:

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='v17'
$env:PACK_SAMPLE_THEME='true'
dotnet run --file build/build.cs -- build
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=v17 \
PACK_SAMPLE_THEME=true \
dotnet run --file build/build.cs -- build
```

Build the Articulate 7 / Umbraco 18 lane:

PowerShell:

```powershell
$env:PACK_SAMPLE_THEME='true'
dotnet run --file build/build.cs -- build --lane v18
```

Bash:

```bash
PACK_SAMPLE_THEME=true \
dotnet run --file build/build.cs -- build --lane v18
```

## Local Docker Validation

Docker is a local validation tool. GitHub Actions builds package artifacts but does not run Docker.

`docker-compose.yml` at the repo root is the authoritative container definition. All Docker workflows set environment variables and call `docker compose`; no `docker run` is used directly.

The Caddy reverse proxy terminates TLS and exposes the stack at `https://localhost:18443/` by default. OpenIddict requires HTTPS — direct HTTP containers cannot complete the backoffice authorize flow in Production mode.

### Dev workflow (single lane)

Build packages for the lane first, then start the compose stack. The dev script waits for Umbraco to finish the unattended install, then publishes Articulate content via the Management API.

Articulate 6.1 / Umbraco 17:

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='v17'
$env:PACK_SAMPLE_THEME='true'
dotnet run --file build/build.cs -- build --lane v17 --sample

$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
dotnet run --file build/build.cs -- docker-dev
```

Bash:

```bash
dotnet run --file build/build.cs -- build --lane v17 --sample

ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
dotnet run --file build/build.cs -- docker-dev
```

Articulate 7 / Umbraco 18:

```powershell
$env:PACK_SAMPLE_THEME='true'
dotnet run --file build/build.cs -- build --lane v18 --sample

$env:UMBRACO_CMS_VERSION='[18.0.0-*,19.0.0)'
$env:IMAGE_TAG='articulate-local:v18'
$env:PACKAGE_SOURCE='build/Release/v18'
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
dotnet run --file build/build.cs -- docker-dev
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
dotnet run --file build/build.cs -- docker-prod
```

```bash
ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
dotnet run --file build/build.cs -- docker-prod
```

### Compose env vars

All lane parameters are passed via environment variables; `docker-compose.yml` supplies defaults.

| Variable                                  | Default                     | Purpose                                         |
|-------------------------------------------|-----------------------------|-------------------------------------------------|
| `PACKAGE_SOURCE`                          | `build/Release/v17`         | NuGet package folder inside the repo            |
| `UMBRACO_CMS_VERSION`                     | `[17.4.0,18.0.0)`           | Umbraco version constraint for the Docker build |
| `TARGET_FRAMEWORK`                        | `net10.0`                   | .NET TFM for the Docker build                   |
| `IMAGE_TAG`                               | `articulate-local:chiseled` | Docker image tag                                |
| `COMPOSE_VOLUME_PREFIX`                   | `articulate`                | Prefix for named Umbraco data/media volumes     |
| `CADDY_HTTPS_PORT`                        | `18443`                     | Host port Caddy listens on for HTTPS            |
| `UMBRACO_PUBLIC_URL`                      | `https://localhost:18443/`  | Public URL passed to Umbraco and smoke scripts  |
| `UMBRACO_RUNTIME_MODE`                    | `BackofficeDevelopment`     | Umbraco runtime mode                            |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` | *(required)*                | Secret for the dev automation API client        |

## End-to-End Docker Testing (v17 & v18)

Run comprehensive Docker validation for both Umbraco versions. Tests build images, perform unattended install, and validate backoffice and frontend readiness.

**Test v17 and v18** (separate ports, isolated databases):

PowerShell:

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'
dotnet run --file build/build.cs -- docker-test --lane all --keep
# Containers stay running at https://localhost:17017/ (v17) and https://localhost:18018/ (v18)
```

Bash:

```bash
ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
dotnet run --file build/build.cs -- docker-test --lane all --keep
```

**Test individual version:**

```powershell
dotnet run --file build/build.cs -- docker-test --lane v17 --keep
# or
dotnet run --file build/build.cs -- docker-test --lane v18 --keep
```

Each test validates:

- ✅ Docker image builds (with correct Umbraco version and package lane)
- ✅ Unattended Umbraco install completes
- ✅ Backoffice `/umbraco` returns 200 OK
- ✅ Dev automation API ready for smoke tests (phase 1)
- ✅ Production mode validation ready (phase 2)

Remove `-Keep` flag to clean up containers after testing. Use `-SkipSmoke` to skip API publish/confirm tests (faster build validation).

## Opt-in Umbraco 18 validation (net10 only)

The default source validation lane targets Umbraco 17 (`[17.4.0,18.0.0)`) on `net10.0`. Pass `-p:ArticulatePackageLane=v18` to use the Umbraco 18 pre-release lane.

Run the Umbraco 17 and 18 commands sequentially, and use `-m:1` for full-solution
builds. The solution graph and both lanes share project `bin` and `obj`
directories, so default parallel MSBuild can lock compiler outputs and static
web asset caches.

OpenAPI note:

- Umbraco 17 lane uses SwaggerGen/operation filter registration.
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
dotnet restore .\src\Articulate.sln -p:ArticulatePackageLane=v18
dotnet build .\src\Articulate.sln -c Debug -f net10.0 --no-restore -m:1 -p:ArticulatePackageLane=v18
dotnet test .\src\Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:ArticulatePackageLane=v18
dotnet run -f net10.0 --project .\src\Articulate.Tests.Website\Articulate.Tests.Website.csproj -p:ArticulatePackageLane=v18
```

Bash:

```bash
dotnet restore ./src/Articulate.sln -p:ArticulatePackageLane=v18
dotnet build ./src/Articulate.sln -c Debug -f net10.0 --no-restore -m:1 -p:ArticulatePackageLane=v18
dotnet test ./src/Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:ArticulatePackageLane=v18
dotnet run -f net10.0 --project ./src/Articulate.Tests.Website/Articulate.Tests.Website.csproj -p:ArticulatePackageLane=v18
```

## Back Office Client Builds

`EnableClientBuild` defaults to `false` so Visual Studio background builds do not clash with Vite output. When you need to rebuild the client during packaging or local validation, set `ENABLE_CLIENT_BUILD=true` inline with the build command:

PowerShell:

```powershell
dotnet run --file build/build.cs -- build --client true
```

Bash:

```bash
dotnet run --file build/build.cs -- build --client true
```

## Schema And Data

If you change the underlying Umbraco schema, installed content, or media, recreate the Articulate package in the back office with its dependencies, then resave `package.zip` and commit it.
