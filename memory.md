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

## Optimisation Backlog
| Priority | Area | Opportunity | Notes |
|---|---|---|---|
| HIGH | Code | ~~Static readonly `SearchFields` dictionary in `DefaultArticulateSearcher`~~ | PR merged 2026-05-27 |
| MEDIUM | Code | ~~`publishedDatePropertyTypeId` redundant DB call in `GetContentByTag`~~ | PR submitted 2026-05-29 |
| MEDIUM | Code | `StringBuilder` reuse in `DefaultArticulateSearcher.Search()` via pooling | Each call allocates a new StringBuilder |
| MEDIUM | Data | `GetPagedPostsSortedByPublishedDate` loads ALL posts into memory before paging | Architectural (Umbraco IPublishedCache) |
| LOW | Code | `ContentExtensions.VariesByCulture` linear scan of `CompositionPropertyTypes` | Not a hot path |

## Completed Work
- 2026-05-25: PR #481 — static readonly SearchFields dictionary in DefaultArticulateSearcher (merged 2026-05-27)
- 2026-05-29: PR — cache publishedDate property type ID in GetContentByTag (branch: efficiency/cache-property-type-id)

## Last Run
- 2026-05-29: Tasks 3, 7
