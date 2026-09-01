# Articulate Development

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0); the repository utilities use [.NET file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps).
- [Node.js 24.x](https://nodejs.org/en/download/) and [pnpm 11.19.0](https://pnpm.io/installation).
- [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) CLI (`dotnet tool install -g nbgv`) for local build and pack commands. Set `ARTICULATE_PACKAGE_VERSION` to use an explicit version instead.
- IDE: Visual Studio 2026, JetBrains Rider, or Visual Studio Code.
- Shell: PowerShell 5+, PowerShell 7+, or Bash (WSL/Linux).

## First Run

1. Clone or fork the repository.
2. Build the solution, client, themes, and sample package.

   ```sh
   dotnet run --file build/build.cs -- build --configuration Debug --client true --sample
   ```

This restores NuGet and Node packages, builds the Back Office client, builds the theme and Markdown editor bundles, builds the .NET solution, and produces NuGet packages.
3. Start the test site.

   ```sh
   dotnet run --file build/build.cs -- site --lane v17
   ```

Use `--lane v18` to test Umbraco 18. Use `--reset` when you need a fresh database. Alternatively, open `src/Articulate.sln`, set `Articulate.Tests.Website` as the startup project, and start it from the IDE.
4. Sign in with the local credentials in `src/Articulate.Tests.Website/appsettings.json`. The test site installs Umbraco and runs the Articulate package migrations automatically.

## Dev Containers

The repository includes a dev container with the required toolchain and supporting CLI tools. Install the [Dev Containers extension for VS Code](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers).

For an isolated checkout, use **Dev Containers: Clone Repository in Container Volume...** for a branch or the default branch, or **Dev Containers: Clone GitHub Pull Request in Container Volume...** for a pull request.

For a normal local checkout, use **Dev Containers: Reopen in Container**. See the [VS Code isolated container volume guide](https://code.visualstudio.com/docs/devcontainers/containers#_quick-start-open-a-git-repository-or-github-pr-in-an-isolated-container-volume) for details.

## Docker

Use the [Docker guide](docker/README.md) for Docker tooling, ports, credentials, runtime modes, smoke checks, and diagnostics. Use [`docker/help.md`](docker/help.md) for command options.

```sh
dotnet run --file docker/run.cs -- docker-dev --lane v17
```

Use `--lane v18` for Umbraco 18. Use `--reset` only when you need a fresh Docker database.

## Client Development

From `src/Articulate.Web/Client`:

```bash
pnpm install
pnpm run build
```

`pnpm run build` checks both lanes and rebuilds the built-in theme and Markdown editor assets.

## Compatibility

`ArticulatePackageLane` selects the package lane. `Directory.Build.props` defines `UMBRACO_18_OR_GREATER` for v18. The packages remain separate because the Umbraco extension points are not binary-compatible.

### Server API

| Area | v17 | v18 | Source |
| --- | --- | --- | --- |
| OpenAPI | Swashbuckle | Back Office OpenAPI | `src/Articulate/Components/ArticulateApiComposer.cs`, `src/Articulate/Swagger/V17/`, `src/Articulate/Swagger/V18/` |
| Routing | `NewDefaultUrlProvider`, `ContentFinderByUrlNew`, `content.UrlSegment` | `DefaultUrlProvider`, `ContentFinderByUrl`, `IDocumentUrlService.GetUrlSegment` | `src/Articulate/Components/ArticulateComposer.cs`, `src/Articulate/Routing/` |
| Tag assignment | Existing `AssignTags` overload | `IIdKeyMap` parameter | `src/Articulate/ContentExtensions.cs` and callers |
| Published wrapper | `PublishedContentWrapped(content, publishedValueFallback)` | `PublishedContentWrapped(content)` | `src/Articulate/Models/MasterModel.cs` |

The date provider and content finder select the matching base class and logger type. `ArticulateRouter` takes `IDocumentUrlService` only in v18. The v18 test projects pin `Microsoft.CodeAnalysis.CSharp.Workspaces` for Umbraco.Code 3. `ArticulateRouteValueTransformerTests.cs` mirrors the v17 `IFileService` and v18 `ITemplateService` constructor requirements.

The checked-in API client uses v18 OpenAPI at `src/Articulate.Web/Client/common/src/api/`; v17 output is comparison-only under `src/Articulate.Web/Client/common/.api-check/v17/`. BlogML responses stay `unknown`; `src/Articulate.Web/Client/common/src/api/api-compatibility.type-test.ts` protects the schema boundary and callers narrow the value to `Blob`.

See [BUILD.md's Client API generation section](BUILD.md#client-api-generation) for generation commands and the v17 schema comparison workflow.

### Back Office client

Feature source lives in `src/Articulate.Web/Client/common/`. The `v17` and `v18` folders contain lane metadata and configuration. `src/Articulate.Web/Client/common/src/lane-adapter.ts` maps v17's deprecated `UmbPropertyValueChangeEvent` to v18's `UmbChangeEvent`; assert the external `change` behaviour rather than the class name.

Keep `legacyDecoratorsPlugin()` in `src/Articulate.Web/Client/common/vite.config.ts`. `pnpm run check` only type-checks; `pnpm run build` also checks the decorator transform. BlogML callers live in `src/Articulate.Web/Client/common/src/components/` and `src/Articulate.Web/Client/common/src/utils/download.ts`.

Run the client build and .NET tests for both lanes after changing compatibility code.

## Build And Pack

See [BUILD.md](BUILD.md) for build parameters, lane boundaries, lock files, API generation, package smoke checks, and CI. Visual Studio client builds stay disabled by default; pass `--client true` when packaging or validating Back Office changes.

## Theme API reference

The wiki covers theme creation and layout. The repository owns the complete theme API contracts and helper implementations:

- [`IMasterModel.cs`](src/Articulate/Models/IMasterModel.cs)
- [`MasterModel.cs`](src/Articulate/Models/MasterModel.cs)
- [`PublishedContentExtensions.cs`](src/Articulate/Models/PublishedContentExtensions.cs)

## Schema And Data

If you change the underlying Umbraco schema, installed content, or media, recreate the Articulate package in the back office with its dependencies, then resave `package.zip` and commit it.
