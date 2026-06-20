# Articulate Development

## Requirements

- .NET 10.0 SDK
- Node.js 24+ with `corepack enable pnpm` (the workspace pins pnpm 11.4.0)
- Optional: Nerdbank.GitVersioning CLI (`dotnet tool install -g nbgv`), only needed for release builds
- IDE: Visual Studio 2026, JetBrains Rider, or Visual Studio Code
- Shell: PowerShell 5+, PowerShell 7+, or Bash (WSL/Linux)

## First run

1. Clone or fork the repository.
2. Build the solution and Back Office client assets:

   PowerShell:

   ```powershell
   dotnet run --file build/build.cs -- build --configuration Debug --client true
   ```

   Bash:

   ```bash
   dotnet run --file build/build.cs -- build --configuration Debug --client true
   ```

   This restores, builds (including the Back Office client and theme/Markdown
   editor dist bundles), and produces NuGet packages.

   ### Local-only overrides

   Both `.actrc` (local `act` settings) and `Directory.Build.props.user` (local
   MSBuild property overrides) are gitignored. Examples:

   ```text
   # .actrc
   --env ACT=true
   ```

   ```xml
   <!-- Directory.Build.props.user -->
   <Project>
     <PropertyGroup>
       <ArticulatePackageLane>v18</ArticulatePackageLane>
       <EnableClientBuild>false</EnableClientBuild>
     </PropertyGroup>
   </Project>
   ```

   `Directory.Build.props.user` is imported automatically by MSBuild when present
   and is useful for persisting a default lane or disabling client builds for
   faster local iteration.
3. Start the test website:

   ```powershell
   dotnet run --file build/build.cs -- site --lane v17
   ```

   Or, open `src/Articulate.sln`, set `Articulate.Tests.Website` as the startup
   project, and start it. The default lane is Umbraco 17; pass
   `-p:ArticulatePackageLane=v18` to run Umbraco 18.
4. Complete the Umbraco installer, then the Articulate package migrations will
   install the required schema and content items.

## Build commands

`build/build.cs` is the entry point for local and CI builds:

```text
dotnet run --file build/build.cs -- help
dotnet run --file build/build.cs -- help <command>
```

The CLI help is the canonical command/option reference, including defaults and
environment requirements. Environment variables remain supported for CI and
local overrides.

### Build parameters

| Parameter                    | Default                                | Description                                                                                                     |
|------------------------------|----------------------------------------|-----------------------------------------------------------------------------------------------------------------|
| `--lane`                     | `v17`                                  | Package lane: `v17` (Articulate 6.1 for Umbraco 17) or `v18` (Articulate 7.0 for Umbraco 18).                   |
| `--configuration`            | `Release`                              | Build configuration: `Debug` or `Release`.                                                                      |
| `--tests`                    | `true` in CI, otherwise `false`        | Run `dotnet test` after build.                                                                                  |
| `--client`                   | `true` in CI/Release, `false` in Debug | Enable the TypeScript Back Office client build (Vite + tsc).                                                    |
| `--sample`                   | `true` locally, `false` in CI          | Also pack `Articulate.Theme.Sample`. The sample .nupkg is consumed locally by the Docker pipeline (see `build/docker-site/ArticulateDockerSite.csproj`); it is **not** published and is excluded from CI artifact uploads. |
| `--clean`                    | `false`                                | Wipe `src/**/bin` and `obj`, `build/ClientAssets`, and the generated `BackOffice` static web assets.            |
| `ARTICULATE_PACKAGE_VERSION` | calculated                             | Optional explicit package-version override. v17 uses NBGV; v18 uses `build/v18-version.txt` plus NBGV metadata. |

The packable package is produced by `src/Articulate.Web/Articulate.Web.csproj`
(`PackageId=Articulate`). Packages are written under `build/$(Configuration)/v17`
or `build/$(Configuration)/v18`.

Because both lanes share project `bin`/`obj` directories and static-web-asset
paths, always run full-solution lane builds sequentially and with `-m:1`
(`build/build.cs` already does this internally).

### Common build commands

Local debug build with the Back Office client:

```powershell
dotnet run --file build/build.cs -- build --configuration Debug --client true
```

v17 release package with sample theme:

```powershell
dotnet run --file build/build.cs -- build --lane v17 --sample
```

v18 release package with sample theme:

