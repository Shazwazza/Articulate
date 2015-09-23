using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Routing;

namespace Articulate
{
    /// <summary>
    /// Used to import comments to disqus via their REST api
    /// FIXME: This currently doens't work because Disqus haven't documented this correctly and the public key isn't the correct
    /// key to use apparently (according to many forum posts)
    /// </summary>
    internal class DisqusImporter
    {
        private readonly string _accessToken;
        private readonly string _publicKey;
        private readonly string _privateKey;


        public DisqusImporter(string publicKey/*, string privateKey, string accessToken*/)
        {
            //_accessToken = accessToken;
            //_privateKey = privateKey;
            _publicKey = publicKey;
        }

        public async Task<bool> Import(string postId, string comment, string user, string email, string userUrl, DateTime date)
        {
            var outgoingQueryString = HttpUtility.ParseQueryString(String.Empty);
            outgoingQueryString.Add("message", comment);
            outgoingQueryString.Add("thread", postId);
            outgoingQueryString.Add("author_email", email);
            outgoingQueryString.Add("author_name", user);
            outgoingQueryString.Add("author_url", userUrl);
            outgoingQueryString.Add("date", date.ToIsoString());
            outgoingQueryString.Add("state", "approved");
            var postdata = outgoingQueryString.ToString();

            var byteArray = Encoding.UTF8.GetBytes(postdata);

            //var request = (HttpWebRequest) WebRequest.Create(
            //    string.Format("https://disqus.com/api/3.0/posts/create.json?access_token={0}&api_key={1}&api_secret={2}",
            //        _accessToken, _publicKey, _privateKey));

            //TODO: This should work but everyone is having a problem with it, apparently the public key listed in apps isn't the one
            // that should be used here but they don't provide you with the correct one! :(
            var request = (HttpWebRequest)WebRequest.Create(
                string.Format("https://disqus.com/api/3.0/posts/create.json?api_key={0}", _publicKey));

            //var request = (HttpWebRequest)WebRequest.Create(
            //    string.Format("https://disqus.com/api/3.0/posts/create.json?api_secret={0}", _privateKey));

            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;
            
            //important! need referrer header for white list on disqus
            request.Referer = "http://articulate.dev/umbraco/umbracoapi/backoffice/ArticulateBlogImport/PostImportBlogMl";

            using (var dataStream = await request.GetRequestStreamAsync())
            {
                dataStream.Write(byteArray, 0, byteArray.Length);   
            }

            try
            {
                using (var response = await request.GetResponseAsync())
                {
                    Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                    var responseStream = response.GetResponseStream();
                    if (responseStream == null) return false;
                    using (var reader = new StreamReader(responseStream))
                    {
                        var stringResponse = reader.ReadToEnd();
                        var jsonResponse = JObject.Parse(stringResponse);
                        return jsonResponse["code"].Value<int>() == 0;
                    }
                }
            }
            catch (WebException ex)
            {
                var stream = ex.Response.GetResponseStream();
                if (stream != null)
                {
                    using (var resp = new StreamReader(stream))
                    {
                        var result = resp.ReadToEnd();

                        LogHelper.Error<BlogMlImporter>("Importing comment failed", new Exception(result));

                        var obj = (JObject)JsonConvert.DeserializeObject(result);
                        throw;
                    }
                }
                else
                {
                    throw;
                }
                
            }
            
        }
    }
}