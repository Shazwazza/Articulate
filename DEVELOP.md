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
   - **Tip:** The test site's target framework selects the Umbraco version: `net9.0` runs Umbraco 16, and `net10.0` runs Umbraco 17. Use `net10.0` only when you specifically want the v17 path.

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
- Packages are written under `build/$(Configuration)/$(ARTICULATE_PACKAGE_LANE)`.

### Build Script Parameters

| Parameter | Default | Description |
| --- | --- | --- |
| `BUILD_CONFIGURATION` | `Release` | Build configuration: `Debug` or `Release`. Debug is typical for local development. |
| `ARTICULATE_PACKAGE_LANE` | `legacy` | Package lane: `legacy` (Articulate 6 for U16/17) or `umbraco18` (Articulate 7 for U18). |
| `ARTICULATE_PACKAGE_VERSION` | *(Nerdbank.GitVersioning)* | Optional: Override version in `version.json` during pack. Supports SemVer prerelease tags (`7.0.0-beta1`, `7.0.0-rc.1`, etc). Script restores `version.json` after pack. |
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

Umbraco 18 beta package (U18) with version override:

```powershell
$env:ARTICULATE_PACKAGE_LANE='umbraco18'
$env:ARTICULATE_PACKAGE_VERSION='7.0.0-beta1'
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
# Run once per lane
$env:ARTICULATE_PACKAGE_LANE='legacy'; ./build/build.ps1
$env:ARTICULATE_PACKAGE_LANE='umbraco18'; $env:ARTICULATE_PACKAGE_VERSION='7.0.0-beta1'; ./build/build.ps1
```

### Package Lanes

The source tree supports two package lanes:

| Lane | Package line | Umbraco support | Target frameworks | Output folder |
| --- | --- | --- | --- | --- |
| `legacy` | Articulate 6.x | Umbraco 16/17 | `net9.0`, `net10.0` | `build/Release/legacy` |
| `umbraco18` | Articulate 7.x | Umbraco 18 | `net10.0` | `build/Release/umbraco18` |

The lanes produce separate NuGet packages because the compiled Umbraco 17 and Umbraco 18 extension points are not binary-compatible. Do not install an Articulate 6 package into Umbraco 18, or an Articulate 7 package into Umbraco 16/17.

Build the Articulate 6 lane:

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='legacy'
$env:ARTICULATE_PACKAGE_VERSION='6.0.0'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=legacy \
ARTICULATE_PACKAGE_VERSION=6.0.0-rc.2 \
PACK_SAMPLE_THEME=true \
./build/build.sh
```

Build the Articulate 7 / Umbraco 18 lane:

PowerShell:

```powershell
$env:ARTICULATE_PACKAGE_LANE='umbraco18'
$env:ARTICULATE_PACKAGE_VERSION='7.0.0-rc.2'
$env:PACK_SAMPLE_THEME='true'
./build/build.ps1
```

Bash:

```bash
ARTICULATE_PACKAGE_LANE=umbraco18 \
ARTICULATE_PACKAGE_VERSION=7.0.0-rc.2 \
PACK_SAMPLE_THEME=true \
./build/build.sh
```

The build scripts temporarily patch `version.json` when `ARTICULATE_PACKAGE_VERSION` is set, then restore it before exiting. This keeps Nerdbank.GitVersioning as the source of package metadata while allowing one checkout to produce the Articulate 6 and 7 package lines.

## Local Docker Validation

Docker is a local validation tool. GitHub Actions builds package artifacts but does not run Docker.

The lane wrappers have two modes:

- `up` starts a direct HTTP container. This is useful as a fast package/install boot smoke test.
- `compose-up` starts the Caddy stack with server-side HTTPS enabled. Use this for real backoffice login/auth testing.

The direct HTTP mode will not complete the backoffice OpenID Connect authorize flow in production mode; OpenIddict rejects the HTTP authorize request with `ID2083` because the server only accepts HTTPS requests.

Build and start the Umbraco 17 direct HTTP smoke container from the Articulate 6 lane:

PowerShell:

```powershell
pwsh -File build/docker-lane.ps1 -Lane legacy -Action up
```

Bash:

```bash
./build/docker-lane.sh legacy up
```

Build and start the Umbraco 18 direct HTTP smoke container from the Articulate 7 lane:

PowerShell:

```powershell
pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action up
```

Bash:

```bash
./build/docker-lane.sh umbraco18 up
```

The direct wrapper mode builds the correct image from the selected package lane and starts containers on fixed ports:

| Lane | Image | Container | URL |
| --- | --- | --- | --- |
| `legacy` | `articulate-local:umbraco17` | `articulate-umbraco17` | `http://localhost:18017/umbraco` |
| `umbraco18` | `articulate-local:umbraco18` | `articulate-umbraco18` | `http://localhost:18018/umbraco` |

