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
   dotnet run --file build/build.cs -- build --configuration Debug --client true --sample
   ```

1. Start the test site:

   ```sh
   dotnet run --file build/build.cs -- site --lane v17
   ```

   Use `--lane v18` when testing the Umbraco 18 path. As an alternative, open
   `src/Articulate.sln`, set `Articulate.Tests.Website` as the startup project,
   and start it from the IDE.
2. The test site installs Umbraco and runs the Articulate package migrations
   automatically. Sign in with the local credentials in
   `src/Articulate.Tests.Website/appsettings.json`. Use `--reset` with the site
   command when you need a fresh database.

## Docker

Use the [Docker guide](docker/README.md) for ports, credentials, runtime modes,
smoke checks, and diagnostics. Its command reference is in
[`docker/help.md`](docker/help.md).

For a standard local stack:

```sh
dotnet run --file docker/run.cs -- docker-dev --lane v17
```

Use `--lane v18` for the Umbraco 18 path. Use `--reset` only when you need a
fresh Docker database.

## Client Development

From `src/Articulate.Web/Client`:

```bash
pnpm install
pnpm run build
```

`pnpm run build` runs both lanes (`tsc && vite build`); the Vite sidecar also regenerates the built-in theme `assets/dist` bundles and the Markdown editor assets, not just the Back Office client.

### Backoffice client compatibility

Keep `legacyDecoratorsPlugin()` in `src/Articulate.Web/Client/common/vite.config.ts`.
Vite 8 otherwise emits decorators in a format Umbraco’s Lit runtime does not accept.

BlogML download responses remain `unknown` in the shared client because Umbraco 17
and 18 describe them differently. The callers check for a `Blob` before downloading it.

See `src/Articulate/Controllers/Api/BlogMlApiController.cs`,
`src/Articulate.Web/Client/common/src/components/blogml-exporter.element.ts`,
`blogml-importer.element.ts`, and `utils/download.ts`.

For API client generation and the v17 LTS schema comparison workflow, see
[BUILD.md's Client API generation section](BUILD.md#client-api-generation). That
section is authoritative. Generation uses `Articulate.Tests.Website` on port
44366.

## Build And Pack

See [BUILD.md](BUILD.md) for build parameters, lane boundaries, lock files, API
generation, package smoke checks, and CI. Visual Studio client builds stay
disabled by default; pass `--client true` when packaging or validating Back
Office changes.

## Theme API reference

The wiki covers theme creation and layout. The repository owns the complete theme
API contracts and helper implementations:

- [`IMasterModel.cs`](src/Articulate/Models/IMasterModel.cs)
- [`MasterModel.cs`](src/Articulate/Models/MasterModel.cs)
- [`PublishedContentExtensions.cs`](src/Articulate/Models/PublishedContentExtensions.cs)

## Schema And Data

If you change the underlying Umbraco schema, installed content, or media, recreate the Articulate package in the back office with its dependencies, then resave `package.zip` and commit it.
