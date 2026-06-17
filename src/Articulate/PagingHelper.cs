#nullable enable
using System.Net;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Articulate
{
    internal static class PagingHelper
    {
        internal static int NormalizePageNumber(int? page)
            => page is > 0 ? page.Value : 1;

        internal static int NormalizePageSize(int pageSize)
            => pageSize > 0 ? pageSize : 10;

        internal static int CalculateTotalPages(long totalPosts, int pageSize)
            => totalPosts == 0 ? 1 : Convert.ToInt32(Math.Ceiling((double)totalPosts / pageSize));

        internal static bool TryCreatePager(
            string? baseUrl,
            IEnumerable<KeyValuePair<string, StringValues>> query,
            int pageSize,
            long totalPosts,
            int? page,
            out PagerModel? pager)
        {
            int pageNumber = NormalizePageNumber(page);
            int normalizedPageSize = NormalizePageSize(pageSize);
            int totalPages = CalculateTotalPages(totalPosts, normalizedPageSize);

            if (totalPages < pageNumber)
            {
                pager = null;
                return false;
            }

            var queryStrings = new StringBuilder();
            foreach ((var key, StringValues val) in query)
            {
                if (key == "p")
                {
                    continue;
                }

                foreach (string? v in val)
                {
                    queryStrings.Append($"&{WebUtility.UrlEncode(key)}={WebUtility.UrlEncode(v)}");
                }
            }

            string queryString = queryStrings.ToString();

            pager = new PagerModel(
                normalizedPageSize,
                pageNumber - 1,
                totalPages,
                totalPages > pageNumber
                    ? GetPagedUrl(baseUrl, pageNumber + 1, queryString)
                    : string.Empty,
                pageNumber > 2 ? GetPagedUrl(baseUrl, pageNumber - 1, queryString) :
                pageNumber > 1 ? GetPagedUrl(baseUrl, null, queryString) : string.Empty);

            return true;
        }

        private static string GetPagedUrl(string? baseUrl, int? page, string queryStrings)
            => page.HasValue
                ? $"{baseUrl?.EnsureEndsWith('?')}p={page}{queryStrings}"
                : $"{baseUrl?.EnsureEndsWith('?')}{queryStrings.TrimStart('&')}";
    }
}
