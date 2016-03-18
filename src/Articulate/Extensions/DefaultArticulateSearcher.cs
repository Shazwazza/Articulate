using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Examine;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate.Extensions
{
    public class DefaultArticulateSearcher : IArticulateSearcher
    {
        private readonly UmbracoHelper _umbracoHelper;

        public DefaultArticulateSearcher(UmbracoHelper umbracoHelper)
        {
            if (umbracoHelper == null) throw new ArgumentNullException(nameof(umbracoHelper));
            _umbracoHelper = umbracoHelper;
        }

        public IEnumerable<IPublishedContent> Search(string term, string provider, int blogArchiveNodeId)
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
                fieldQuery.Append(string.Format("{0}:{1}^{2}", field.Key, "\"" + term + "\"", field.Value * exactMatch));
                fieldQuery.Append(" ");
                //NOTE: Phrase match wildcard isn't really supported unless you use the Lucene
                // API like ComplexPhraseWildcardSomethingOrOther...
                //split match
                foreach (var s in splitSearch)
                {
                    //match on each term, no wildcard, higher boost
                    fieldQuery.Append(string.Format("{0}:{1}^{2}", field.Key, s, field.Value * termMatch));
                    fieldQuery.Append(" ");

                    //match on each term, with wildcard 
                    fieldQuery.Append(string.Format("{0}:{1}*", field.Key, s));
                    fieldQuery.Append(" ");
                }
            }

            var criteria = provider == null
                ? ExamineManager.Instance.CreateSearchCriteria()
                : ExamineManager.Instance.SearchProviderCollection[provider].CreateSearchCriteria();

            criteria.RawQuery(string.Format("+parentID:{0} +({1})", blogArchiveNodeId, fieldQuery));

            var searchProvider = provider == null
                ? ExamineManager.Instance.DefaultSearchProvider
                : ExamineManager.Instance.SearchProviderCollection[provider];

            return _umbracoHelper.TypedSearch(criteria, searchProvider).ToArray();
        }
    }
}