The Docker wrappers expect both `Articulate` and `Articulate.Theme.Sample` packages in the selected lane folder. Rebuild with `PACK_SAMPLE_THEME=true` if the wrapper reports that the sample theme package is missing.

The default unattended Docker backoffice credentials are:

- Name: `Jane Doe`
- Password: `@rticulate`
- Email: `admin17@localhost` for the legacy lane
- Email: `admin18@localhost` for the Umbraco 18 lane

### HTTPS / Caddy Compose Path

Use `compose-up` for the full local HTTPS experience. The wrapper passes the selected package lane, Umbraco version, public HTTPS URLs, unattended user email, image tag, and lane-specific volume prefix into `docker compose`.

Articulate 6 / Umbraco 17:

PowerShell:

```powershell
pwsh -File build/docker-lane.ps1 -Lane legacy -Action compose-up
```

Bash:

```bash
./build/docker-lane.sh legacy compose-up
```

Articulate 7 / Umbraco 18:

PowerShell:

```powershell
pwsh -File build/docker-lane.ps1 -Lane umbraco18 -Action compose-up
```

Bash:

```bash
./build/docker-lane.sh umbraco18 compose-up
```

Compose exposes Caddy at `https://localhost:18443/umbraco`. It runs one selected HTTPS lane at a time because both lanes bind port `18443`; use `compose-down` for the active lane before switching. The wrapper keeps Umbraco data/media volumes separate per lane so v17 and v18 databases are not reused across lanes.

## End-to-End Docker Testing (v17 & v18)

Run comprehensive Docker validation for both Umbraco versions. Tests build images, perform unattended install, and validate backoffice and frontend readiness.

**Test v17 and v18** (separate ports, isolated databases):

PowerShell:
```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'
pwsh -File scripts/docker-test.ps1 -Target all -Keep
# Containers stay running at https://localhost:17017/ (v17) and https://localhost:18018/ (v18)
```

Bash:
```bash
ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret' \
./scripts/docker-test.sh all
```

**Test individual version:**
```powershell
pwsh -File scripts/docker-test.ps1 -Target umbraco17 -Keep
# or
pwsh -File scripts/docker-test.ps1 -Target umbraco18 -Keep
```

Each test validates:
- ✅ Docker image builds (with correct Umbraco version and package lane)
- ✅ Unattended Umbraco install completes
- ✅ Backoffice `/umbraco` returns 200 OK
- ✅ Dev automation API ready for smoke tests (phase 1)
- ✅ Production mode validation ready (phase 2)

Remove `-Keep` flag to clean up containers after testing. Use `-SkipSmoke` to skip API publish/confirm tests (faster build validation).

## Opt-in Umbraco 18 beta validation (net10 only)

Default source validation lanes remain unchanged (`net9.0` => Umbraco 16, `net10.0` => Umbraco 17 stable).

Use explicit version pinning to validate Umbraco 18 beta locally.

OpenAPI note:

- Umbraco 16/17 lane uses legacy SwaggerGen/operation filter registration.
- Umbraco 18 lane uses native OpenAPI transformers for Articulate operation IDs and security requirements.

Baseline (`net10.0` + Umbraco 17 stable):

PowerShell:

```powershell
dotnet restore .\src\Articulate.sln -p:UmbracoCmsPackageVersion=17.2.2
dotnet build .\src\Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=17.2.2
dotnet test .\src\Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=17.2.2
```

Bash:

```bash
dotnet restore ./src/Articulate.sln -p:UmbracoCmsPackageVersion=17.2.2
dotnet build ./src/Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=17.2.2
dotnet test ./src/Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=17.2.2
```

PowerShell:

```powershell
dotnet restore .\src\Articulate.sln -p:UmbracoCmsPackageVersion=18.0.0-beta2
dotnet build .\src\Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=18.0.0-beta2
dotnet test .\src\Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=18.0.0-beta2
dotnet run -f net10.0 --project .\src\Articulate.Tests.Website\Articulate.Tests.Website.csproj -p:UmbracoCmsPackageVersion=18.0.0-beta2
```

Bash:

```bash
dotnet restore ./src/Articulate.sln -p:UmbracoCmsPackageVersion=18.0.0-beta2
dotnet build ./src/Articulate.sln -c Debug -f net10.0 --no-restore -p:UmbracoCmsPackageVersion=18.0.0-beta2
dotnet test ./src/Articulate.sln -c Debug -f net10.0 --no-build --no-restore -p:UmbracoCmsPackageVersion=18.0.0-beta2
dotnet run -f net10.0 --project ./src/Articulate.Tests.Website/Articulate.Tests.Website.csproj -p:UmbracoCmsPackageVersion=18.0.0-beta2
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