```powershell
dotnet run --file build/build.cs -- build --lane v18 --sample
```

CI / release build for both lanes:

```powershell
# Run once per lane; each run cleans shared outputs first.
dotnet run --file build/build.cs -- build --lane v17 --clean --client true --tests --sample
dotnet run --file build/build.cs -- build --lane v18 --clean --client true --tests --sample
```

### Package lanes

| Lane  | Package line     | Umbraco support | Target framework | Output folder       |
|-------|------------------|-----------------|------------------|---------------------|
| `v17` | Articulate 6.1.x | Umbraco 17.4+   | `net10.0`        | `build/Release/v17` |
| `v18` | Articulate 7.0.x | Umbraco 18      | `net10.0`        | `build/Release/v18` |

The lanes produce separate NuGet packages because Umbraco 17 and 18 extension
points are not binary-compatible. Do not cross-install (Articulate 6 ↔ Umbraco 18,
or Articulate 7 ↔ Umbraco 17).

`version.json` defines the Articulate 6.1 version through NBGV.
`build/v18-version.txt` defines the Articulate 7 base version. Build scripts
append NBGV commit metadata when present: a v18 base of `7.0.0-rc1` produces
`7.0.0-rc1.gabcdef`. Change the text file to advance the v18 release
candidate; callers do not normally pass a version.

## Client development

The Back Office client is a pnpm workspace at `src/Articulate.Web/Client` with
per-lane packages under `v17/` and `v18/`.

Install once:

```bash
cd src/Articulate.Web/Client
pnpm install
```

Work in the required lane:

```bash
cd v17   # or v18
pnpm run check     # tsc --noEmit + shared-file guard
pnpm run build     # tsc && vite build
pnpm run lint
```

`pnpm run build` also regenerates the built-in theme `assets/dist` bundles and
the Markdown editor assets, not just the Back Office client.

`pnpm run generate:api` regenerates that lane's typed client (`src/api/**`)
from a running Umbraco site. The v17 script reads Swagger JSON; the v18 script
reads the native OpenAPI JSON.

## Test website

Start the test site directly from the build script:

```powershell
dotnet run --file build/build.cs -- site --lane v17
```

Use `--reset` to delete the local `umbraco` data folder before starting.

## Docker validation

Docker is a local validation tool. GitHub Actions builds package artifacts but
does not run Docker.

`docker-compose.yml` at the repo root is the authoritative container definition.
See [`build/docker-site/README.md`](build/docker-site/README.md) for focused
container diagnostics, certificate trust, smoke commands, and LAN access.
All Docker workflows set environment variables and call `docker compose`; no
`docker run` is used directly.

The Caddy reverse proxy terminates TLS and exposes the stack at
`https://localhost:18443/` by default. OpenIddict requires HTTPS — direct HTTP
containers cannot complete the backoffice authorize flow in Production mode.

The compose stack supports two runtime modes through `UMBRACO_RUNTIME_MODE`:

- `BackofficeDevelopment` (default) auto-provisions the dev API user and client
  credentials after install and migrations.
- `Production` disables that bootstrap and verifies that published content
  survives a restart.

Typical flow: start in `BackofficeDevelopment` with empty volumes, publish
and confirm content, then re-run in `Production` against the same volumes.

### Build a Docker image

```powershell
dotnet run --file build/build.cs -- docker-build --lane v17
```

Optional `--tag` overrides the default `articulate-local:v17` or
`articulate-local:v18` image name.

### Dev workflow

The dev script builds packages for the lane if they are missing, starts the
compose stack in `BackofficeDevelopment` mode, waits for Umbraco, then publishes
and confirms Articulate content via the Management API.

v17:

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
dotnet run --file build/build.cs -- docker-dev --lane v17
```

v18:

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
dotnet run --file build/build.cs -- docker-dev --lane v18
```

`docker-dev` ensures the selected lane packages exist, configures its
lane-specific image, ports, project name, volumes, and callback URLs, then
builds and starts the Compose stack. The default lane is `v17`.

Use `--reset` to run `docker compose down -v` before starting (empty-DB QA, not
for normal iterative runs). Use `--skip-smoke` to skip the API publish/confirm
steps.

Trust the local CA once per machine:

- Windows: `powershell -ExecutionPolicy Bypass -File .\build\docker-site\Trust-CaddyRootCA.ps1`
- Linux/WSL: `sudo ./build/docker-site/trust-caddy-root-ca.sh`

