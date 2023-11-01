[![Articulate Build](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml/badge.svg)](https://github.com/Shazwazza/Articulate/actions/workflows/build.yml)

![Articulate](assets/Logo.png?raw=true)

> A wonderful Blog engine built on Umbraco

---
_❤️ If you use and like Articulate please consider [becoming a GitHub Sponsor](https://github.com/sponsors/Shazwazza/) ❤️_

## Features

Supporting all the features you'd want in a blogging platform

* Categories & Tags
* Themes
* Multiple archives
* Live Writer support
* Markdown support
* Post from your mobile phone including photos direct from you camera
* Disqus or Facebook comment support (or build your own)
* Search
* Blogml import/export (including Disqus import)
* Customizable RSS feeds
* Customizable urls 
* Author profiles

## Minimum requirements

Articulate version 5+ is only compatible with Umbraco 10.1.0+

## [Documentation](https://github.com/Shazwazza/Articulate/wiki)

Docs on installation, creating posts, customizing/creating themes, etc...

## [Discussions](https://our.umbraco.org/projects/starter-kits/articulate/discussions)

Please post to this Umbraco discussions forum to discuss Articulate, it's features and functionality. Do not post issues here, post them [here](https://github.com/Shazwazza/Articulate/issues) on GitHub

## [Issues](https://github.com/Shandem/Articulate/issues)

If you have any issues, please post them here on GitHub

## [Releases](https://github.com/Shazwazza/Articulate/releases)

See here for the list of releases and their release notes

## Contributing

1. Clone/fork the repository
1. Open the /src/Articulate.sln file
1. Build the solution (will also performa Nuget restore)
1. Ensure that Articulate.Web is set as the startup project
1. Start the Articulate.Web project
1. This will run the Umbraco installer, install as per normal
1. The Articulate package migrations will also execute and install all of the Articulate schema and content items

Now you're all set! Any source changes you wish to make just do that in Visual Studio, build the solution when you need to and the changes will be reflected in the website.

### Changing Umbraco Articulate schema/data elements

If you need to make changes to the underlying Umbraco schema (doc types, data types, etc...) or the installed package's content/media, then you will need
to re-create the Articulate package in the back office with all required dependencies and then re-save the package.zip file and commit it to the repository.

### Updating to latest committed changes

## Copyright & Licence

&copy; 2023 by Shannon Deminick

This is free software and is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT)
