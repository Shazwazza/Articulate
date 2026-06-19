# Upgrading Articulate

Back up the site, database, and media before upgrading. Match the Articulate
package line to the target Umbraco version:

| Umbraco                | Articulate |
|------------------------|------------|
| Umbraco 18             | 7.x        |
| Umbraco 17.4+          | 6.1.x      |
| Umbraco 16.5+ or 17.4+ | 6.0.x      |
| Umbraco 13 LTS         | 5.x        |

## Rich-text editor migration

During upgrade, Articulate migrates its built-in rich-text data type to
Umbraco's TipTap editor when a TinyMCE editor UI is unavailable.

To keep TinyMCE, install
[TinyMCE.Umbraco](https://github.com/ProWorksCorporation/TinyMCE-Umbraco)
before starting the upgraded site for the first time. Once Umbraco records the
migration as complete, it is not run again.

## Moving from Articulate 5

You can upgrade in place or transfer posts through BlogML.

- Configuration moves to `appsettings.json`.
- The standalone Markdown editor requires OpenIddict client configuration.
- Custom themes may require new paths and helper APIs.
- Media in `media/articulate` is not moved by BlogML.
- `postImage` can often be mapped from an attachment or embedded image.
- Other inline media may need to be copied and its URLs updated separately.

On Umbraco 13, older installations may also require pending package migrations
to be run and the `Articulate Image Picker` data type to be saved once to
repair bundled demo-media references.

## Recommended order

1. Upgrade Umbraco to a version supported by the target Articulate package.
2. Back up and install the matching Articulate package.
3. Configure `appsettings.json` and `UmbracoApplicationUrl`.
4. Install TinyMCE.Umbraco first if TinyMCE must be preserved.
5. Start the site and complete any migration prompts.
6. Verify editing, publishing, routing, themes, and media.
7. Test BlogML import/export and the standalone Markdown editor if used.

## More detail

- [Migration from v5 to v6](https://github.com/Shazwazza/Articulate/wiki/Migration-from-v5-to-v6)
- [Installation](https://github.com/Shazwazza/Articulate/wiki/Installation)
- [Configuration](configuration.md)
- [Themes](themes.md)
