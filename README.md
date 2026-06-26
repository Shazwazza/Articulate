# Articulate

[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](https://raw.githubusercontent.com/Shazwazza/Articulate/develop/assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

## Installation

Install the version of Articulate that matches your Umbraco installation:

| Umbraco                | Articulate | Status           |
|------------------------|------------|------------------|
| Umbraco 18             | 7.x        | Current          |
| Umbraco 17.4+          | 6.1.x      | Current          |
| Umbraco 16.5+ or 17.4+ | 6.0.x      | Previous release |
| Umbraco 13 LTS         | 5.x        | Maintenance      |

For Umbraco 17+, install from NuGet:

```shell
dotnet add package Articulate    # Umbraco 17 / Articulate 6.1, or Umbraco 18 / Articulate 7.0
```

After installation, open Umbraco and complete any on-screen migration prompts.

## Features

Supporting all the features you'd want in a blogging platform:

- Categories & Tags
- Themes
- Multiple archives
- Live Writer support
- Markdown support
- Post from your mobile phone including photos direct from your camera
- Disqus and Giscus comment support (or build your own)
- Search
- BlogML import/export (including Disqus import)
- Customizable RSS feeds
- Customizable URLs
- Author profiles

## Upgrading

Back up your site, database, and media before upgrading.

See the [installation and upgrade guide](docs/upgrading.md) for version
selection, rich-text editor migration, BlogML guidance, and post-upgrade checks.

## Themes

Articulate includes ready-to-use themes and supports custom themes. You can
copy an existing theme as a starting point or install a theme supplied by
another package.

See [Themes](docs/themes.md) for customization guidance.

## Configuration

Articulate settings live in `appsettings.json` under the `Articulate` section.
See [Configuration](docs/configuration.md) for the settings reference,
external image allowlisting, Markdown editor authentication, and request-limit
guidance.

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
