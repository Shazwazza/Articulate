using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class ContentUrls
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public ContentUrls(IUmbracoContextAccessor umbracoContextAccessor)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        /// <summary>
        /// Returns the content item URLs taking into account any domains assigned
        /// </summary>
        /// <param name="publishedContent"></param>
        /// <returns></returns>
        internal HashSet<string> GetContentUrls(IPublishedContent publishedContent)
        {
            HashSet<string> allUrls;
            var other = _umbracoContextAccessor.UmbracoContext.UrlProvider.GetOtherUrls(publishedContent.Id).ToArray();
            if (other.Length > 0)
            {
                var urls = other.Where(x => x.IsUrl && string.IsNullOrEmpty(x.Text) == false).Select(x => x.Text);

                //this means there are domains assigned
                allUrls = new HashSet<string>(urls)
                {
                    _umbracoContextAccessor.UmbracoContext.UrlProvider.GetUrl(publishedContent.Id, UrlProviderMode.Absolute)
                };
            }
            else
            {
                allUrls = new HashSet<string>()
                {
                    publishedContent.Url
                };
            }
            return allUrls;
        }
    }
}