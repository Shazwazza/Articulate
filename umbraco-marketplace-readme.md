# Articulate

[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](https://raw.githubusercontent.com/Shazwazza/Articulate/develop/assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

## Features

Supporting all the features you'd want in a blogging platform:

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

## Minimum requirements

- Umbraco 18.1.1+ - Articulate version 8.x
- Umbraco 17.6.2+ - Articulate version 7.x
- Umbraco 16.5.1+ or early Umbraco 17.4.0+ - Articulate version 6.x (deprecated)
- Umbraco 13 LTS (maintenance) - Articulate 5.x.

## Installation

Install the package version that matches your Umbraco installation. See the
[Installation guide](https://github.com/Shazwazza/Articulate/wiki/Installation)
for the compatibility matrix and version-specific commands.

## Upgrading

Back up your site, database, and media before upgrading.

On Umbraco 17 or 18, install [TinyMCE.Umbraco](https://github.com/ProWorksCorporation/TinyMCE-Umbraco) before first run to keep TinyMCE as your rich-text editor. Articulate migrates `Umbraco.RichText` to TipTap on first boot; the TinyMCE package must be present before that step.

See [Installation](https://github.com/Shazwazza/Articulate/wiki/Installation) and [Upgrading Articulate](https://github.com/Shazwazza/Articulate/wiki/Upgrading) for version selection, editor migration, BlogML guidance, and post-upgrade checks.

## Learn more

- [Documentation](https://github.com/Shazwazza/Articulate/wiki/)
- [Releases](https://github.com/Shazwazza/Articulate/releases)
- [Community discussions](https://forum.umbraco.com/tag/packages)
- [Report an issue](https://github.com/Shazwazza/Articulate/issues)

## Copyright and license

&copy; 2026 Shannon Deminick

Articulate is free software licensed under the
[MIT License](https://opensource.org/licenses/MIT).
