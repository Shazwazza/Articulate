# Articulate build utility

```text
dotnet run --file build/build.cs -- <command> [options]
dotnet run --file build/build.cs -- help [command]
```

Use `build/build.cs` for repository build, client, and test-site tasks.

| Command  | Purpose                                                    |
|----------|------------------------------------------------------------|
| `build`  | Restore, build, optionally test, and pack a package lane.  |
| `client` | Install, typecheck, build, and lint one Backoffice client. |
| `site`   | Run `Articulate.Tests.Website` for one lane.               |

Commands default to lane `v17`.

### build

```text
dotnet run --file build/build.cs -- build [options]
```

| Option            | Default                                             | Notes                                                                                                                                                                                                                                                                                                                               |
|-------------------|-----------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `--lane`          | `v17`                                               | `v17` (Articulate 7.x for Umbraco 17) or `v18` (Articulate 8.x for Umbraco 18).                                                                                                                                                                                                                                                     |
| `--configuration` | `BUILD_CONFIGURATION` or `Release`                  | `Debug` or `Release`.                                                                                                                                                                                                                                                                                                               |
| `--tests`         | `RUN_TESTS`, otherwise true in CI                   | Run `dotnet test` after building.                                                                                                                                                                                                                                                                                                   |
| `--client`        | `ENABLE_CLIENT_BUILD`, otherwise true in Release/CI | Build the Backoffice client.                                                                                                                                                                                                                                                                                                        |
| `--sample`        | `PACK_SAMPLE_THEME`, otherwise true locally         | Pack `Articulate.Theme.Sample`.                                                                                                                                                                                                                                                                                                     |
| `--clean`         | `false`                                             | Clean source outputs, client assets, and packaged Backoffice assets first; required when switching lanes. It does not delete client dependencies or local test-site state. Use `site --reset` for an explicit site reset. Same-lane builds reuse client dependencies and, when tracked client inputs and build mode are unchanged, the incremental client stamp. |
| `--update-locks`  | `false`                                             | Regenerate both v17 and v18 NuGet lock files before building. Local-only; the command refuses to run in CI or act. |

Packages land in `build/<Configuration>/<lane>/`.

### client

```text
dotnet run --file build/build.cs -- client [--lane v17|v18]
```

Runs `pnpm install`, then `check`, `build`, and `lint` for the selected lane.

### site

```text
dotnet run --file build/build.cs -- site [options]
```

| Option            | Default | Notes                                         |
|-------------------|---------|-----------------------------------------------|
| `--lane`          | `v17`   | `v17` or `v18`.                               |
| `--configuration` | `Debug` | Build configuration.                          |
| `--reset`         | `false` | Delete the site's local `umbraco` data first. |

This command remains attached to the running site until stopped.

## Conventions

- Both lanes share project `bin`/`obj` and static-web-asset paths; full-solution
  lane builds run sequentially with `-m:1`.
- `ARTICULATE_PACKAGE_VERSION` overrides calculated package versions.
- `help` is the canonical build command and option reference.
