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
- ObjectPool<StringBuilder> available transitively via ASP.NET Core (Microsoft.Extensions.ObjectPool)
- TemplateMatcher.TryMatch is thread-safe (only writes to caller-supplied RouteValueDictionary)
- Big refactor merged PR #520 (2026-07-02): Umbraco 18 lane, CPM, locked packable deps, net10.0 TFM
- RssFeedGenerator.GetFeedItem computes mediaRoot/rootUrlTrimmed per post — mediaRoot hoisted via Lazy<string> in PR #574; rootUri+GetLeftPart+rootUrlTrimmed still per-post (blocked on PR #573)
- AffectsArticulateRoutes is called per content-cache-refresh, iterates all Articulate root nodes
- GetDefaultIsoCodeAsync called in notification handlers; hoisted with ??= pattern in PR #570
- StringExtensions._newlineRegex migrated to [GeneratedRegex] in PR #571
- IHostingEnvironment.ApplicationVirtualPath is app-lifetime constant — safe to cache normalized forms
- ArticulateRouter.GetOrBuildRoutes: requestBaseUri now hoisted outside loop; Split('/').Length replaced with Count(c == '/') in PR #576

## Optimisation Backlog
| Priority | Area | Opportunity | Notes |
|---|---|---|---|
| MEDIUM | Data | `GetPagedPostsSortedByPublishedDate` loads ALL posts into memory before paging | Architectural (Umbraco IPublishedCache) |
| LOW | Code | `ContentExtensions.VariesByCulture` linear scan of `CompositionPropertyTypes` | Not a hot path |
| LOW | Code | `RssFeedGenerator.GetFeedItem` rootUri+GetLeftPart+rootUrlTrimmed per-post | Blocked until PR #573 merges |

## Completed Work
- 2026-05-25: PR #481 — static readonly SearchFields FrozenDictionary in DefaultArticulateSearcher (merged)
- 2026-05-29: PR #485 — cache publishedDate property type ID in GetContentByTag (merged)
- 2026-06-02: PR #486 — replace Regex.IsMatch with char-based check in IsDisqusEnabled (closed)
- 2026-06-03: PR #489 — pool StringBuilder in DefaultArticulateSearcher.Search() (closed)
- 2026-06-04: PR #491 — cache queryStrings.ToString() in PagingHelper.TryCreatePager (closed)
- 2026-06-07: PR #493 — hoist TrimEnd/EnsureStartsWith in RssFeedGenerator.GetFeedItem (closed)
- 2026-06-08: PR #495 — hoist EnsureEndsWith out of per-author loop and AdvertiseWeblogApi (closed)
- 2026-06-09: PR #497 — replace ParseExact+catch with TryParseExact in DateFormattedPostContentFinder (closed)
- 2026-06-12: PR #500 — cache TemplateMatcher in ArticulateRouteTemplate (closed)
- 2026-08-14: PR #566 (branch: efficiency/hoist-domain-uri-cast) — hoist DomainAndUri cast out of per-domain loop
- 2026-08-15: PR #568 (branch: efficiency/hoist-changedpath-concat) — hoist changedPath concat out of per-node loop
- 2026-08-17: PR #570 (branch: efficiency/hoist-getdefaultisocode) — hoist GetDefaultIsoCodeAsync out of per-entity loops
- 2026-08-18: PR #571 (branch: efficiency/generated-regex-newline) — migrate _newlineRegex to [GeneratedRegex]
- 2026-08-20: PR #573 (branch: efficiency/cache-apppath-rssfeed) — cache normalized ApplicationVirtualPath in RssFeedGenerator
- 2026-08-22: PR #575 (branch: efficiency/no-interpolation-in-search-query-builder) — replace interpolated strings with chained Append in search query builder
- 2026-08-23: PR #576 (branch: efficiency/hoist-request-uri-and-split) — hoist requestBaseUri + replace Split('/').Length with Count in ArticulateRouter

## Last Run
- 2026-08-23: Tasks 3, 7
- Monthly Activity: Updated August 2026 issue #567
