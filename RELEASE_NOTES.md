# Articulate Release Notes

## Version 7.0.0

- Adds the Articulate 7 line for Umbraco 18 on .NET 10.
- Uses the Umbraco 18 Backoffice client and native OpenAPI endpoints.
- Adds Giscus as a comment provider alongside Disqus. Required settings
  (`DataRepo`, `DataRepoId`, `DataCategory`, `DataCategoryId`) are configurable
  globally via `Articulate:Comments:Giscus` in `appsettings.json`. If a blog has
  all four fields populated on the Articulate doc type (in the **blog** tab,
  after `disqusShortname`), those values override appsettings for that blog only.
  Partial overrides are ignored — the blog falls through entirely to appsettings.
  If both a Disqus shortname and Giscus options are configured on the same blog,
  Disqus wins; clear the shortname to enable Giscus.
- Optional Giscus settings exposed: `ScriptSrc`, `DataMapping`, `DataStrict`,
  `DataReactionsEnabled`, `DataEmitMetadata`, `DataInputPosition`, `DataTheme`,
  `DataLang`, `DataLoading` (set to `"lazy"` to defer the iframe until scroll-near).
  All optional settings are appsettings-only — there is no per-blog override.
- `DataTheme` defaults to empty, which **auto-derives** from the active theme's
  `giscus.css` via the `/articulate/giscus-theme/{theme}` endpoint — a CORS-enabled
  proxy of `/App_Plugins/Articulate/Themes/{theme}/assets/giscus.css` that
  works for built-in, copied/forked, and RCL themes uniformly. Set `DataTheme`
  to a giscus keyword (`light`, `dark`, `preferred_color_scheme`) or absolute CSS
  URL to override. See [`docs/configuration.md`](docs/configuration.md#matching-comments-to-your-theme)
  for the giscus iframe CORS/localhost story.
- A new migration (`AddGiscusPerBlogProperties`) adds the four per-blog Giscus
  properties to the existing `blog` tab of the Articulate doc type for existing
  installs. Existing blogs get empty values, so behavior is unchanged until the
  fields or matching appsettings are populated.
- The rendered Giscus script tag now includes `crossorigin="anonymous"` and
  `async` (matching the canonical giscus snippet from giscus.app).
- Ships separately from Articulate 6 because the Umbraco 17 and 18 extension
  points are not binary-compatible.
- Hardened external-image imports against SSRF, malicious redirects, and
  HTTPS-downgrade attacks.

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

## Version 6.1.0

- Targets Umbraco 17.4 and later on .NET 10.
- Continues the Articulate 6 package line for supported Umbraco 17 sites.
- Articulate 6.0 remains the previous compatibility line for Umbraco 16 and 17.

## Version 6.0.0

### Breaking Changes

> [!WARNING]
> **Platform requirements**
>
> - Minimum Umbraco version: **17.4.0** on .NET 10
> - Umbraco 15 and earlier are no longer supported by Articulate 6

- Articulate 6 targets `net10.0` and supports Umbraco 16 and 17 from a single package.
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
