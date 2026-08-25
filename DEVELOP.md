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
dotnet run build/build.cs -- build --configuration Debug --client true --sample
```

Bash:

```bash
dotnet run build/build.cs -- build --configuration Debug --client true --sample
```

This restores NuGet and Node packages, builds the Back Office client, builds the theme and Markdown editor dist bundles, builds the .NET solution, and produces NuGet packages.

1. Open `src/Articulate.sln`.
2. Set `Articulate.Tests.Website` as the startup project.
3. Start `Articulate.Tests.Website` and complete the Umbraco installer.
4. The Articulate package migrations will run and install the required schema and content items.
   - **Tip:** The `ArticulatePackageLane` property selects the Umbraco version: `v17` uses Umbraco 17 and `v18` uses Umbraco 18. The default lane is `v17`; set `ArticulatePackageLane=v18` when testing the v18 path.

## Docker Modes

The Compose stack supports two explicit runtime states through `UMBRACO_RUNTIME_MODE`. The default is `BackofficeDevelopment`; switch to `Production` for the production-style check.

- `BackofficeDevelopment` (default) for local dev and agent runs. This enables the dev-only test-site bootstrap so the API user and client credentials can be provisioned automatically after install and migrations.
- `Production` for the production-style check. This disables test-site bootstrap and keeps the stack honest about what content was already published in the data volume.

Recommended benchmark flow:

1. Start with an empty Docker volume set in `BackofficeDevelopment` with `dotnet run docker/run.cs -- docker-dev --lane v17 --reset`.
2. Unattended install and package migrations run, test-site bootstrap provisions credentials, and `docker/smoke.mjs` publishes/verifies the Articulate content tree.
3. Verify `/` returns `200` and record the timing.
4. Re-run the same volume set in `Production` to confirm the published content still serves without any dev-only test-site bootstrap.

- `--reset` runs `docker compose down -v` before the dev command starts the stack. Use it for empty-DB QA, not for normal iterative runs.

## Client Development

From `src/Articulate.Web/Client`:

```bash
pnpm install
pnpm run build
```

`pnpm run build` runs both lanes (`tsc && vite build`); the Vite sidecar also regenerates the built-in theme `assets/dist` bundles and the Markdown editor assets, not just the Back Office client.

For API client generation and the v17 LTS schema comparison workflow, see
[BUILD.md's Client API generation section](BUILD.md#client-api-generation). That
section is authoritative.

## Build And Pack

| Shell | Command |
| --- | --- |
| Any shell | `dotnet run build/build.cs -- build [options]` |

- `--configuration Debug` is the default for local builds; Release is the default in packaging flows.
- `--client true` enables local TypeScript Back Office client builds.
- `--sample` packs `Articulate.Theme.Sample`.
- `build/build.cs` cleans, restores, builds, tests, and packs the current Articulate projects.
- The packable NuGet package is produced by `src/Articulate.Web/Articulate.Web.csproj` (`PackageId=Articulate`). Packages are written under `build/$(Configuration)` by default.
- If you change packaged runtime dependencies or client/static assets, regenerate the Docker inputs before validating source-built or Docker-based installs:
  - `dotnet pack src/Articulate.Web/Articulate.Web.csproj -c Release`
  - `dotnet pack src/Articulate.Theme.Sample/Articulate.Theme.Sample.csproj -c Release`
- The Dockerfile selects the newest `Articulate.[0-9]*.nupkg` in `build/Release` by modified time and ignores `.snupkg` files and theme packages when choosing the version.
- Rebuilding the image is not enough on its own. A running Compose service can remain on an older image/container. Use `docker compose up -d --build --force-recreate articulate`, or run both steps explicitly:
  - `docker compose build articulate`
  - `docker compose up -d --force-recreate --no-deps articulate`
- The default image tag is `articulate-local:chiseled`; the Compose container name will still be project/service based, for example `articulate-pr-articulate-1`.
- If the Docker back office still appears stale after a rebuild, check the running container, not just the image:
  - `docker compose ps`
  - `docker exec articulate-pr-articulate-1 /bin/sh -c "find /app -path '*App_Plugins/Articulate/BackOffice/articulate-backoffice.js' -o -path '*App_Plugins/Articulate/umbraco-package.json'"`
  - `Invoke-WebRequest https://localhost:18443/App_Plugins/Articulate/BackOffice/articulate-backoffice.js -SkipCertificateCheck`
- The default unattended Docker backoffice user is `admin@localhost` with password `@rticulate` and display name `Jane Doe`. Override with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and `UMBRACO_USER_PASSWORD` when needed.

## Back Office Client Builds

`EnableClientBuild` defaults to `false` so Visual Studio background builds do not clash with Vite output. When you need to rebuild the client during packaging or local validation, pass `--client true` to the build command:

PowerShell:

```powershell
dotnet run build/build.cs -- build --client true --sample
```

Bash:

```bash
dotnet run build/build.cs -- build --client true --sample
```

## Schema And Data

If you change the underlying Umbraco schema, installed content, or media, recreate the Articulate package in the back office with its dependencies, then resave `package.zip` and commit it.