The unattended install creates this default **local Docker backoffice
administrator**:

- Email: `admin@localhost`
- Password: `@rticulate`
- Display name: `Jane Doe`

Use that account to sign in at the selected lane's `/umbraco/` URL. These
credentials are public repository defaults and must not be used outside the
local Docker test site. Override the unattended user with `UMBRACO_USER_NAME`,
`UMBRACO_USER_EMAIL`, and `UMBRACO_USER_PASSWORD`.

### Production smoke

After the dev script finishes, run the production smoke to confirm published
content survives a `Production`-mode restart:

```powershell
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='articulate-dev-local-secret'
dotnet run --file build/build.cs -- docker-prod --lane v17
```

Use the same lane as the preceding `docker-dev` command so production mode
reuses that lane's volumes and published content.

### End-to-end Docker testing

Run comprehensive validation for one or both lanes:

```powershell
dotnet run --file build/build.cs -- docker-test --lane all --keep
```

`ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` defaults to
`articulate-dev-local-secret` (matching `docker-compose.yml`) and only needs
to be exported when overriding the dev secret.

Containers stay running at `https://localhost:44317/` (v17) and
`https://localhost:44318/` (v18), with Caddy's HTTP listener on
`http://localhost:44380/` (v17) and `http://localhost:44381/` (v18). The
defaults sit out of common dev-port collision ranges; override with
`CADDY_HTTPS_PORT` / `CADDY_HTTP_PORT` if they clash on your host. Use
`--lane v17` or `--lane v18` for a single lane. Remove `--keep` to clean
up containers after testing. Use `--skip-smoke` to skip API publish/confirm
tests (faster build validation).

Inspect a running lane and confirm its packaged Backoffice files are present:

```powershell
dotnet run --file build/build.cs -- docker-status --lane v17
```

Use `--lane v18` for the v18 stack.

### Compose environment variables

The compose file and build script use these variables. Lane-specific defaults
are applied by `docker-test` for v17/v18.

| Variable                                  | Default                                | Purpose                                                                                                                                                                          |
|-------------------------------------------|----------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ARTICULATE_PACKAGE_LANE`                 | `v17`                                  | Package lane passed to the Docker build.                                                                                                                                         |
| `BUILD_CONFIGURATION`                     | `Release`                              | .NET build configuration inside the Docker build.                                                                                                                                |
| `TARGET_FRAMEWORK`                        | `net10.0`                              | .NET TFM for the Docker build.                                                                                                                                                   |
| `DOTNET_SDK_IMAGE`                        | `mcr.microsoft.com/dotnet/sdk:10.0`    | SDK image used to build the site container.                                                                                                                                      |
| `DOTNET_ASPNET_IMAGE`                     | `mcr.microsoft.com/dotnet/aspnet:10.0` | Runtime image used for the site container.                                                                                                                                       |
| `PACKAGE_SOURCE`                          | `build/Release/v17`                    | NuGet package folder inside the repo.                                                                                                                                            |
| `UMBRACO_CMS_VERSION`                     | `[17.4.0,18.0.0)`                      | Umbraco version constraint for the Docker build.                                                                                                                                 |
| `IMAGE_TAG`                               | `articulate-local:chiseled`            | Docker image tag.                                                                                                                                                                |
| `COMPOSE_PROJECT_NAME`                    | `articulate`                           | Docker Compose project name.                                                                                                                                                     |
| `COMPOSE_VOLUME_PREFIX`                   | `articulate`                           | Prefix for named Umbraco data/media volumes.                                                                                                                                     |
| `CADDY_BIND_IP`                           | `127.0.0.1`                            | Host interface Caddy binds. Loopback by default so the auto-provisioned OAuth client + known admin password aren't exposed on the LAN. Set to `0.0.0.0` to expose intentionally. |
| `CADDY_HTTP_PORT`                         | `8080`                                 | Host port Caddy listens on for HTTP.                                                                                                                                             |
| `CADDY_HTTPS_PORT`                        | `18443`                                | Host port Caddy listens on for HTTPS.                                                                                                                                            |
| `CADDY_HTTPS_HOST`                        | `localhost:18443`                      | Browser-facing HTTPS authority, including the external port.                                                                                                                     |
| `CADDY_TLS_HOST`                          | `localhost`                            | Host or IP Caddy mints a certificate for and uses when an IP client omits SNI. Derived from `CADDY_HTTPS_HOST` by default.                                                       |
| `UMBRACO_PUBLIC_HOST`                     | `https://localhost:18443`              | Public host passed to Umbraco.                                                                                                                                                   |
| `UMBRACO_PUBLIC_URL`                      | `https://localhost:18443/`             | Public URL passed to Umbraco and smoke scripts.                                                                                                                                  |
| `UMBRACO_RUNTIME_MODE`                    | `BackofficeDevelopment`                | Umbraco runtime mode (`BackofficeDevelopment` or `Production`).                                                                                                                  |
| `UMBRACO_USER_NAME`                       | `Jane Doe`                             | Unattended install user name.                                                                                                                                                    |
| `UMBRACO_USER_EMAIL`                      | `admin@localhost`                      | Unattended install user email.                                                                                                                                                   |
| `UMBRACO_USER_PASSWORD`                   | `@rticulate`                           | Unattended install user password.                                                                                                                                                |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` | *(required for smoke)*                 | Secret for the dev automation API client.                                                                                                                                        |
| `ARTICULATE_DEV_AUTOMATION_CLIENT_ID`     | `articulate-dev-automation`            | Client ID for the dev automation API user.                                                                                                                                       |
| `ARTICULATE_OPENID_CLIENT_ID`             | `umbraco-articulate`                   | OpenIddict client ID for the Markdown editor.                                                                                                                                    |
| `ARTICULATE_OPENID_DISPLAY_NAME`          | `Articulate Markdown Editor`           | Display name for the Markdown editor OpenIddict client.                                                                                                                          |
| `ARTICULATE_REDIRECT_URI`                 | `https://localhost:18443/a-new/`       | Sign-in callback for the Markdown editor.                                                                                                                                        |
| `ARTICULATE_LOGOUT_REDIRECT_URI`          | `https://localhost:18443/`             | Post-sign-out destination for the Markdown editor.                                                                                                                               |

