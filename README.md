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

Articulate version 4+ is only compatible with Umbraco 8.0+

## [Documentation](https://github.com/Shazwazza/Articulate/wiki)

Docs on installation, creating posts, customizing/creating themes, etc...

## [Discussions](https://our.umbraco.org/projects/starter-kits/articulate/discussions)

Please post to this Umbraco discussions forum to discuss Articulate, it's features and functionality. Do not post issues here, post them [here](https://github.com/Shazwazza/Articulate/issues) on GitHub

## [Issues](https://github.com/Shandem/Articulate/issues)

If you have any issues, please post them here on GitHub

## [Releases](https://github.com/Shazwazza/Articulate/releases)

See here for the list of releases and their release notes

## Contributing

If you would like to contribute to the Articulate project, you'll need some info on how to get started with the solution

1. Clone the repository
1. Open the /src/Articulate.sln file
1. Run a Nuget package restore (you can right click the solution in VS and choose Restore packages)
1. You will need to then run this in the Package Manager Console: `Update-Package UmbracoCms -reinstall` this is needed due to the way that Nuget works for Umbraco and will install all the client files
1. Build the solution
1. Ensure that Articualte.Web is set as the startup project
1. Start the Articulate.Web project
1. This will run the Umbraco installer, install as per normal
1. Open a powershell command line at the /build folder
1. Execute build.ps1
1. This will prompt you for a version, enter the latest Articulate version (at the time of writing this is "3.0.0")
1. It will prompt your for a pre-release value, just press Enter to skip this
1. Once the build has completed, it will have created the Articulate Umbraco package at /build/Release/Articulate.zip
1. Install this package in the Umbraco back office

Now you're all set! Any source changes you wish to make just do that in Visual Studio, build the solution when you need to and the changes will be reflected in the website.

### Changing Umbraco Articulate schema elements

What I mean by 'schema' elements here are things like Document Types, Property Types, Data Types, etc... 
If you wish to make changes to these, like adding a property to the Blog Post Document Type, you must make the changes in the xml file: `/buid/packagemanifest.xml`

### Updating to latest committed changes

If you are developing on this project and someone has made changes to any schema elements and you need to sync your environment to the latest changes, you can now use the Articulate dashboard installer and run the installer. This will essentially re-install the package business logic will will add any missing schema elements that are missing.

If you wish to re-force the package installation, you can remove the Articulate `<package>` element from the file  `/App_Data/packages/intalled/installedPackages.config` for the current Articulate vesion you are working with. Then you can run the dashboard installer and it will re-sync the data.

## Copyright & Licence

&copy; 2023 by Shannon Deminick

This is free software and is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT)
