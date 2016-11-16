![Articulate](assets/Logo.png?raw=true)

==========

> Blogging built on Umbraco

## Features

Supporting all the features you'd want in a blogging platform

* Categories & Tags
* Themes
* Live Writer support
* Markdown support
* Easily post from your mobile phone including adding images and photos direct from you camera
* Disqus, Google+ or Facebook comment support
* Search
* Blogml import/export
 * During import we can also export a compatible XML so you can easily import your comments to Disqus
* RSS
 * Built in or custom feed
 * Feeds per category/tag
* Customizable urls 
* Author profiles

## Minimum requirements

Articulate version 2+ is only compatible with Umbraco 7.3+

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
2. Open the /src/Articulate.sln file
3. Build the solution - this will run a Nuget package restore
4. Ensure that Articualte.Web is set as the startup project
5. Start the Articulate.Web project
6. This will run the Umbraco installer, install as per normal
7. Open a powershell command line at the /build folder
8. Execute build.ps1
9. This will prompt you for a version, enter the latest Articulate version, currently this is "2.0.5"
10. It will prompt your for a pre-release value, just press Enter to skip this
11. Once the build has completed, it will have created the Articulate Umbraco package at /build/Releases/2.0.5/Articulate.zip
12. Install this package in the Umbraco back office

Now you're all set! Any source changes you wish to make just do that in Visual Studio, build the solution when you need to and the changes will be reflected in the website.

## Copyright & Licence

&copy; 2016 by Shannon Deminick

This is free software and is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT)