### Cookie isolation between lanes

Both v17 and v18 run on the same `localhost` domain but different ports. Browser
cookies are domain-scoped (port is ignored), so the default Umbraco back-office
cookie (`UMB_UCONTEXT` in v17.4 / v18.0.0-rc3, plus the new OAuth cookies
`umbAccessToken` / `umbRefreshToken` / `umbPkceCode` in v17.3+) would normally
clash and log you out of one lane when signing into the other.

`build/build.cs` `ConfigureLane` sets two per-lane config values to fix this:

- `Umbraco__CMS__Security__AuthCookieName=UMB_UCONTEXT-{lane}` — renames the
  legacy `UMB_UCONTEXT` cookie. (`Security:AuthCookieName` is the supported
  config key; the docker harness just plumbs it through.)
- `Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName=-{lane}` — appends
  a suffix to the new OAuth cookie names per Umbraco PR #22057 (shipped in
  Umbraco 17.3+).

You stay logged into both lanes simultaneously without browser juggling.

### LAN or custom-host exposure

To test `/a-new/` from another machine on the LAN:

1. `CADDY_BIND_IP=0.0.0.0` so Caddy binds to all interfaces.
2. Use the LAN IP (or hostname) in every browser-facing URL.
3. Pass `--reset` so OpenIddict registers redirect URIs for that origin.

```powershell
# <lan-ip>: IPv4 address (or mDNS hostname like devbox.local) other machines
# on your network can reach this dev box on.
$env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET="articulate-dev-local-secret"
$env:CADDY_BIND_IP="0.0.0.0"
$env:CADDY_HTTPS_HOST="<lan-ip>:44317"
$env:UMBRACO_PUBLIC_HOST="https://<lan-ip>:44317"
$env:UMBRACO_PUBLIC_URL="https://<lan-ip>:44317/"
$env:ARTICULATE_REDIRECT_URI="https://<lan-ip>:44317/a-new/"
$env:ARTICULATE_LOGOUT_REDIRECT_URI="https://<lan-ip>:44317/"
dotnet run --file build/build.cs -- docker-dev --lane v17 --reset
```

> [!WARNING]
> Exposing the dev harness to the LAN makes the site and its default administrative credentials (`admin@localhost` / `@rticulate`) accessible to anyone on your network. Never run this configuration on a public or untrusted network.

