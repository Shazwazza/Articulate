# Articulate.Theme.Sample

`Articulate.Theme.Sample` is a small Razor Class Library theme package for Articulate.

- Canonical theme key: `Sample`
- Views use modern Razor layout files with `_ViewStart.cshtml` and `_Layout.cshtml`
- Theme views live under `App_Plugins/Articulate/Themes/Sample/Views/`
- Theme assets live under `wwwroot/App_Plugins/Articulate/Themes/Sample/assets/`

The package registers itself with Articulate by contributing the `Sample` theme key through `IArticulateThemeDescriptorProvider`.

## View contract

Articulate controllers render these theme views directly:

- `List.cshtml`
- `Post.cshtml`
- `Author.cshtml`

Optional controller-rendered views:

- `Tags.cshtml`
- `Authors.cshtml`

This sample also uses normal Razor layout files (`_ViewStart.cshtml` and `_Layout.cshtml`) and theme-owned partials such as `Menu.cshtml`, `Pager.cshtml`, `PostCard.cshtml`, and `CommentsDisqus.cshtml`.

`CommentsDisqus.cshtml` renders only when post comments are enabled and Disqus or Giscus is configured. The filename is kept for compatibility with existing themes.

See the wiki for the full theme guidance:

- [Creating Themes](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme)
- [Theme File Structure](https://github.com/Shazwazza/Articulate/wiki/Theme-File-Structure)

For local development in this repository, the sample theme is referenced by `Articulate.Tests.Website`.
