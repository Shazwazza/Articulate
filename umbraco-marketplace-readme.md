# Articulate

[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](https://raw.githubusercontent.com/Shazwazza/Articulate/develop/assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

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

## Compatibility

| Umbraco                | Articulate | Status            |
|------------------------|------------|-------------------|
| Umbraco 18             | 7.x        | Release candidate |
| Umbraco 17.4+          | 6.1.x      | Current           |
| Umbraco 16.5+ or 17.4+ | 6.0.x      | Previous release  |
| Umbraco 13 LTS         | 5.x        | Maintenance       |

## Upgrading

Back up your site and database before upgrading.

When upgrading, Articulate uses Umbraco's TipTap rich-text editor if a TinyMCE
editor UI is unavailable. To keep using TinyMCE, install
[TinyMCE.Umbraco](https://github.com/ProWorksCorporation/TinyMCE-Umbraco)
before starting the upgraded site for the first time.

Sites moving from Articulate 5 can upgrade in place or transfer posts through
BlogML. Review media paths during a BlogML migration because media files are
not moved automatically.

## Learn more

- [Installation and upgrading](https://github.com/Shazwazza/Articulate/blob/develop/docs/upgrading.md)
- [Configuration](https://github.com/Shazwazza/Articulate/blob/develop/docs/configuration.md)
- [Themes](https://github.com/Shazwazza/Articulate/blob/develop/docs/themes.md)
- [Importing](https://github.com/Shazwazza/Articulate/wiki/Importing)
- [Releases](https://github.com/Shazwazza/Articulate/releases)
- [Report an issue](https://github.com/Shazwazza/Articulate/issues)
- [Community discussions](https://forum.umbraco.com/tag/packages)

## Copyright and license

&copy; 2026 Shannon Deminick

Articulate is free software licensed under the
[MIT License](https://opensource.org/licenses/MIT).