## NuGet lock files

The two shipped packages use lock files (opt-in via `RestorePackagesWithLockFile=true`):

- `src/Articulate.Web/packages.v17.lock.json`, `…packages.v18.lock.json`
- `src/Articulate.Theme.Sample/packages.v17.lock.json`, `…packages.v18.lock.json`

CI and the build script use `--locked-mode`, so these files must be checked in
and kept current.

After changing any centralized version in `Directory.Packages.props`,
regenerate the lock files for both lanes:

```powershell
dotnet restore ./src/Articulate.sln -p:ArticulatePackageLane=v17 -p:RestoreLockedMode=false --force-evaluate
dotnet restore ./src/Articulate.sln -p:ArticulatePackageLane=v18 -p:RestoreLockedMode=false --force-evaluate
```

Test projects and the test website float; they do not need lock files.

The lock files are restore-time inputs for `<RestoreLockedMode>` and never ship
in the published package — both packable `.csproj` files exclude
`packages.*.lock.json` via `<Content Remove>`. The [package smoke test](#package-smoke-test)
fails the build if a lock file reappears in any `*.nupkg`.

## Package smoke test

`build/smoke-package.mjs` opens each `build/Release/<lane>/*.nupkg` and
`*.snupkg`, extracts key files, and verifies the package is well-formed. CI runs
it after both lanes pack and before artifact upload; a failed check skips the
workflow upload so leaks never reach GitHub Actions artifacts.

Run it locally after a build:

```bash
node build/smoke-package.mjs build/Release/v17 build/Release/v18
```

What it checks for `Articulate.<ver>.nupkg`:

- Root files: `LICENSE`, `README.md`, `icon.png`.
- `.nuspec` parses; `id=Articulate`; has the `net10.0` dependency group with
  `Umbraco.Cms.Web.Website` + `Umbraco.Cms.Api.Management`.
- Ships only `staticwebassets/` for client assets (no legacy `content/` or
  `contentFiles/any/{tfm}/` paths).
- `lib/net10.0/`: `Articulate.Web.dll` + `.xml`, `Articulate.dll` + `.xml`.
- `build/`: `Articulate.targets`, `Articulate.props`, and the
  `Microsoft.AspNetCore.StaticWebAssets*.props` files.
- `umbraco-package.json` parses and declares a back-office entry.
- BackOffice bundles present (entrypoint, articulate-backoffice.js, dashboard,
  theme-picker, markdown editor) and all four theme preview PNGs
  (`theme-{material,mini,phantom,vapor}.png`).
- `MarkdownEditor` assets (`md-editor.min.css`, `md-editor.min.js`).
- Each shipped theme (Material, Mini, Phantom, VAPOR) has its `*.min.css` (and
  the JS bundles that exist).
- `Articulate.dll` contains every embedded resource under
  `Articulate.Packaging.*` — `author.jpg`, `banner.jpg`, `logo.png`,
  `package.zip` (Starter Kit installer), `post1.jpg`, `post2.jpg`.
- `Articulate.Web.dll` exposes the `Articulate.Theme://` logical-name prefix
  (used by `ArticulateThemeRepository.CopyThemeAsync`) and at least 50
  occurrences of `App_Plugins_Articulate_Themes_*` (compiled razor views).
- No `packages.*.lock.json` anywhere in the archive.

What it checks for `Articulate.Theme.Sample.<ver>.nupkg`:

- Root files, no lock files, single `.nuspec` with `id=Articulate.Theme.Sample`
  and a dependency on `Articulate`.
- `lib/net10.0/Articulate.Theme.Sample.dll` present.
- `staticwebassets/.../Themes/Sample/assets/{css/site.css,js/site.js}` present.

What it checks for `Articulate.<ver>.snupkg`:

- `lib/net10.0/Articulate.Web.pdb` present and non-trivial (> 50 KB).

## Back Office client builds

`EnableClientBuild` defaults to `false` so Visual Studio background builds do
not clash with Vite output. When you need to rebuild the client during packaging
or local validation, set `--client true` on the build command or set
`ENABLE_CLIENT_BUILD=true` inline.

```powershell
dotnet run --file build/build.cs -- build --client true
```

## Schema and data

If you change the underlying Umbraco schema, installed content, or media,
recreate the Articulate package in the back office with its dependencies, then
resave `package.zip` and commit it.
