# Articulate Release Notes

## Version 7.0.0-rc1

- Adds the Articulate 7 release-candidate line for Umbraco 18 on .NET 10.
- Uses the Umbraco 18 Backoffice client and native OpenAPI endpoints.
- Ships separately from Articulate 6 because the Umbraco 17 and 18 extension
  points are not binary-compatible.
- The release base version is defined in `build/v18-version.txt`; development
  builds append commit metadata.

### Changes in this release

- Hardened the image-import service against SSRF (per-redirect revalidation,
  pinned connections, IANA special-use IP coverage, HTTPS-downgrade rejection).
- Replaced the PowerShell/Bash build scripts with a single `build/build.cs`
  .NET tool and added lane-aware NuGet package locking
  (`packages.v17.lock.json` / `packages.v18.lock.json`, `RestoreLockedMode`).
- Tightened the dev Docker harness: Caddy ports bind to loopback by default
  (override with `CADDY_BIND_IP`); Caddy image pinned to `2.11.4-alpine`;
  per-lane Backoffice auth-cookie isolation via `AuthCookieName` +
  `BackOfficeTokenCookie:SiteName` so v17 and v18 can run side-by-side without
  trampling each other's sessions.
- Restored the markdown editor's `monacoMarkdownEditorAction` extension point
  (the toolbar action slot was querying a type that could never match; package-
  contributed actions can now load).
- Removed a dead namespace literal in `ArticulateOperationIdHandler`.
- Added tests for the OpenAPI operation-id handler, the tag-repository SQL
  builders, the route-cache lifecycle, and the date-formatted URL provider.
- **NuGet packaging:** BackOffice Vite output now ships as `staticwebassets/`
  only (no duplicate `content/` + `contentFiles/any/{tfm}/` paths, ~140 KB
  per package).
- **Smoke test:** `build/smoke-package.mjs` now asserts the
  `staticwebassets/`-only layout. CI runs it after both lanes pack and skips
  the artifact upload on failure.

### Breaking changes

> [!WARNING]
> **Dev harness only — does not affect production installs.**
>
> - The Docker dev harness now binds Caddy to `127.0.0.1` by default instead
>   of `0.0.0.0`. If you relied on reaching the dev site from another machine
>   on your LAN, set `CADDY_BIND_IP=0.0.0.0` in your environment.
> - The markdown editor's monaco action extension point is renamed to
>   `monacoMarkdownEditorAction` (matching Umbraco). Any manifest registered
>   under the previous `articulateMonacoMarkdownEditorAction` /
>   `articulateMarkdownEditorAction` names must be updated — though those names
>   never matched each other, so such a manifest could not have loaded anyway.

## Version 6.1.0-rc1

- Targets Umbraco 17.4 and later on .NET 10.
- Continues the Articulate 6 package line for supported Umbraco 17 sites.
- Articulate 6.0 remains the previous compatibility line for Umbraco 16 and 17.

## Version 6.0.0

### Breaking Changes

> [!WARNING]
> **Platform requirements**
>
> - Minimum Umbraco version: **16.5.1** on .NET 9
> - Minimum Umbraco version: **17.4.0** on .NET 10
> - Umbraco 15 and earlier are no longer supported by Articulate 6

- Articulate 6 is multi-targeted for `net9.0` and `net10.0`, supporting Umbraco 16 and 17 from a single package.
- The old split-project/package layout has been consolidated. Articulate now ships as the main package with the backoffice extension and static assets included.
- Markdown conversion services were renamed:
  - `IMarkdownToHtmlConverter` -> `IArticulateMarkdownConverter`
  - `MarkdownService` -> `ArticulateMarkdownService`
- Obsolete model constructors that accepted `IVariationContextAccessor` have been removed.
- `ListModel` now requires an explicit `listItems` collection. The older fallback behavior that discovered posts through Umbraco services is no longer available.
- Built-in Articulate themes can still be copied, but copied themes cannot use a built-in theme name as the destination.
- `redirectArchive` no longer controls the `/authors/` directory. Themes that provide `Authors.cshtml` render the authors directory; themes without it redirect `/authors/` to the blog root.

### Theme Migration

For Razor themes migrating from older Articulate versions, helper usage should move from `Html` and `Url` helpers to model extension methods:

| Old (v5)                               | New (v6)                           |
|----------------------------------------|------------------------------------|
| `@Html.AuthorCitation(Model)`          | `@Model.AuthorCitation()`          |
| `@Html.RenderOpenSearch(Model)`        | `@Model.RenderOpenSearch()`        |
| `@Html.RssFeed(Model)`                 | `@Model.RssFeed()`                 |
| `@Html.MetaTags(Model)`                | `@Model.MetaTags()`                |
| `@Html.GoogleAnalyticsTracking(Model)` | `@Model.GoogleAnalyticsTracking()` |
| `@Html.TagCloud(...)`                  | `@Model.Tags.TagCloud(...)`        |
| `@Html.ThemedPartialAsync("Name")`     | `@await Html.PartialAsync("Name")` |
| `@Url.ArticulateSearchUrl(Model)`      | `@Model.ArticulateSearchUrl()`     |
| `@Url.ArticulateRssUrl(Model)`         | `@Model.ArticulateRssUrl()`        |

URL-bearing background images in Razor themes should be assigned through CSS custom properties with `ToCssBackgroundImageVariableValue(...)`. The legacy `BlogLogoCss` and `BlogBannerCss` APIs remain as obsolete compatibility shims, but are scheduled for removal in a future release.

### Internal API Updates

These changes mainly affect custom extensions that inherit from Articulate classes:

- `DateFormattedUrlProvider` now inherits from `NewDefaultUrlProvider`.
- `DateFormattedPostContentFinder` now inherits from `ContentFinderByUrlNew`.
- `MigrateDataTypeConfigurationBase` now inherits from `AsyncMigrationBase`, and custom migrations should implement async migration methods.

### Notes

- The standalone Markdown editor now keeps its access token in memory. Refreshing the page clears the token and requires sign-in again.
- Custom themes should provide the expected views, including `Authors.cshtml`, where applicable.

---

## Version 5.x

For release notes from previous versions, see the [GitHub Releases](https://github.com/Shazwazza/Articulate/releases) page.
