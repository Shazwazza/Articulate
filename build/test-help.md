# Articulate E2E tests

Run from the repository root with .NET 10, Node 24 and [Client dependencies installed](../src/Articulate.Web/Client/e2e/README.md#install-and-run):

```text
dotnet run --file build/test.cs -- fresh [--lane v17|v18|all]
dotnet run --file build/test.cs -- e2e [--lane v17|v18|all]
dotnet run --file build/test.cs -- report --lane v17|v18
dotnet run --file build/test.cs -- help
```

`fresh` defaults to `all`. Build the selected lane's Release packages with `--sample` first. The runner inspects those archives, restores a copy of the existing packaged-site project outside the checkout, verifies the restored package hashes, and starts it directly with .NET. Each lane has its own temporary database, self-signed certificate, random secret and free loopback HTTPS port. No Docker or browser installation is required.

Preparation publishes and confirms starter content, restarts the host in Production, then verifies that a temporary post appears in `ExternalIndex` and is removed before testing. The shared 11-test suite runs with no retries. The runner stops its own process and deletes its own temporary directory on success or failure. Host logs remain under `.temp/art_e2e_native_<lane>_<id>/`; Playwright reports and test artefacts use the same candidate name under `src/Articulate.Web/Client/e2e-report-*` and `e2e-results-*`. Neither certificate trust stores nor existing sites are changed.

See [CI jobs and E2E](../BUILD.md#ci-jobs-and-e2e) for workflow selection and uploads.

Local `--lane all` runs are sequential and stop on failure. `NODE_BIN` and `DOTNET_BIN` can select existing executables without changing PATH.

`e2e` tests sites that are already running. It does not build packages or start sites. Only these dedicated manual-test URLs are allowed:

| Lane | Test URL |
| --- | --- |
| v17 | `https://localhost:19443` |
| v18 | `https://localhost:19444` |

Manual-site reports are in `src/Articulate.Web/Client/e2e-report-<lane>`; test artefacts are in `e2e-results-<lane>`. `report` opens the manual-site report (or, if absent, the newest fresh/candidate report for the lane) on an available loopback port. Playwright prints the URL. Stop it with Ctrl+C.

See the [E2E guide](../src/Articulate.Web/Client/e2e/README.md) for optional Docker deployment checks and manual-site setup. Do not use the normal `art_v17` or `art_v18` sites.
