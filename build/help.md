# Articulate build utility

```
dotnet run --file build/build.cs -- <command> [options]
dotnet run --file build/build.cs -- help [command]
```

| Command         | Purpose                                                               |
|-----------------|-----------------------------------------------------------------------|
| `build`         | Restore, build, optionally test, and pack a package lane.             |
| `client`        | Install, typecheck, build, and lint one Backoffice client.           |
| `site`          | Run `Articulate.Tests.Website` for one lane.                         |
| `docker-build`  | Build the standalone chiseled Docker image.                          |
| `docker-dev`    | Build and start a development stack; publish sample content.         |
| `docker-prod`   | Restart an existing lane in Production mode and smoke-test it.       |
| `docker-status` | Inspect a running lane and verify packaged Backoffice assets.        |
| `docker-test`   | Run the complete Docker matrix for one or both lanes.                |
| `docker-ca`     | Export and trust the local Caddy root CA.                            |

Defaults: `--lane v17` for every command except `docker-test`, which defaults to `--lane all`.
`help <command>` below for command-specific options.

### build

Restore, build, optionally test, and pack a package lane.

```
dotnet run --file build/build.cs -- build [options]
```

| Option                | Default                                | Notes                                                                  |
|-----------------------|----------------------------------------|------------------------------------------------------------------------|
| `--lane`              | `v17`                                  | `v17` (Articulate 6.x for Umbraco 17) or `v18` (Articulate 7.x for Umbraco 18). |
| `--configuration`     | `BUILD_CONFIGURATION` or `Release`      | `Debug` or `Release`.                                                   |
| `--tests true\|false` | `true` in CI, else `false`             | Run `dotnet test` after build.                                          |
| `--client true\|false`| `true` in CI/Release, else `false`     | Build the Backoffice client (Vite + tsc).                              |
| `--sample true\|false`| `true` locally, `false` in CI          | Pack `Articulate.Theme.Sample` (consumed by Docker, not published).    |
| `--clean`             | `false`                                | Wipe `src/**/bin`, `src/**/obj`, `build/ClientAssets`, `Client/node_modules` before building. |
| `--configuration`     | `BUILD_CONFIGURATION` or `Release`      | Override build configuration.                                           |

Packages land in `build/<Configuration>/<lane>/`.

### client

```
dotnet run --file build/build.cs -- client [--lane v17|v18]
```

Runs `pnpm install`, `pnpm run check`, `pnpm run build`, `pnpm run lint` for the Backoffice client workspace.

### site

```
dotnet run --file build/build.cs -- site [options]
```

| Option             | Default | Notes                                                      |
|--------------------|---------|------------------------------------------------------------|
| `--lane`           | `v17`   | v17 or v18.                                                |
| `--configuration`  | `Debug` | Build configuration.                                       |
| `--reset`          | `false` | Delete the site's `umbraco/` data folder first.             |

This command remains attached to the running site until stopped.

### docker-build

```
dotnet run --file build/build.cs -- docker-build [--lane v17|v18] [--tag image:tag]
```

| Option   | Default                   | Notes                      |
|----------|---------------------------|----------------------------|
| `--lane` | `v17`                     | v17 or v18.                |
| `--tag`  | `articulate-local:<lane>` | Image tag.                 |

Missing `Articulate` and sample-theme packages are built first.

### docker-dev

```
dotnet run --file build/build.cs -- docker-dev [options]
```

| Option       | Default | Notes                                                          |
|--------------|---------|----------------------------------------------------------------|
| `--lane`     | `v17`   | v17 or v18.                                                    |
| `--reset`    | `false` | Run `docker compose down -v` first.                             |
| `--skip-smoke`| `false`| Skip sample publish/confirm checks.                            |

Without `--skip-smoke`, `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` is required.
Missing packages are built automatically.

### docker-prod

```
dotnet run --file build/build.cs -- docker-prod [--lane v17|v18]
```

Requires `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`. Reuses the selected lane's
volumes, then runs front-end and theme smoke checks.

### docker-status

```
dotnet run --file build/build.cs -- docker-status [--lane v17|v18]
```

Shows Compose status and verifies the packaged Backoffice bundle and
`umbraco-package.json` inside the running container.

### docker-test

```
dotnet run --file build/build.cs -- docker-test [options]
```

| Option        | Default | Notes                                                    |
|---------------|---------|----------------------------------------------------------|
| `--lane`      | `all`   | `v17`, `v18`, or `all`.                                  |
| `--keep`      | `false` | Leave successful stacks running.                         |
| `--skip-smoke`| `false` | Skip API, front-end, and theme smoke tests.              |

Each lane builds without Docker cache, starts in development mode, and, unless
smoke is skipped, publishes/confirms content then restarts in Production mode
for front-end and theme checks.

Without `--skip-smoke`, `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET` is required.

### docker-ca

```
dotnet run --file build/build.cs -- docker-ca
```

Wraps the platform-specific helper under `build/docker-site/` and keeps the
local CA trust workflow reachable from the canonical build runner.

---

## Conventions

- Both lanes share project `bin`/`obj` and static-web-asset paths; always run
  full-solution lane builds sequentially with `-m:1`. The CLI enforces this.
- `ARTICULATE_PACKAGE_VERSION` is a CI/release override. v17 uses NBGV; v18
  derives from `build/v18-version.txt` plus NBGV metadata.
- CLI helpers `Bool(value?)` accept `true`/`false`; missing values fall back to
  the underlying environment variable.
