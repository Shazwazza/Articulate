# Articulate Release Notes

## Version 6.0.0

### Breaking Changes

> [!WARNING]
> **Platform requirements**
>
> - Minimum Umbraco version: **16.5.1** on .NET 9
> - Minimum Umbraco version: **17.2.2** on .NET 10
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

| Old (v5) | New (v6) |
| --- | --- |
| `@Html.AuthorCitation(Model)` | `@Model.AuthorCitation()` |
| `@Html.RenderOpenSearch(Model)` | `@Model.RenderOpenSearch()` |
| `@Html.RssFeed(Model)` | `@Model.RssFeed()` |
| `@Html.MetaTags(Model)` | `@Model.MetaTags()` |
| `@Html.GoogleAnalyticsTracking(Model)` | `@Model.GoogleAnalyticsTracking()` |
| `@Html.TagCloud(...)` | `@Model.Tags.TagCloud(...)` |
| `@Html.ThemedPartialAsync("Name")` | `@await Html.PartialAsync("Name")` |
| `@Url.ArticulateSearchUrl(Model)` | `@Model.ArticulateSearchUrl()` |
| `@Url.ArticulateRssUrl(Model)` | `@Model.ArticulateRssUrl()` |

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
