# Articulate Docker utility

```text
dotnet run --file docker/run.cs -- <command> [options]
dotnet run --file docker/run.cs -- help [command]
```

| Command         | Purpose                                                             |
|-----------------|---------------------------------------------------------------------|
| `docker-build`  | Build the standalone chiseled image.                                |
| `docker-dev`    | Start the development stack, publish and confirm content.          |
| `docker-prod`   | Restart the existing image in Production mode and check readiness.  |
| `docker-down`   | Stop one or both lanes; optionally remove volumes or all resources. |
| `docker-status` | Inspect containers and packaged Backoffice assets.                  |
| `docker-test`   | Run package, startup, Production-readiness and HTTP E2E checks.       |
| `docker-ca`     | Export and trust Caddy's local root CA.                             |

Commands default to lane `v17`; `docker-test` defaults to both lanes.

### docker-build

```text
dotnet run --file docker/run.cs -- docker-build [--lane v17|v18] [--clean] [--tag image:tag]
```

Builds current packages through `build/build.cs` before creating `articulate-local:<lane>` unless a tag is supplied. `--clean` rebuilds the package lane from clean outputs.

### docker-dev

```text
dotnet run --file docker/run.cs -- docker-dev [--lane v17|v18] [--clean] [--reset] [--skip-smoke] [--reuse-packages]
```

Builds current packages through `build/build.cs` and builds the Docker image, starts the development stack, then publishes and confirms content. Use `--reuse-packages` to skip the package build and use the existing `build/Release/<lane>` output; this is useful when only the Docker stack needs restarting. It cannot be combined with `--clean`. Docker package refreshes preserve the host test site's `umbraco` state; Docker database state lives in named volumes. `--reset` removes the lane's Docker volumes first; `--skip-smoke` stops after readiness succeeds.

### docker-prod

```text
dotnet run --file docker/run.cs -- docker-prod [--lane v17|v18] [--skip-smoke]
```

Recreates the lane's existing image in Production mode. Run `docker-dev` or `docker-build` first when package contents changed.

### docker-down

```text
dotnet run --file docker/run.cs -- docker-down [--lane v17|v18|all] [--volumes|--purge]
```

`--lane all` stops both lanes. `--volumes` removes volumes for the selected lane. `--purge` removes the selected lane's containers, volumes, service images, and orphans.

### docker-status

```text
dotnet run --file docker/run.cs -- docker-status [--lane v17|v18]
```

Shows the lane's containers and verifies the packaged Backoffice files inside the running site.

### docker-test

```text
dotnet run --file docker/run.cs -- docker-test [--lane v17|v18|all] [--keep] [--skip-smoke] [--reuse-packages]
```

Each lane uses a unique Compose project, named volumes, image tag, and free loopback ports. It inspects the package, builds the image from that lane's package output, starts a fresh Development site, publishes and confirms its starter content, checks Production root and publication-indexing readiness, and runs the existing HTTP Playwright suite. It then removes only that candidate project's containers and volumes and its unique image tag. `--reuse-packages` uses the current `build/Release/<lane>` output. This command is optional deployment coverage using the same suite as native E2E. CI runs `build/test.cs -- fresh` instead. `--keep` leaves the candidate project and image for debugging. `--skip-smoke` retains the startup-only path and skips publish/confirm, Production, and E2E checks.

Indexing readiness publishes one temporary post, waits until Umbraco's Examine query API returns its exact key from `ExternalIndex`, then deletes it and verifies removal. This uses the existing 300-second preparation budget; E2E assertions and timeouts do not change. The check runs only on disposable candidate projects.

### HTTP E2E tests

See the [E2E guide](../src/Articulate.Web/Client/e2e/README.md) for the standalone runner and suite details. `docker-test` provisions and cleans its own candidate projects; it never targets `art_v17`, `art_v18`, `art_e2e_v17`, or `art_e2e_v18`.

### docker-ca

```text
dotnet run --file docker/run.cs -- docker-ca [--lane v17|v18]
```

Exports and trusts the running lane's Caddy root CA. On Windows, it writes to the current user's root certificate store. Run it only if you want to trust that CA.
