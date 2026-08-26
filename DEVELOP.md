# Articulate Development

## Requirements

- .NET 10.0 SDK
- Node.js 24.x and pnpm 11.19.0. Install the pinned pnpm version with the [official standalone script](https://pnpm.io/installation): `curl -fsSL https://get.pnpm.io/install.sh | env PNPM_VERSION=11.19.0 sh -`. PowerShell: `$env:PNPM_VERSION = '11.19.0'; Invoke-WebRequest https://get.pnpm.io/install.ps1 -UseBasicParsing | Invoke-Expression`.
- Nerdbank.GitVersioning CLI (`dotnet tool install -g nbgv`) for local `build` and pack commands, unless `ARTICULATE_PACKAGE_VERSION` is set explicitly
- IDE: Visual Studio 2026, JetBrains Rider, or Visual Studio Code
- Shell: PowerShell 5+, PowerShell 7+, or Bash (WSL/Linux)

## Dev Containers

The repository includes a dev container with .NET 10, Node.js 24, pnpm 11.19.0,
NBGV, SQLite, GitHub CLI, and the Copilot CLI. Install the [Dev Containers
extension for VS Code](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers).

For an isolated checkout, use the VS Code Command Palette:

- **Dev Containers: Clone Repository in Container Volume...** for a branch or the
  default branch. Paste a repository or GitHub branch URL.
- **Dev Containers: Clone GitHub Pull Request in Container Volume...** for a PR.
  Paste the pull request URL.

For a normal local checkout, use **Dev Containers: Reopen in Container**. See the
[VS Code isolated container volume guide](https://code.visualstudio.com/docs/devcontainers/containers#_quick-start-open-a-git-repository-or-github-pr-in-an-isolated-container-volume)
for details.

## First Run

1. Clone or fork the repository.
2. Prime the site and solution so the Back Office client extension and asset bundles are built.

```sh
dotnet run build/build.cs -- build --configuration Debug --client true --sample
```

This restores NuGet and Node packages, builds the Back Office client, builds the theme and Markdown editor dist bundles, builds the .NET solution, and produces NuGet packages.

1. Start the test site:

   ```sh
   dotnet run build/build.cs -- site --lane v17
   ```

   Use `--lane v18` when testing the Umbraco 18 path. As an alternative, open
   `src/Articulate.sln`, set `Articulate.Tests.Website` as the startup project,
   and start it from the IDE.
2. The test site installs Umbraco and runs the Articulate package migrations
   automatically. Sign in with the local credentials in
   `src/Articulate.Tests.Website/appsettings.json`. Use `--reset` with the site
   command when you need a fresh database.

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
section is authoritative. Generation uses `Articulate.Tests.Website` on port
44366.

## Build And Pack

| Shell | Command |
| --- | --- |
| Any shell | `dotnet run build/build.cs -- build [options]` |

- The `site` command defaults to `Debug`. The `build` command defaults to `Release`; pass `--configuration Debug` when you need a local debug build.
- `--client true` enables local TypeScript Back Office client builds.
- `--sample` packs `Articulate.Theme.Sample`.
- `build/build.cs` cleans, restores, builds, tests, and packs the current Articulate projects.
- The packable NuGet package is produced by `src/Articulate.Web/Articulate.Web.csproj` (`PackageId=Articulate`). Packages are written under `build/$(Configuration)` by default.
- If you change packaged runtime dependencies or client/static assets, rebuild the affected lane with the repository runner. It writes the package inputs Docker consumes:
  - `dotnet run build/build.cs -- build --lane v17 --client true --sample`
  - `dotnet run build/build.cs -- build --lane v18 --client true --sample`
- For Docker validation, use the lane-specific runner so package versions, output paths, and `UMBRACO_CMS_VERSION` stay aligned:
  - `dotnet run docker/run.cs -- docker-test --lane v17`
  - `dotnet run docker/run.cs -- docker-test --lane v18`
- The Dockerfile selects the newest `Articulate.[0-9]*.nupkg` in `build/Release` by modified time and ignores `.snupkg` files and theme packages when choosing the version.
- Rebuilding the image is not enough on its own. A running Compose service can remain on an older image/container. Use the lane runner to rebuild and recreate it:
  - `dotnet run docker/run.cs -- docker-dev --lane v17`
  - `dotnet run docker/run.cs -- docker-dev --lane v18`
  Add `--reuse-packages` when only the image and container need refreshing. Direct Compose use needs the package-version variables and lane-specific ports from `docker/docker-compose.yml`.
- The default image tag is `articulate-local:chiseled`; the Compose container name will still be project/service based, for example `articulate-pr-articulate-1`.
- If the Docker back office still appears stale after a rebuild, check the running container, not just the image:
  - `docker compose ps`
  - `docker exec articulate-pr-articulate-1 /bin/sh -c "find /app -path '*App_Plugins/Articulate/BackOffice/articulate-backoffice.js' -o -path '*App_Plugins/Articulate/umbraco-package.json'"`
  - `curl --insecure --fail https://localhost:18443/App_Plugins/Articulate/BackOffice/articulate-backoffice.js`
- The default unattended Docker backoffice user is `admin@localhost` with password `@rticulate` and display name `Jane Doe`. Override with `UMBRACO_USER_NAME`, `UMBRACO_USER_EMAIL`, and `UMBRACO_USER_PASSWORD` when needed.

## Back Office Client Builds

`EnableClientBuild` defaults to `false` so Visual Studio background builds do not clash with Vite output. When you need to rebuild the client during packaging or local validation, pass `--client true` to the build command:

```sh
dotnet run build/build.cs -- build --client true --sample
```

## Theme API reference

The wiki covers theme creation and layout. The repository owns the complete theme
API contracts and helper implementations:

- [`IMasterModel.cs`](src/Articulate/Models/IMasterModel.cs)
- [`MasterModel.cs`](src/Articulate/Models/MasterModel.cs)
- [`PublishedContentExtensions.cs`](src/Articulate/Models/PublishedContentExtensions.cs)

## Schema And Data

If you change the underlying Umbraco schema, installed content, or media, recreate the Articulate package in the back office with its dependencies, then resave `package.zip` and commit it.
