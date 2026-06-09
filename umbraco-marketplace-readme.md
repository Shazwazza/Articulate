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

## Minimum requirements

- Umbraco 16.5.1+ (NET 9) and 17.4.0+ (NET 10) - Articulate version 6.x
- Umbraco 13 LTS (maintenance) - Articulate 5.x.

## Upgrade note for rich text editor compatibility

On Umbraco 16/17, Articulate will migrate the built-in `Umbraco.RichText` property editor to `Umb.PropertyEditorUi.TipTap` during package upgrade only if the TinyMCE editor UI is not registered. 

- You must have the [TinyMCE.Umbraco](https://github.com/ProWorksCorporation/TinyMCE-Umbraco) package installed before you start your site to keep using TinyMCE after upgrade.
- This setting affects upgrades only. Once the Articulate migration plan step has executed, Umbraco records it as complete.

## [Documentation](https://github.com/Shazwazza/Articulate/wiki)

Docs on installation, creating posts, customizing/creating themes, etc...

## [Discussions](https://forum.umbraco.com/tag/packages)

Please post to this Umbraco discussions forum to discuss Articulate, it's features and functionality. Do not post issues here, [post them here](https://github.com/Shazwazza/Articulate/issues) on GitHub

## [Issues](https://github.com/Shazwazza/Articulate/issues)

If you have any issues, please post them here on GitHub

## [Releases](https://github.com/Shazwazza/Articulate/releases)

See here for the list of releases and their release notes

## Development

For local setup and contributor notes, see [DEVELOP.md](https://github.com/Shazwazza/Articulate/blob/develop/DEVELOP.md).

### Changing Umbraco Articulate schema/data elements

If you need to make changes to the underlying Umbraco schema (doc types, data types, etc...) or the installed package's content/media, then you will need
to re-create the Articulate package in the back office with all required dependencies and then re-save the package.zip file and commit it to the repository.

## Copyright & License

&copy; 2026 by Shannon Deminick

This is free software and is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT)
