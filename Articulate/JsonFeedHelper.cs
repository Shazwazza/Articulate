using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core.Cache;

namespace Articulate
{
    public static class JsonFeedHelper
    {
        public static JArray GetResult(IRuntimeCacheProvider cache, string url)
        {
            return (JArray)cache.GetCacheItem(url, () =>
            {
                using (var client = new HttpClient())
                {
                    var result = client.GetStringAsync(url);
                    Task.WaitAll(result);
                    return JsonConvert.DeserializeObject<JArray>(result.Result);
                }
            });

        }
    }
}
