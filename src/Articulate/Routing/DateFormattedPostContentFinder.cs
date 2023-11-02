using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    public class DateFormattedPostContentFinder : ContentFinderByUrl
    {
        public DateFormattedPostContentFinder(ILogger<ContentFinderByUrl> logger, IUmbracoContextAccessor umbracoContextAccessor) : base(logger, umbracoContextAccessor)
        {
        }

        public override async Task<bool> TryFindContent(IPublishedRequestBuilder contentRequest)
        {
            await Task.CompletedTask;

            // This simple logic should do the trick: basically if I find an url with more than 4 segments (the 3 date parts and the slug)
            // I leave the last segment (the slug), remove the 3 date parts, and keep all the rest.
            var segmentLength = contentRequest.Uri.Segments.Length;
            if (segmentLength <= 4)
            {
                return false;
            }

            var stringDate = contentRequest.Uri.Segments[segmentLength - 4] + contentRequest.Uri.Segments[segmentLength - 3] + contentRequest.Uri.Segments[segmentLength - 2].TrimEnd('/');
            DateTime postDate;
            try
            {
                postDate = DateTime.ParseExact(stringDate, "yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return false;
            }

            var newRoute = string.Empty;
            for (var i = 0; i < segmentLength; i++)
            {
                if (i < segmentLength - 4 || i > segmentLength - 2)
                {
                    newRoute += contentRequest.Uri.Segments[i].ToLowerInvariant();
                }
            }

            // if there's a domain attached we need to lookup the content with the domain Id
            // and the domain's path stripped from the start
            if (contentRequest.HasDomain())
            {
                newRoute = contentRequest.Domain.ContentId + DomainUtilities.PathRelativeToDomain(contentRequest.Domain.Uri, newRoute);
            }

            var node = FindContent(contentRequest, newRoute);

            // If by chance something matches the format pattern I check again if there is sucn a node and if it's an articulate post
            if (node == null || (node.ContentType.Alias != "ArticulateRichText" && node.ContentType.Alias != "ArticulateMarkdown"))
            {
                return false;
            }

            if (!node.Parent.Parent.Value<bool>("useDateFormatForUrl"))
            {
                return false;
            }

            if (node.Value<DateTime>("publishedDate").Date != postDate.Date)
            {
                return false;
            }

            contentRequest.SetPublishedContent(node);
            return true;

        }
    }
}
