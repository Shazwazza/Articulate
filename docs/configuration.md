# Configuration

Articulate 6 and 7 use the `Articulate` section in `appsettings.json`.

## Core settings

| Setting                                           | Default    | Purpose                                                                                                                 |
|---------------------------------------------------|------------|-------------------------------------------------------------------------------------------------------------------------|
| `AutoGenerateExcerpt`                             | `true`     | Generate an excerpt when a post excerpt is empty                                                                        |
| `MaxImportImageBytes`                             | `10485760` | Maximum size of each imported or uploaded image                                                                         |
| `AllowedMediaHosts`                               | empty      | Hosts Articulate may use for external image downloads                                                                   |
| `AllowUnsafeLocalExternalImageHostsInDevelopment` | `false`    | Permit explicitly allowed local/private hosts outside Production                                                        |
| `BlogMlImportMaxXmlCharacters`                    | `10000000` | Maximum characters allowed in a BlogML XML document during import (maps to `XmlReaderSettings.MaxCharactersInDocument`) |

If `AllowedMediaHosts` is empty, posts still import but external images are not
downloaded. Redirect destinations must also be allowlisted. Articulate rejects
unsafe addresses, HTTPS-to-HTTP redirects, unsupported image types, and files
larger than `MaxImportImageBytes`.

```json
{
  "Articulate": {
    "AutoGenerateExcerpt": true,
    "MaxImportImageBytes": 10485760,
    "AllowedMediaHosts": [
      "images.example.com",
      "cdn.example.com"
    ],
    "AllowUnsafeLocalExternalImageHostsInDevelopment": false
  }
}
```

Only allow hosts you control or trust. The local/private-host override is
ignored when `Umbraco:CMS:Runtime:Mode` is `Production`.

### External image import safety

In the BlogML importer, choose **Verify file** before import to see which external image hosts are allowed or blocked. Posts still import when a host is blocked — only the external image download is skipped.

