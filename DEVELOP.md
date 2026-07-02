# Articulate Development

## Requirements

- .NET 10.0 SDK
- Node.js 24+ with `corepack enable pnpm` (the workspace pins pnpm 11.4.0)
- Optional: Nerdbank.GitVersioning CLI (`dotnet tool install -g nbgv`), only needed for release builds
- IDE: Visual Studio 2026, JetBrains Rider, or Visual Studio Code
- Shell: PowerShell 7+ preferred (`pwsh`), PowerShell 5+, or Bash (WSL/Linux)

## First run

1. Clone or fork the repository.
2. Build the solution and Back Office client:

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

   Or open `src/Articulate.sln`, set `Articulate.Tests.Website` as the startup
   project, and start it. The default lane is Umbraco 17; pass
   `-p:ArticulatePackageLane=v18` to run Umbraco 18.
4. Complete the Umbraco installer, then the Articulate package migrations will
   install the required schema and content items.

For the full build command, parameter, lane, lock file, and smoke test
reference, see [BUILD.md](BUILD.md).

### Switching Umbraco lanes locally

The local dev database is lane-specific: Umbraco does not down-grade schema
across major versions. If you started the test website with
`ArticulatePackageLane=v18` and then switch to `v17` (e.g. via
`Directory.Build.props.user` or the IDE's launch profile), point v17 at a
**fresh** database and let it migrate. Do not reuse the v18 DB or schema
checks will fail. Back up any local content first.

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

For Docker validation (commands, ports, credentials, runtime modes, smoke
tests, the dev automation user, CA trust, the Umbraco MCP integration), see
[`build/docker-site/README.md`](build/docker-site/README.md).

## Back Office client builds

`EnableClientBuild` defaults to `false` so Visual Studio background builds do
not clash with Vite output. When you need to rebuild the client during packaging
or local validation, set `--client true` on the build command or set
`ENABLE_CLIENT_BUILD=true` inline.

```powershell
dotnet run --file build/build.cs -- build --client true
```

## Schema and data

If you need to make changes to the underlying Umbraco schema (doc types, data
types, etc...) or the installed package's content/media, then you will need
to re-create the Articulate package in the back office with all required
dependencies and then re-save the `package.zip` file and commit it to the
repository.

## Extending Articulate

Articulate ships as a NuGet package with extension points for theme authors,
custom controllers, and importers. The package layout puts extension surfaces
under `src/Articulate.Web/`:

```text
src/Articulate.Web/
├── Client/                          # Backoffice TS workspace (v17/ + v18/)
├── Controllers/                     # MVC + API controllers
├── Models/                          # View models for themes
├── PropertyEditors/                 # Markdown editor + custom property editors
├── ImportExport/                    # BlogML import/export
├── MetaWeblog/                      # MetaWeblog API (Live Writer)
├── Composers/                       # Umbraco composer registrations
└── umbraco-package.json             # Backoffice manifest
```

### Themes

For most sites, start with a built-in theme and copy it. See
[`docs/themes.md`](docs/themes.md) for the full authoring guide — folder
layout, descriptor registration, helper APIs, and the Razor model.
To add a new comment provider, update the provider switch in
`CommentsDisqus.cshtml` and document its configuration in
[`docs/configuration.md`](docs/configuration.md).

### Custom controllers and API endpoints

- **Render controllers** in `src/Articulate.Web/Controllers/` extend
  `RenderController` for custom routes that resolve Umbraco content.
- **API controllers** in `src/Articulate.Web/Controllers/Api/` follow Umbraco's
  Management API conventions and surface in Swagger / OpenAPI.
- **MetaWeblog** support lives in `src/Articulate.Web/MetaWeblog/` for Live
  Writer and compatible desktop clients.

### Property editors

The Markdown editor and other Articulate property editors live in
`src/Articulate.Web/PropertyEditors/`. To override behavior, subclass the
existing editor and re-register through your own composer.

### Importers

`src/Articulate.Web/ImportExport/BlogMlImporter.cs` is the reference
implementation for BlogML import. The BlogML safety rules around image
allowlisting and SSRF apply to any custom importer you add — keep the
`AllowedMediaHosts` and `MaxImportImageBytes` configuration knobs in mind.

### Rich text compatibility

For the fuller rich-text upgrade and compatibility notes, see the wiki. The
code keeps `Umbraco.RichText` as the stable schema, and the `EditorUiAlias`
migration only runs when TinyMCE is not available. When `TinyMCE.Umbraco` is
installed for a lane, Articulate leaves that editor path alone on first boot.

### Backoffice extensions

`umbraco-package.json` is the backoffice manifest. To extend the backoffice,
follow [Umbraco's extension registry docs](https://docs.umbraco.com/umbraco-cms/extending/extension-registry)
and add the corresponding TS workspace under `Client/v17/` and `Client/v18/`
to ship lane-aware extensions.
