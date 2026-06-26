# Articulate Build

The canonical reference for the build system and CI pipeline. For day-to-day
developer setup, see [DEVELOP.md](DEVELOP.md).

`build/build.cs` is the entry point for local and CI builds:

```text
dotnet run --file build/build.cs -- help
dotnet run --file build/build.cs -- help <command>
```

The CLI help is the canonical command/option reference, including defaults and
environment requirements. Environment variables remain supported for CI and
local overrides.

## Build parameters

| Parameter                    | Default                                | Description                                                                                                                                                                                                                |
|------------------------------|----------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `--lane`                     | `v17`                                  | Package lane: `v17` (Articulate 6.1 for Umbraco 17) or `v18` (Articulate 7.0 for Umbraco 18).                                                                                                                              |
| `--configuration`            | `Release`                              | Build configuration: `Debug` or `Release`.                                                                                                                                                                                 |
| `--tests`                    | `true` in CI, otherwise `false`        | Run `dotnet test` after build.                                                                                                                                                                                             |
| `--client`                   | `true` in CI/Release, `false` in Debug | Enable the TypeScript Back Office client build (Vite + tsc).                                                                                                                                                               |
| `--sample`                   | `true` locally, `false` in CI          | Also pack `Articulate.Theme.Sample`. The sample .nupkg is consumed locally by the Docker pipeline (see `build/docker-site/ArticulateDockerSite.csproj`); it is **not** published and is excluded from CI artifact uploads. |
| `--clean`                    | `false`                                | Wipe `src/**/bin` and `obj`, `build/ClientAssets`, and client `node_modules`. `BuildAsync` always invalidates the per-lane `BackOffice` assets and Vite stamp before each build (see [Package lanes](#package-lanes)).     |
| `ARTICULATE_PACKAGE_VERSION` | calculated                             | Optional explicit package-version override. v17 uses NBGV; v18 uses `build/v18-version.txt` plus NBGV metadata.                                                                                                            |

The packable package is produced by `src/Articulate.Web/Articulate.Web.csproj`
(`PackageId=Articulate`). Packages are written under `build/$(Configuration)/v17`
or `build/$(Configuration)/v18`.

Because both lanes share project `bin`/`obj` directories and static-web-asset
paths, always run full-solution lane builds sequentially and with `-m:1`
(`build/build.cs` already does this internally).

## Common build commands

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

## Package lanes

| Lane  | Package line     | Umbraco support | Target framework | Output folder       |
|-------|------------------|-----------------|------------------|---------------------|
| `v17` | Articulate 6.1.x | Umbraco 17.4+   | `net10.0`        | `build/Release/v17` |
| `v18` | Articulate 7.0.x | Umbraco 18      | `net10.0`        | `build/Release/v18` |

The lanes produce separate NuGet packages because Umbraco 17 and 18 extension
points are not binary-compatible. Do not cross-install (Articulate 6 ↔ Umbraco 18,
or Articulate 7 ↔ Umbraco 17).

`version.json` defines the Articulate 6.1 version through NBGV.
`build/v18-version.txt` defines the Articulate 7 base version. Build scripts
append NBGV commit metadata when present: a v18 base of `7.0.0` produces
`7.0.0.gabcdef`. Change the text file to advance the v18 release; callers do
not normally pass a version.

Both lanes share `wwwroot/App_Plugins/Articulate/BackOffice/`, so `BuildAsync`
wipes it — and deletes the per-lane Vite stamp — before each build to keep
per-lane output from leaking and force the incremental `BuildBackofficeClient`
target to run (otherwise a surviving stamp makes Vite skip and the package ships
without BackOffice bundles). Follow-up: add the resolved app version to
`@(ClientBuildInput)` in `BuildBackofficeClient` so Vite's incremental check
invalidates on lane/version change and the wipe becomes unnecessary.

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
