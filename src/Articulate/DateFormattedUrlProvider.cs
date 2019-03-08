using System;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class DateFormattedUrlProvider : DefaultUrlProvider
    {
        public DateFormattedUrlProvider(IRequestHandlerSection requestSettings, ILogger logger, IGlobalSettings globalSettings, ISiteDomainHelper siteDomainHelper) 
            : base(requestSettings, logger, globalSettings, siteDomainHelper)
        {
        }

        public override UrlInfo GetUrl(UmbracoContext umbracoContext, IPublishedContent content, UrlProviderMode mode, string culture, Uri current)
        {
            if (content != null && (content.ContentType.Alias == "ArticulateRichText" || content.ContentType.Alias == "ArticulateMarkdown") && content.Parent != null)
            {
                if (content.Parent.Parent != null)
                {
                    var useDateFormat = content.Parent.Parent.Value<bool>("useDateFormatForUrl");
                    if (!useDateFormat)
                        return null;
                }

                var date = content.Value<DateTime>("publishedDate");
                if (date != null)
                {
                    var parentPath = base.GetUrl(umbracoContext, content, mode, culture, current);
                    var urlFolder = string.Format("{0}/{1:d2}/{2:d2}", date.Year, date.Month, date.Day);
                    var newUrl = parentPath.EnsureEndsWith("/") + urlFolder + "/" + content.UrlName;

                    return UrlInfo.Url(newUrl, culture);
                }
            }

            return null;
        }
    }
}