Full safety rules (allowlist redirects, no HTTPS→HTTP downgrade, IP pinning, loopback/private blocks, no ambient proxy/auth headers, Umbraco upload rules) live in the [Importing guide](https://github.com/Shazwazza/Articulate/wiki/Importing#external-image-import-and-trusted-hosts).

## BlogML export

The BlogML exporter (Articulate dashboard) only includes **published** posts.
Drafts and unpublished content are skipped — `BlogMlExporter.AddBlogPosts`
filters on `child.Published` with no toggle. There is no "export all" option
today; a flag to include unpublished content is a possible future enhancement.

## Markdown editor authentication

The standalone editor at `/a-new/` uses Umbraco's Backoffice OpenIddict
endpoints with authorization code flow and PKCE.

```json
{
  "Articulate": {
    "ManagementApi": {
      "OpenIddict": {
        "Client": {
          "Enabled": true,
          "ClientId": "umbraco-articulate",
          "DisplayName": "Articulate Markdown Editor",
          "RedirectUris": [
            "https://example.com/a-new/"
          ],
          "PostLogoutRedirectUris": [
            "https://example.com/"
          ]
        }
      }
    }
  },
  "Umbraco": {
    "CMS": {
      "WebRouting": {
        "UmbracoApplicationUrl": "https://example.com/"
      }
    }
  }
}
```

`RedirectUris` are sign-in callbacks. `PostLogoutRedirectUris` are final
destinations after sign-out; the sign-out endpoint itself does not belong in
that list. Use exact absolute HTTPS URLs.

The editor keeps its access token in memory, so refreshing the page requires a
new sign-in.

## Comment providers

Articulate can render Disqus or Giscus comments from the existing
`CommentsDisqus.cshtml` theme partial. Post-level `enableComments` still controls
whether a post shows comments at all.

For operator guidance, provider precedence, theming notes, and import caveats,
see the [Comments wiki page](https://github.com/Shazwazza/Articulate/wiki/Comments).

Giscus has two configuration surfaces:

1. **App-wide defaults** via `appsettings.json` — covers all optional + required fields.
2. **Per-blog overrides** on the Articulate doc type — only the four required fields
   (`giscusRepo`, `giscusRepoId`, `giscusCategory`, `giscusCategoryId`), live in the
   existing `blog` tab alongside `disqusShortname`. Added by the
   `AddGiscusPerBlogProperties` migration that runs on first boot.

### Giscus appsettings shape

```json
{
  "Articulate": {
    "Comments": {
      "Giscus": {
        "ScriptSrc": "https://giscus.app/client.js",
        "DataRepo": "owner/repository",
        "DataRepoId": "R_kgDOExample",
        "DataCategory": "Announcements",
        "DataCategoryId": "DIC_kwDOExample",
        "DataMapping": "pathname",
        "DataStrict": "0",
        "DataReactionsEnabled": "1",
        "DataEmitMetadata": "0",
        "DataInputPosition": "bottom",
        "DataTheme": "",
        "DataLang": "en",
        "DataLoading": ""
      }
    }
  }
}
```

### Required appsettings (or per-blog doc-type) fields

| Field            | Purpose                                 |
|------------------|-----------------------------------------|
| `DataRepo`       | GitHub repo (`owner/repository`)        |
| `DataRepoId`     | Repo ID from giscus.app (`R_...`)       |
| `DataCategory`   | Discussion category name                |
| `DataCategoryId` | Category ID from giscus.app (`DIC_...`) |

### Optional appsettings-only fields

The 9 below are appsettings-only — no per-blog doc-type override exists. Change requires an appsettings edit (no per-blog granularity).

| Setting                | Default                        | Purpose                                                                                                                                                                                     |
|------------------------|--------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ScriptSrc`            | `https://giscus.app/client.js` | Override for self-hosted giscus. Point at your own hosted client (see giscus SELF-HOSTING.md).                                                                                              |
| `DataMapping`          | `pathname`                     | Discussion ↔ page mapping: `pathname`, `url`, `title`, `og:title`, `specific`, or a specific term.                                                                                          |
| `DataStrict`           | `0`                            | `1` enables strict title matching to avoid fuzzy-search collisions.                                                                                                                         |
| `DataReactionsEnabled` | `1`                            | `0` hides reactions on the main post.                                                                                                                                                       |
| `DataEmitMetadata`     | `0`                            | `1` posts discussion metadata to the parent window (for `message` listeners).                                                                                                               |
| `DataInputPosition`    | `bottom`                       | `top` puts the comment box above the comments.                                                                                                                                              |
| `DataTheme`            | `""` (empty)                   | Giscus `data-theme`. Empty (default) auto-derives from the active theme's `giscus.css` (see below); a keyword (`light`, `dark`, `preferred_color_scheme`) or absolute CSS URL overrides it. |
| `DataLang`             | `en`                           | IETF language tag for the giscus widget UI.                                                                                                                                                 |
| `DataLoading`          | `""`                           | Set to `"lazy"` to defer iframe load until the user scrolls near the comments container.                                                                                                    |
| `AllowedCorsOrigins`   | `["https://giscus.app"]`       | Origins permitted to fetch the proxied `giscus.css` cross-origin. Articulate serves that stylesheet through the `GiscusThemeController`, which reads the theme asset from disk and returns it with the right CORS headers. Add a self-hosted giscus origin (`https://comments.example.com`) here. The request's `Origin` header is echoed in `Access-Control-Allow-Origin` only when it matches; otherwise the response omits the CORS header and the browser blocks the stylesheet. `Vary: Origin` is set on reflected responses. Empty array = no cross-origin CSS (same-origin callers still get `*`). |

`DataTheme` defaults to empty. In that mode Articulate uses the active theme's
`assets/giscus.css` through `/articulate/giscus-theme/{theme}` when available;
see [Comments](https://github.com/Shazwazza/Articulate/wiki/Comments#matching-giscus-to-your-theme).

### Advanced OpenIddict options

Most installations only need the settings above. The following optional keys
override the default Umbraco endpoints used by the Markdown editor:

| Setting          | Purpose                                       |
|------------------|-----------------------------------------------|
| `ClientType`     | `Public` (default) or `Confidential`.         |
| `ClientSecret`   | Required when `ClientType` is `Confidential`. |
| `AuthorizeUrl`   | Override the authorization endpoint.          |
| `TokenUrl`       | Override the token endpoint.                  |
| `EndSessionUrl`  | Override the end-session (sign-out) endpoint. |
| `RevocationUrl`  | Override the token-revocation endpoint.       |
| `CurrentUserUrl` | Override the current-user endpoint.           |
| `LoginLogoUrl`   | Override the login logo shown by the editor.  |

When `Enabled=true`, `ClientId` and at least one absolute `RedirectUris` entry
are required.

## Upload and request limits

`MaxImportImageBytes` limits each image after Articulate receives a request.
Large BlogML, WXR, Markdown, or MetaWeblog requests can be rejected earlier by
the hosting stack.

Keep these limits aligned:

- `Umbraco:CMS:Runtime:MaxRequestLength` (KB)
- ASP.NET Core `FormOptions.MultipartBodyLengthLimit` (bytes)
- Kestrel `Limits.MaxRequestBodySize` (bytes)
- IIS request limits (bytes), when IIS is used

If one layer has a lower limit, uploads can fail with `413 Payload Too Large`.
The full request limit must cover the import file plus multipart overhead.

Example for a 100 MB request limit:

```csharp
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 104857600;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 104857600;
});
```

```json
{
  "Umbraco": {
    "CMS": {
      "Runtime": {
        "MaxRequestLength": 102400
      }
    }
  }
}
```

`MaxRequestLength` is expressed in KB; the ASP.NET Core server limits above
are bytes. IIS-hosted deployments may also need the corresponding
`web.config` request-filtering limit.

## More detail

- [Wiki configuration guide](https://github.com/Shazwazza/Articulate/wiki/Configuration)
- [Importing](https://github.com/Shazwazza/Articulate/wiki/Importing)
- [Comments](https://github.com/Shazwazza/Articulate/wiki/Comments)
- [Migration from v5 to v6](https://github.com/Shazwazza/Articulate/wiki/Migration-from-v5-to-v6)
