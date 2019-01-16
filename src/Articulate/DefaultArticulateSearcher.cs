using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Examine;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate
{
    public class DefaultArticulateSearcher : IArticulateSearcher
    {
        private readonly UmbracoHelper _umbracoHelper;

        public DefaultArticulateSearcher(UmbracoHelper umbracoHelper)
        {
            if (umbracoHelper == null) throw new ArgumentNullException(nameof(umbracoHelper));
            _umbracoHelper = umbracoHelper;
        }

        public IEnumerable<IPublishedContent> Search(string term, string provider, int blogArchiveNodeId, int pageSize, int pageIndex, out int totalResults)
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

            var criteria = provider == null
                ? ExamineManager.Instance.CreateSearchCriteria()
                : ExamineManager.Instance.SearchProviderCollection[provider].CreateSearchCriteria();

            criteria.RawQuery($"+parentID:{blogArchiveNodeId} +({fieldQuery})");

            var searchProvider = provider == null
                ? ExamineManager.Instance.DefaultSearchProvider
                : ExamineManager.Instance.SearchProviderCollection[provider];

            var searchResult = searchProvider.Search(criteria, 
                //don't return more results than we need for the paging
                pageSize*(pageIndex + 1));

            //TODO: Wait until Umbraco 7.5.7 is out so this is public, for now we'll use reflection

            var examineExtensionsType = typeof(UmbracoContext).Assembly.GetType("Umbraco.Web.ExamineExtensions");
            var result = (IEnumerable<IPublishedContent>)examineExtensionsType.CallStaticMethod(
                "ConvertSearchResultToPublishedContent", 
                searchResult.Skip(pageIndex*pageSize),
                _umbracoHelper.UmbracoContext.ContentCache);

            totalResults = searchResult.TotalItemCount;

            return result;
        }
    }
}
