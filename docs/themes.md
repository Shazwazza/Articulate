# Themes

Articulate includes built-in themes and supports copied themes and reusable
theme packages.

For most sites, start with a built-in theme, copy it to a new name, and edit
the copy. Do not customize package-owned built-in files directly.

## Copied themes

Copied theme views belong under:

```text
Views/ArticulateThemes/{Theme}/Views/
```

Static assets belong under:

```text
wwwroot/App_Plugins/Articulate/Themes/{Theme}/assets/
```

Older themes with views directly under
`Views/ArticulateThemes/{Theme}/` still resolve for compatibility, but the
nested `Views` folder is preferred for new themes.

Common views include:

- `List.cshtml`
- `Post.cshtml`
- `Author.cshtml`
- optional `Tags.cshtml` and `Authors.cshtml`
- normal Razor layout files such as `_Layout.cshtml` and `_ViewStart.cshtml`
- theme-owned partials for navigation, paging, tags, search, or comments

## Package themes

A Razor Class Library can supply a reusable theme by registering
`IArticulateThemeDescriptorProvider`. The returned theme key must match the
theme folder name and the value stored by the theme picker.

Keep the key stable and distinctive: existing blogs store it. Built-in names
are reserved, and duplicate package keys are ignored.

See [`Articulate.Theme.Sample`](../src/Articulate.Theme.Sample) for a working
RCL theme, or the [creating a theme guide](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme).

## Disqus

Built-in themes render Disqus only when comments are enabled and the blog root
has a valid `disqusShortname`. Leave the value empty to disable Disqus without
rendering a placeholder panel or loading its script.

## Upgrading custom themes

Older themes may need:

- views and assets moved into their current separate roots
- old `Html` and `Url` helpers replaced with model extension methods
- async partial rendering through `Html.PartialAsync`
- URL-bearing CSS values passed through current safe CSS helpers
- an `Authors.cshtml` view if an authors directory should be displayed

## More detail

- [Themes overview](https://github.com/Shazwazza/Articulate/wiki/Themes)
- [Theme file structure](https://github.com/Shazwazza/Articulate/wiki/Theme-File-Structure)
- [Creating a theme](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme)
- [Installed themes](https://github.com/Shazwazza/Articulate/wiki/Installed-Themes)
