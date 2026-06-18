# Local Docker Site

`docker-compose.yml` defines the containers. Cross-platform orchestration lives in the .NET 10
file-based app at `build/build.cs`; `smoke.mjs` contains the Management API assertions.

## Commands

```text
dotnet run --file build/build.cs -- docker-build --lane v17
dotnet run --file build/build.cs -- docker-dev
dotnet run --file build/build.cs -- docker-prod
dotnet run --file build/build.cs -- docker-test --lane all
```

Options:

- `docker-dev --skip-smoke`: boot and readiness only.
- `docker-dev --reset`: remove volumes first.
- `docker-test --lane v17|v18|all`: choose lanes.
- `docker-test --keep`: leave successful stacks running.
- `docker-test --skip-smoke`: build, install, migrate, and check `/umbraco/`.

Full smoke tests require `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`.

| Lane | Image | HTTPS |
| --- | --- | --- |
| `v17` | `articulate-local:v17` | `https://localhost:17017/` |
| `v18` | `articulate-local:v18` | `https://localhost:18018/` |

Trust Caddy's local CA once per machine:

- Windows: `powershell -ExecutionPolicy Bypass -File build/docker-site/Trust-CaddyRootCA.ps1`
- Linux/WSL: `sudo build/docker-site/trust-caddy-root-ca.sh`

Those helpers remain platform-specific because certificate stores are platform-specific.

Against an already healthy stack, `smoke.mjs` supports `publish`, `confirm`, `smoke`, and `theme`.
Package inputs come from `build/Release/<lane>` and must include Articulate and the sample theme.

## Umbraco MCP Dev

For interactive inspection, `@umbraco-cms/mcp-dev` can reuse the dev automation API user's
credentials:

- `UMBRACO_CLIENT_ID` = `ARTICULATE_DEV_AUTOMATION_CLIENT_ID`
- `UMBRACO_CLIENT_SECRET` = `ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET`
- `UMBRACO_BASE_URL` = the lane's public URL

Do not use the `umbraco-articulate` OpenID client: that is the Markdown Editor's browser client,
not an API user client-credentials identity. MCP complements `smoke.mjs`; it does not replace the
deterministic publish, state, front-end, and theme assertions used by the Docker test command.
