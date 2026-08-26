# Articulate Release Notes

## Version 8.0.0

### Breaking changes for v8.0.0

  > [!WARNING]
  > Requires Umbraco 18.1.1 through 18.x on .NET 10.
  >
  > Articulate 8 is a separate package line from Articulate 7 because Umbraco 18
  > APIs and backoffice extension points are not binary-compatible.

- Adds the Articulate 8 package line for Umbraco 18.
- Includes the functional changes listed under [Version 7.0.0](#version-700).

## Version 7.0.0

### Breaking changes for v7.0.0

  > [!WARNING]
  > Requires Umbraco 17.6.2 through 17.x on .NET 10. Umbraco 16 is not supported.
  >
  > Articulate 7 is a separate package line from Articulate 6 because Umbraco 17
  > APIs and backoffice extension points are not binary-compatible.

- Adds the Articulate 7 package line for Umbraco 17.
- Adds lane-specific OpenAPI, Swagger, and generated backoffice API support.
- Adds the Umbraco 17 backoffice client lane and updated Markdown editor assets.
- Package upgrades migrate the built-in rich-text editor to TipTap unless `TinyMCE.Umbraco` is installed before startup.
- Organizes the built-in post URL-alias and import-ID properties into SEO and System property groups during migration.

## Version 6.0.0

### Breaking Changes for v6.0.0

> [!WARNING]
> Minimum Umbraco version: **16.5.1** on .NET 9
> Umbraco 17 support starts at **17.4.0** on .NET 10; [Version 7.0.0](#version-700) supersedes this and is the recommended version for Umbraco 17.
> Umbraco 15 and earlier are no longer supported by Articulate 6

- Articulate 6 is deprecated; these notes document compatibility for existing installations.
- Articulate 6 is multi-targeted for `net9.0` and `net10.0`, supporting Umbraco 16 and 17 from a single package.
- The old split-project/package layout has been consolidated. Articulate now ships as the main package with the backoffice extension and static assets included.
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

### Notes

- The standalone post editor now keeps its access token in memory. Refreshing the page clears the token and requires sign-in again.
- Custom themes should provide the expected views, including `Authors.cshtml`, where applicable.

---

## Version 5.x

For release notes from previous versions, see the [GitHub Releases](https://github.com/Shazwazza/Articulate/releases) page.
