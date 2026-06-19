# Articulate

[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](https://raw.githubusercontent.com/Shazwazza/Articulate/develop/assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

## Installation

Install the version of Articulate that matches your Umbraco installation:

| Umbraco | Articulate | Status |
| --- | --- | --- |
| Umbraco 18 | 7.x | Release candidate |
| Umbraco 17.4+ | 6.1.x | Current |
| Umbraco 16.5+ or 17.4+ | 6.0.x | Previous release |
| Umbraco 13 LTS | 5.x | Maintenance |

For Umbraco 17+, install from NuGet. The Umbraco 18 / Articulate 7.x
release candidate line requires opting in to prereleases:

```shell
dotnet add package Articulate             # Umbraco 17 / Articulate 6.x
dotnet add package Articulate --prerelease  # Umbraco 18 / Articulate 7.x release candidate
```

## Features

Supporting all the features you'd want in a blogging platform
The package includes its Backoffice extension, themes, and static assets.
After installation, open Umbraco and follow any on-screen migration prompts.

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
See the [installation and upgrade guide](docs/upgrading.md) for version
selection and migration advice.

## Upgrading

Back up your site and database before upgrading.

### From Articulate 5

You can upgrade in place or move content with BlogML export/import. Media under
`media/articulate` is not moved automatically by BlogML, so review image paths
as part of the migration.

### Rich-text editor compatibility

When upgrading, Articulate migrates its built-in rich-text editor to Umbraco's
TipTap editor if a TinyMCE editor UI is unavailable.

To continue using TinyMCE, install
[TinyMCE.Umbraco](https://github.com/ProWorksCorporation/TinyMCE-Umbraco)
before starting the upgraded site for the first time.

## Themes

Articulate includes ready-to-use themes and supports custom themes. You can
copy an existing theme as a starting point or install a theme supplied by
another package.

See [Themes](docs/themes.md) for customization guidance.

## Importing external images

BlogML can optionally download external post images. For safety, downloads are
disabled unless their host is explicitly listed in
`Articulate:AllowedMediaHosts`.

Only allow hosts you control or trust. Articulate validates redirects, file
types, and download sizes before accepting an image.

See [Configuration](docs/configuration.md) for allowlisting, request limits,
Markdown editor authentication, and examples.

## Getting help

- [Documentation](https://github.com/Shazwazza/Articulate/wiki)
- [Settings reference](https://github.com/Shazwazza/Articulate/wiki/Settings-Reference)
- [Markdown editor authentication](https://github.com/Shazwazza/Articulate/wiki/Markdown-Editor-Authentication)
- [Releases](https://github.com/Shazwazza/Articulate/releases)
- [Report a bug](https://github.com/Shazwazza/Articulate/issues)
- [Community discussions](https://forum.umbraco.com/tag/packages)

Please use GitHub Issues for reproducible bugs and the Umbraco forum for usage
questions and general discussion.

## Contributing

Repository setup, builds, tests, and Docker workflows are documented in
[DEVELOP.md](DEVELOP.md).

## Copyright and license

&copy; 2026 Shannon Deminick

Articulate is free software licensed under the
[MIT License](https://opensource.org/licenses/MIT).
