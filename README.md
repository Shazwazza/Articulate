# Articulate

[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](https://raw.githubusercontent.com/Shazwazza/Articulate/develop/assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

## Installation

Choose the package line that matches your Umbraco installation.

### Umbraco 17 and 18 (.NET 10)

- Umbraco 17.6.2 through 17.x: `dotnet add package Articulate --version 7.0.0-rc1`
- Umbraco 18.1.1 through 18.x: `dotnet add package Articulate --version 8.0.0-rc1`

These are pre-release packages. Use `7.0.0` or `8.0.0` after the stable release.

### Umbraco 13 (.NET 8, maintenance)

Articulate 5.x remains available for existing Umbraco 13 sites. Umbraco 13 security support ended in December 2025 and reaches end of life in December 2026. See the [Umbraco lifecycle page](https://umbraco.com/products/knowledge-center/long-term-support-and-end-of-life/) for the current platform dates.

### Articulate 6.x (deprecated)

Articulate 6.x supports Umbraco 16.5.1–16.x on .NET 9 and Umbraco 17.4.0–17.x on .NET 10. Articulate 7.0.0 supersedes it for Umbraco 17.6.2+; current feature work is on Articulate 7.x and 8.x.

## Features

- Categories and tags
- Themes and custom theme packages
- Multiple archives and author profiles
- Live Writer and MetaWeblog support (disabled by default; enable `Articulate:EnableMetaWeblog` when required)
- Markdown and rich-text posts
- Disqus comments
- Search and customizable URLs
- BlogML import/export and Disqus export
- Customizable RSS feeds
- Mobile publishing with image support

Legacy MetaWeblog and Open Live Writer support is disabled by default because it uses legacy username/password authentication. Set `Articulate:EnableMetaWeblog` to `true` and restart the application when you need it.

## Documentation

The [Articulate wiki](https://github.com/Shazwazza/Articulate/wiki) covers:

- [Installation](https://github.com/Shazwazza/Articulate/wiki/Installation)
- [Configuration](https://github.com/Shazwazza/Articulate/wiki/Configuration)
- [Creating blog posts](https://github.com/Shazwazza/Articulate/wiki/Creating-a-blog-post)
- [Themes](https://github.com/Shazwazza/Articulate/wiki/Themes)
- [Markdown editor authentication](https://github.com/Shazwazza/Articulate/wiki/Markdown-Editor-Authentication)
- [Importing](https://github.com/Shazwazza/Articulate/wiki/Importing)
- [Upgrading](https://github.com/Shazwazza/Articulate/wiki/Upgrading)

## Issues and discussions

- [Report an issue](https://github.com/Shazwazza/Articulate/issues)
- [Community discussions](https://forum.umbraco.com/tag/packages)
- [Releases](https://github.com/Shazwazza/Articulate/releases)

## Development

The package includes the backoffice extension and static assets. Local development, source-build, Docker, and CI guidance lives in [DEVELOP.md](DEVELOP.md) and [BUILD.md](BUILD.md).

## Copyright and licence

&copy; 2026 Shannon Deminick

This is free software licensed under the [MIT License](http://opensource.org/licenses/MIT).
