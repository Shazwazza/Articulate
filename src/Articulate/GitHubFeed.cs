using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core.Cache;

namespace Articulate
{
    public sealed class GitHubFeed
    {
        private readonly AppCaches _cache;
        private readonly string _url;
        private readonly int _maxResults;

        public GitHubFeed(AppCaches cache, string feedUrl, int maxResults = 10)
        {
            _cache = cache;
            _url = feedUrl;
            _maxResults = maxResults;
        }

        public string[] GetResult()
        {
            return (string[])_cache.RuntimeCache.Get(typeof(GitHubFeed).ToString(), () =>
            {
                using (var client = new HttpClient())
                {
                    var result = client.GetStringAsync(_url);
                    Task.WaitAll(result);
                    var xml = XDocument.Parse(result.Result);
                    var ns = XNamespace.Get("http://www.w3.org/2005/Atom");

                    return xml.Root.Descendants(ns + "content").Select(x => x.Value).Take(_maxResults).ToArray();
                }
            }, TimeSpan.FromHours(0.5));
        }
    }
}