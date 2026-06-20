# Configuration

Articulate 6.1 and 7 use the `Articulate` section in `appsettings.json`.

## Core settings

| Setting                                           | Default    | Purpose                                                          |
|---------------------------------------------------|------------|------------------------------------------------------------------|
| `AutoGenerateExcerpt`                             | `true`     | Generate an excerpt when a post excerpt is empty                 |
| `MaxImportImageBytes`                             | `10485760` | Maximum size of each imported or uploaded image                  |
| `AllowedMediaHosts`                               | empty      | Hosts Articulate may use for external image downloads            |
| `AllowUnsafeLocalExternalImageHostsInDevelopment` | `false`    | Permit explicitly allowed local/private hosts outside Production |

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

### Advanced OpenIddict options

Most installations only need the settings above. The following optional keys
override the default Umbraco endpoints used by the Markdown editor:

| Setting                | Purpose                                              |
|------------------------|------------------------------------------------------|
| `ClientType`           | `Public` (default) or `Confidential`.                |
| `ClientSecret`         | Required when `ClientType` is `Confidential`.        |
| `AuthorizeUrl`         | Override the authorization endpoint.                 |
| `TokenUrl`             | Override the token endpoint.                         |
| `EndSessionUrl`        | Override the end-session (sign-out) endpoint.        |
| `RevocationUrl`        | Override the token-revocation endpoint.              |
| `CurrentUserUrl`       | Override the current-user endpoint.                  |
| `LoginLogoUrl`         | Override the login logo shown by the editor.         |

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
- [Settings reference](https://github.com/Shazwazza/Articulate/wiki/Settings-Reference)
- [Markdown editor authentication](https://github.com/Shazwazza/Articulate/wiki/Markdown-Editor-Authentication)
- [Importing](https://github.com/Shazwazza/Articulate/wiki/Importing)
