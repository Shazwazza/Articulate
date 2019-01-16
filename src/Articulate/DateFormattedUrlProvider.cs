using System;
using Umbraco.Core.Configuration;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class DateFormattedUrlProvider : DefaultUrlProvider
    {
        public DateFormattedUrlProvider() : base(UmbracoConfig.For.UmbracoSettings().RequestHandler) { }

        public override string GetUrl(UmbracoContext umbracoContext, int id, Uri current, UrlProviderMode mode)
        {
            var content = umbracoContext.ContentCache.GetById(id);

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
                    var parentPath = base.GetUrl(umbracoContext, content.Parent.Id, current, mode);
                    var urlFolder = String.Format("{0}/{1:d2}/{2:d2}", date.Year, date.Month, date.Day);
                    var newUrl = parentPath + urlFolder + "/" + content.UrlName;
                    return newUrl;
                }
            }
            return null;
        }

    }
}
