# Articulate

[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](https://raw.githubusercontent.com/Shazwazza/Articulate/develop/assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

## Installation

Three support tracks are available depending on the Umbraco version you run.

### Umbraco 13 LTS (maintenance mode)

Articulate 5.x remains available for Umbraco 13, which is in security maintenance until **December 2025** and reaches end of life in **December 2026**. The package still installs from the Umbraco marketplace.

- After installing, open the Packages section (`umbraco/section/packages/view/installed`) and run any pending migrations.
- Save the `Articulate Image Picker` data type once to fix bundled demo media (issue [#460](https://github.com/Shazwazza/Articulate/issues/460)). This step is only required on Umbraco 13.
- For long-term projects consider upgrading to Umbraco 16+ where Articulate 6 receives active feature work.

_Need help?_ Head over to [Articulate on GitHub](https://github.com/Shazwazza/Articulate) for extra tips, known issues and fixes.

### Umbraco 16 (NET 9) & 17 (NET 10) (current track)

Articulate 6 targets Umbraco 16.5.1+ and 17.2.2+.

- Install `Articulate` from NuGet (`dotnet add package Articulate`). The package includes the backoffice extension and static assets; no extra package references or manual copies required.
- When building from source, run the test site with `-f net9.0` for Umbraco 16 or `-f net10.0` for Umbraco 17, then sign into the Umbraco Back Office to finish setup.
- Migrating from 5.x: in place upgrade or export BlogML from your Articulate 5 site and import it into Articulate 6; media in `media/articulate` is not auto-migrated. During import you can map `postImage` to base64 or an attachment; other inline images must be moved manually (copy the folder, or consider an in-place package upgrade).

### Umbraco 18 (NET 10) opt-in

Articulate 7 targets Umbraco 18 on `net10.0`.

- Articulate 6.x packages are for Umbraco 16/17.
- Articulate 7.x packages are for Umbraco 18.
- The source tree can build both package lanes, but the resulting NuGet packages are separate because the compiled Umbraco 17 and Umbraco 18 integration points are not binary-compatible.
- Articulate 7 will be available on both the Umbraco Marketplace and NuGet when Umbraco 18 is released.
- For development or early validation, see `DEVELOP.md` for lane build and Docker commands.

#### Rich Text Editor upgrade behavior

Articulate will migrate the built-in `Umbraco.RichText` property editor to `Umb.PropertyEditorUi.TipTap` during package upgrade only if the TinyMCE editor UI is not registered. This applies to all Articulate versions (6 and 7).

- You must have the [TinyMCE.Umbraco](https://github.com/ProWorksCorporation/TinyMCE-Umbraco) package installed before you start your site to keep using TinyMCE after upgrade.
- This setting affects upgrades only. Once the Articulate migration plan step has executed, Umbraco records it as complete.

### Theme Structure (Articulate 6)

Articulate 6 separates built-in theme views from static assets:

- Built-in Razor views live under `src/Articulate.Web/App_Plugins/Articulate/Themes/{Theme}/Views/`
- Built-in static assets live under `src/Articulate.Web/wwwroot/App_Plugins/Articulate/Themes/{Theme}/assets/`

For copied or custom user themes, the preferred layout is:

- views in `Views/ArticulateThemes/{Theme}/Views/`
- assets in `wwwroot/App_Plugins/Articulate/Themes/{Theme}/assets/`

Reusable RCL/NuGet theme packages can also contribute themes to the Articulate theme picker by registering `IArticulateThemeDescriptorProvider` and returning one or more canonical theme keys. Those keys should match the package theme folder name and the theme picker value. Built-in theme names are reserved, and duplicate package keys are ignored.

## Markdown Editor Authentication

The standalone Markdown editor uses Umbraco's built-in back-office OpenIddict endpoints with the authorization code flow and PKCE.

- `RedirectUris` are the allowed callback URLs after a successful sign-in.
- `PostLogoutRedirectUris` are the allowed final destinations after sign-out completes.
- The built-in sign-out endpoint is an endpoint the client calls. It is **not** itself a post-logout redirect URI.
- The editor requests a specific `post_logout_redirect_uri` during sign-out. Umbraco/OpenIddict will only honor it if it exists in `PostLogoutRedirectUris`.
- The editor keeps the access token in memory. Refreshing the page clears that token and requires the user to sign in again.

Minimal example:

```json
"Articulate": {
  "ManagementApi": {
    "OpenIddict": {
      "Client": {
        "Enabled": true,
        "ClientId": "umbraco-articulate",
        "DisplayName": "Articulate Markdown Editor",
        "RedirectUris": [
          "https://localhost:44366/a-new/"
        ],
        "PostLogoutRedirectUris": [
          "https://localhost:44366/"
        ]
      }
    }
  }
}
```

## BlogML External Image Import

Articulate treats external BlogML image import as an opt-in convenience feature for trusted hosts.

- If `Articulate:AllowedMediaHosts` is empty, external image downloads are disabled.
- BlogML posts still import normally; this only affects fetching the first external image attachment when `Import First Image from Post Attachments` is enabled.
- In the backoffice importer, click `Verify file` after selecting a BlogML file to analyze it before import. The summary shows:
  - the number of external image attachments
  - the unique external hosts referenced by the file
  - which hosts are currently allowed
  - which hosts are currently blocked by the external image safety policy
- Redirects are limited and revalidated on every hop. Redirect targets must still be allowlisted in `Articulate:AllowedMediaHosts`, pass IP safety checks, and cannot downgrade from `https` to `http`.
- This supports common CDN-style redirects such as `images.example.com` redirecting to `cdn.example.com`, as long as both hosts are explicitly allowlisted.
- Downloads are validated against Umbraco upload rules and image file types, capped by size, and pinned to the validated IP address for the actual connection.
- External image downloads use direct validated connections and do not inherit ambient proxy settings or default authentication headers from application `HttpClient` configuration.

Production-oriented example:

```json
{
  "Articulate": {
    "MaxImportImageBytes": 10485760,
    "AllowedMediaHosts": [
      "images.example.com",
      "cdn.example.com"
    ],
    "AllowUnsafeLocalExternalImageHostsInDevelopment": false
  },
  "Umbraco": {
    "CMS": {
      "Runtime": {
        "Mode": "Production"
      }
    }
  }
}
```

Local development example:

```json
{
  "Articulate": {
    "MaxImportImageBytes": 10485760,
    "AllowedMediaHosts": [
      "localhost"
    ],
    "AllowUnsafeLocalExternalImageHostsInDevelopment": true
  },
  "Umbraco": {
    "CMS": {
      "Runtime": {
        "Mode": "BackofficeDevelopment"
      }
    }
  }
}
```

Notes:

- Only add hosts you control or strongly trust.
- `AllowUnsafeLocalExternalImageHostsInDevelopment` is ignored when `Umbraco:CMS:Runtime:Mode` is `Production`.
- `localhost`, loopback, and private-network targets remain blocked unless the development-only override is enabled.
- For BlogML export/import round-trip tests, use an importer-reachable media hostname end-to-end. `localhost` only works when the importer resolves it to the exporting site. If not, rewrite the BlogML media URLs before import or configure the exporting site to emit a reachable hostname instead.
- See [DEVELOP.md](DEVELOP.md) for Docker-based development and testing notes.

## Upload and Request Limits

Images entering Articulate import/editor flows are validated against Umbraco upload rules and image file types, and capped by `Articulate:MaxImportImageBytes` (`10 MB` by default).

Large BlogML files and MetaWeblog XML-RPC requests can hit hosting request-size limits before Articulate receives them.

These limits are related but separate: `MaxImportImageBytes` caps each image Articulate accepts after a request is being processed; the startup/server request limits below cap the total HTTP request size before import or MetaWeblog processing can run.

Keep these aligned:

- `Umbraco:CMS:Runtime:MaxRequestLength`
- ASP.NET Core `FormOptions.MultipartBodyLengthLimit`
- Kestrel `Limits.MaxRequestBodySize`
- IIS `MaxRequestBodySize` when applicable

The local site config uses:

```csharp
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100MB
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 104857600; // 100MB
});
```

The included site uses the same `FormOptions` and Kestrel configuration pattern.

Appsettings example:

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

If you still rely on IIS `web.config` request settings, update those too:

```xml
<configuration>
  <system.web>
    <httpRuntime maxRequestLength="102400" />
  </system.web>
  <system.webServer>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="104857600" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

If one layer remains lower than the others, uploads can still fail with `413 Payload Too Large`.

## Features

Supporting all the features you'd want in a blogging platform

- Categories & Tags
- Themes
- Multiple archives
- Live Writer support
- Markdown support
- Post from your mobile phone including photos direct from your camera
- Disqus comment support (or build your own)
- Search
- BlogML import/export (including Disqus import)
- Customizable RSS feeds
- Customizable URLs
- Author profiles

## Disqus comments

Built-in themes render Disqus comments only when both post comments are enabled and the Articulate root has a valid `disqusShortname` value. Leave `disqusShortname` empty to disable the Disqus widget entirely; themes should not render placeholder comment panels or load Disqus scripts without it.

## Minimum requirements

- Articulate 5.x (maintenance): Umbraco 13 LTS (security support through Dec 2025, EOL Dec 2026)
- Articulate 6.x (current): Umbraco 16.5.1+ on .NET 9; Umbraco 17.4.0+ on .NET 10
- Articulate 7.x (Umbraco 18 track): Umbraco 18+ on .NET 10

### Umbraco 18 beta validation (opt-in)

This branch enables opt-in validation against Umbraco 18 beta on `net10.0` using the Articulate 7 package lane.

See [DEVELOP.md](DEVELOP.md) for package-lane build commands and local Docker usage.

## [Documentation](https://github.com/Shazwazza/Articulate/wiki)

Docs on installation, creating posts, customizing/creating themes, etc...

For Umbraco 16/17 upgrades, also see the rich text editor upgrade behavior notes above if you need to preserve TinyMCE compatibility.

## [Issues](https://github.com/Shazwazza/Articulate/issues)

If you have any issues, please post them here on GitHub

## [Releases](https://github.com/Shazwazza/Articulate/releases)

See here for the list of releases and their release notes

## [Community Discussions](https://forum.umbraco.com/tag/packages)

- Please use the Umbraco forums to ask questions and discuss Articulate, it's features and functionality.
- Do not post issues here, post them to [Articulate/issues](https://github.com/Shazwazza/Articulate/issues) on GitHub

## Development

Local development and contributor setup lives in [DEVELOP.md](DEVELOP.md).

## Copyright & License

&copy; 2026 by Shannon Deminick

This is free software and is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT)

