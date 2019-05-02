using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Examine;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate
{
    public class DefaultArticulateSearcher : IArticulateSearcher
    {
        private readonly UmbracoContext _umbracoContext;
        private readonly IExamineManager _examineManager;

        public DefaultArticulateSearcher(UmbracoContext umbracoContext, IExamineManager examineManager)
        {
            _umbracoContext = umbracoContext ?? throw new ArgumentNullException(nameof(umbracoContext));
            _examineManager = examineManager;
        }

        public IEnumerable<IPublishedContent> Search(string term, string indexName, int blogArchiveNodeId, int pageSize, int pageIndex, out long totalResults)
        {
            var splitSearch = term.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            //The fields to search on and their 'weight' (importance)
            var fields = new Dictionary<string, int>
            {
                {"markdown", 2},
                {"richText", 2},
                {"nodeName", 3},
                {"tags", 1},
                {"categories", 1},
                {"umbracoUrlName", 3}
            };

            //The multipliers for match types
            const int exactMatch = 5;
            const int termMatch = 2;

            var fieldQuery = new StringBuilder();
            //build field query
            foreach (var field in fields)
            {
                //full exact match (which has a higher boost)
                fieldQuery.Append($"{field.Key}:{"\"" + term + "\""}^{field.Value*exactMatch}");
                fieldQuery.Append(" ");
                //NOTE: Phrase match wildcard isn't really supported unless you use the Lucene
                // API like ComplexPhraseWildcardSomethingOrOther...
                //split match
                foreach (var s in splitSearch)
                {
                    //match on each term, no wildcard, higher boost
                    fieldQuery.Append($"{field.Key}:{s}^{field.Value*termMatch}");
                    fieldQuery.Append(" ");

                    //match on each term, with wildcard 
                    fieldQuery.Append($"{field.Key}:{s}*");
                    fieldQuery.Append(" ");
                }
            }

            indexName = indexName.IsNullOrWhiteSpace() ? Constants.UmbracoIndexes.ExternalIndexName : indexName;

            if (!_examineManager.TryGetIndex(indexName, out var index))
                throw new InvalidOperationException("No index found by name " + indexName);

            var searcher = index.GetSearcher();

            var criteria = searcher.CreateQuery()
                .Field("parentID", blogArchiveNodeId)
                .And()
                .NativeQuery($" +({fieldQuery})");

            var searchResult = criteria.Execute(
                //don't return more results than we need for the paging
                pageSize*(pageIndex + 1));

            var result = searchResult.ToPublishedSearchResults(_umbracoContext.PublishedSnapshot.Content);

            totalResults = searchResult.TotalItemCount;

            return result.Select(x => x.Content);
        }
    }
}
