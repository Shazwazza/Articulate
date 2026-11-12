#nullable enable
#if UMBRACO_19_OR_GREATER
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying.Filtering;

namespace Articulate
{
    /// <summary>
    /// Default implementation of <see cref="IArticulateSearcher"/>, using the CMS published-content search.
    /// </summary>
    public class DefaultArticulateSearcher(
        ISearcherResolver searcherResolver,
        IPublishedContentCache publishedContentCache,
        IVariationContextAccessor variationContextAccessor)
        : IArticulateSearcher
    {
        /// <inheritdoc/>
        public async Task<(IEnumerable<IPublishedContent> Results, long TotalResults)> SearchAsync(
            string term,
            int blogArchiveNodeId,
            int pageSize,
            int pageIndex)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return ([], 0);
            }

            // DoS protection: limit query complexity
            const int maxTermLength = 200;
            const int maxTokenCount = 10;
            IEnumerable<string> searchTerms = term[..Math.Min(term.Length, maxTermLength)]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(maxTokenCount);

            const string indexAlias = Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
            ISearcher? searcher = searcherResolver.GetSearcher(indexAlias);
            Guid? archiveKey = publishedContentCache.GetById(blogArchiveNodeId)?.Key;
            if (searcher is null || archiveKey is null)
            {
                return ([], 0);
            }

            var culture = variationContextAccessor.VariationContext?.Culture;

            // note: CMS all-term matching and default field relevance replace the legacy weighted Lucene query; add an IContentIndexer to tier post fields if ranking needs tuning.
            Umbraco.Cms.Core.Search.Querying.SearchResult result = await searcher.SearchAsync(
                indexAlias,
                query: string.Join(' ', searchTerms),
                filters: [new KeywordFilter(Umbraco.Cms.Core.Constants.Search.FieldNames.ParentId, [archiveKey.Value.ToString("D")], Negate: false)],
                culture: string.IsNullOrWhiteSpace(culture) ? null : culture,
                skip: pageIndex * pageSize,
                take: pageSize);

            return (result.Documents
                .Select(document => publishedContentCache.GetById(document.Id))
                .OfType<IPublishedContent>(), result.Total);
        }
    }
}
#else
using System.Collections.Frozen;
using System.Text;
using Examine;
using Examine.Search;
using Lucene.Net.QueryParsers.Classic;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Articulate
{
    /// <summary>
    /// Default implementation of <see cref="IArticulateSearcher"/>.
    /// </summary>
    public class DefaultArticulateSearcher(
        IUmbracoContextAccessor umbracoContextAccessor,
        IExamineManager examineManager)
        : IArticulateSearcher
    {
        [ThreadStatic]
        private static StringBuilder? _sSharedStringBuilder;

        // Static to avoid allocating a new Dictionary on every search call.
        private static readonly FrozenDictionary<string, int> SearchFields = new Dictionary<string, int>
        {
            { "markdown", 2 },
            { "richText", 2 },
            { "nodeName", 3 },
            { "tags", 1 },
            { "categories", 1 },
            { "umbracoUrlName", 3 },
        }.ToFrozenDictionary();

        /// <inheritdoc/>
        public IEnumerable<IPublishedContent> Search(
            string term,
            string? indexName,
            int blogArchiveNodeId,
            int pageSize,
            int pageIndex,
            out long totalResults)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                totalResults = 0;
                return [];
            }

            // DoS protection: limit query complexity
            const int maxTermLength = 200;
            const int maxTokenCount = 10;

            if (term.Length > maxTermLength)
            {
                term = term[..maxTermLength];
            }

            var splitSearch = term.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            if (splitSearch.Length > maxTokenCount)
            {
                splitSearch = splitSearch[..maxTokenCount];
            }

            var escapedTerm = QueryParserBase.Escape(term);

            // The fields to search on and their 'weight' (importance)
            FrozenDictionary<string, int> fields = SearchFields;

            // The multipliers for match types
            const int exactMatch = 5;
            const int termMatch = 2;

            _sSharedStringBuilder ??= new StringBuilder();
            StringBuilder fieldQuery = _sSharedStringBuilder;
            fieldQuery.Clear();

            // build field query
            foreach (KeyValuePair<string, int> field in fields)
            {
                // full exact match (which has a higher boost)
                _ = fieldQuery.Append($"{field.Key}:\"{escapedTerm}\"^{field.Value * exactMatch}");
                _ = fieldQuery.Append(' ');

                // NOTE: Phrase match wildcard isn't really supported unless you use the Lucene
                // API like ComplexPhraseWildcardSomethingOrOther...
                // split match
                foreach (var s in splitSearch)
                {
                    var escapedSplitTerm = QueryParserBase.Escape(s);

                    // match on each term, no wildcard, higher boost
                    _ = fieldQuery.Append($"{field.Key}:{escapedSplitTerm}^{field.Value * termMatch}");
                    _ = fieldQuery.Append(' ');

                    // match on each term, with wildcard
                    _ = fieldQuery.Append($"{field.Key}:{escapedSplitTerm}*");
                    _ = fieldQuery.Append(' ');
                }
            }

            indexName = string.IsNullOrWhiteSpace(indexName)
                ? Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName
                : indexName;

            if (!examineManager.TryGetIndex(indexName, out IIndex? index) || index is null)
            {
                // Unknown index - return empty results rather than 500
                totalResults = 0;
                return [];
            }

            ISearcher searcher = index.Searcher;

            IBooleanOperation criteria = searcher.CreateQuery()
                .Field("parentID", blogArchiveNodeId)
                .And()
                .NativeQuery($" +({fieldQuery})");

            ISearchResults searchResult = criteria.Execute(QueryOptions.SkipTake(pageIndex * pageSize, pageSize));

            IEnumerable<PublishedSearchResult> result = searchResult
                .ToPublishedSearchResults(umbracoContextAccessor.GetRequiredUmbracoContext().Content);
            totalResults = searchResult.TotalItemCount;

            return result.Select(x => x.Content);
        }
    }
}
#endif
