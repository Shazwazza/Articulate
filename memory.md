# Efficiency Improver Memory — Shazwazza/Articulate

## Build/Test Commands (validated: build fails in sandbox due to Nerdbank.GitVersioning needing full git history with fetch-depth:0)
- Build: `./build/build.sh` (requires fetch-depth:0 git clone + nbgv tool)
- Test: `RUN_TESTS=true ./build/build.sh`
- Quick build (CI): runs via `.github/workflows/build.yml`

## Efficiency Notes
- Umbraco IPublishedCache is in-memory, so in-memory pagination of posts is by design
- TagRepository uses 30-second runtime cache (in Release mode) for tag queries
- Build requires Nerdbank.GitVersioning; can't fully validate locally without full git history
- publishedDate propertytype ID is schema-level (app-lifetime constant), safe to cache for 1h
- MetaWeblog provider already uses [GeneratedRegex] source-generated regexes (good pattern)

## Optimisation Backlog
| Priority | Area | Opportunity | Notes |
|---|---|---|---|
| MEDIUM | Code | `StringBuilder` reuse in `DefaultArticulateSearcher.Search()` via pooling | Each call allocates a new StringBuilder |
| MEDIUM | Data | `GetPagedPostsSortedByPublishedDate` loads ALL posts into memory before paging | Architectural (Umbraco IPublishedCache) |
| LOW | Code | `ContentExtensions.VariesByCulture` linear scan of `CompositionPropertyTypes` | Not a hot path |

## Completed Work
- 2026-05-25: PR #481 — static readonly SearchFields FrozenDictionary in DefaultArticulateSearcher (merged 2026-05-27)
- 2026-05-29: PR #485 — cache publishedDate property type ID in GetContentByTag (merged 2026-06-01)
- 2026-06-02: PR — replace Regex.IsMatch with char-based check in IsDisqusEnabled (branch: efficiency/disqus-regex-removal)

## Last Run
- 2026-06-02: Tasks 3, 7
- Monthly Activity: May issue (#482) closed; June issue created
