using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    public class DateFormattedUrlProvider : DefaultUrlProvider
    {
        public DateFormattedUrlProvider(
            IOptionsMonitor<RequestHandlerSettings> requestSettings,
            ILogger<DefaultUrlProvider> logger,
            ISiteDomainMapper siteDomainMapper,
            IUmbracoContextAccessor umbracoContextAccessor,
            UriUtility uriUtility,
            ILocalizationService localizationService)
            : base(requestSettings, logger, siteDomainMapper, umbracoContextAccessor, uriUtility, localizationService)
        {
        }

        public override UrlInfo GetUrl(IPublishedContent content, UrlMode mode, string culture, Uri current)
        {
            if (content != null && (content.ContentType.Alias == "ArticulateRichText" || content.ContentType.Alias == "ArticulateMarkdown") && content.Parent != null)
            {
                if (content.Parent.Parent != null)
                {
                    var useDateFormat = content.Parent.Parent.Value<bool>("useDateFormatForUrl");
                    if (!useDateFormat)
                        return null;
                }

                var date = content.Value<DateTime?>("publishedDate");
                if (date != null)
                {
                    var parentPath = base.GetUrl(content.Parent, mode, culture, current);
                    var urlFolder = string.Format("{0}/{1:d2}/{2:d2}", date.Value.Year, date.Value.Month, date.Value.Day);
                    var newUrl = parentPath.Text.EnsureEndsWith("/") + urlFolder + "/" + content.UrlSegment.EnsureEndsWith("/");

                    return UrlInfo.Url(newUrl, culture);
                }
            }

            return null;
        }
    }
}
