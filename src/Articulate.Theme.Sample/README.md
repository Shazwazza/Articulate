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

Native E2E and Docker test sites use the resulting package locally. It is not published to the general NuGet feed.

## View contract

Articulate controllers render these theme views directly:

- `List.cshtml`
- `Post.cshtml`
- `Author.cshtml`

Optional controller-rendered views:

- `Tags.cshtml`
- `Authors.cshtml`

This sample also uses normal Razor layout files (`_ViewStart.cshtml` and `_Layout.cshtml`) and theme-owned partials such as `Menu.cshtml`, `Pager.cshtml`, `PostCard.cshtml`, and `CommentsDisqus.cshtml`.

`CommentsDisqus.cshtml` renders when post comments are enabled and a provider is configured. Disqus renders when the Articulate root has a valid `disqusShortname` value. Otherwise Giscus renders when the four `giscus*` properties on the root are set, or when the `Articulate:Comments:Giscus` appsettings section is fully configured. When both are configured, Disqus takes precedence. With no provider configured the partial renders nothing.

See the wiki for the full theme guidance:

- [Creating Themes](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme)
- [Theme File Structure](https://github.com/Shazwazza/Articulate/wiki/Theme-File-Structure)

For local development in this repository, the sample theme is referenced by `Articulate.Tests.Website`.
