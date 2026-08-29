# Articulate.Theme.Sample

`Articulate.Theme.Sample` is a small Razor Class Library theme package for Articulate.

- Canonical theme key: `Sample`
- Views use modern Razor layout files with `_ViewStart.cshtml` and `_Layout.cshtml`
- Theme views live under `App_Plugins/Articulate/Themes/Sample/Views/`
- Theme assets live under `wwwroot/App_Plugins/Articulate/Themes/Sample/assets/`

The package registers itself with Articulate by contributing the `Sample` theme key through `IArticulateThemeDescriptorProvider`.

## Local use

Build the sample theme for local validation with either package lane:

```sh
dotnet run --file build/build.cs -- build --lane v17 --sample
dotnet run --file build/build.cs -- build --lane v18 --sample
```

The Docker test site consumes the resulting package locally. This package is not
published to the general NuGet feed.

## View contract

Articulate controllers render these theme views directly:

- `List.cshtml`
- `Post.cshtml`
- `Author.cshtml`

Optional controller-rendered views:

- `Tags.cshtml`
- `Authors.cshtml`

This sample also uses normal Razor layout files (`_ViewStart.cshtml` and `_Layout.cshtml`) and theme-owned partials such as `Menu.cshtml`, `Pager.cshtml`, `PostCard.cshtml`, and `CommentsDisqus.cshtml`.

`CommentsDisqus.cshtml` renders only when post comments are enabled and the Articulate root has a valid `disqusShortname` value. Leave `disqusShortname` empty to disable Disqus without showing a placeholder panel.

See the wiki for the full theme guidance:

- [Creating Themes](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme)
- [Theme File Structure](https://github.com/Shazwazza/Articulate/wiki/Theme-File-Structure)

For local development in this repository, the sample theme is referenced by `Articulate.Tests.Website`.
