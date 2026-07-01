# Themes

Articulate includes built-in themes and supports copied themes and reusable
theme packages.

For most sites, start with a built-in theme, copy it to a new name, and edit
the copy. Do not customize package-owned built-in files directly.

## Built-in themes

Built-in themes are package-owned. Use them as a reference when copying or
extending a theme, but do not edit the package-owned files in an installed site.

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

See the sample theme project in this repository for a working RCL theme, or the
[creating a theme guide](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme).

## Comments

Built-in themes render comments only when post comments are enabled and either
Disqus or Giscus is configured. The existing `CommentsDisqus.cshtml` partial name
is kept for compatibility, but it now handles both providers.

Custom themes that override `CommentsDisqus.cshtml` can keep the same filename
and branch on `Model.CommentsProvider`. For user-facing setup and provider
precedence, see [Comments](https://github.com/Shazwazza/Articulate/wiki/Comments).

### Giscus palette per theme

Each shipped theme includes a `giscus.css` in its `assets/` folder. Leave
`DataTheme` empty and Articulate uses the active theme's file through
`/articulate/giscus-theme/{theme}`. The same convention works for:

1. Built-in themes (Articulate.Web).
2. Copied or forked themes (`wwwroot/App_Plugins/Articulate/Themes/{theme}/assets/`).
3. Package (RCL) themes (the package's `wwwroot/` served via the
   static-web-assets manifest).

Themes without a `giscus.css` fall back to the built-in Giscus palette.

## Upgrading custom themes

Older themes may need:

- views and assets moved into their current separate roots
- old `Html` and `Url` helpers replaced with model extension methods
- async partial rendering through `Html.PartialAsync`
- URL-bearing CSS values passed through current safe CSS helpers
- `CommentsDisqus.cshtml` updated if the theme should customize Giscus markup
- an `Authors.cshtml` view if an authors directory should be displayed

## More detail

- [Themes overview](https://github.com/Shazwazza/Articulate/wiki/Themes)
- [Theme file structure](https://github.com/Shazwazza/Articulate/wiki/Theme-File-Structure)
- [Creating a theme](https://github.com/Shazwazza/Articulate/wiki/Creating-a-theme)
- [Installed themes](https://github.com/Shazwazza/Articulate/wiki/Installed-Themes)
