#nullable enable
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
