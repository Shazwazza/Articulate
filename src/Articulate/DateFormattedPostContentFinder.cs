
using System;
using System.Globalization;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    class DateFormattedPostContentFinder : ContentFinderByNiceUrl
    {
        public override bool TryFindContent(PublishedContentRequest contentRequest)
        {
            string route;
            if (contentRequest.HasDomain)
                route = contentRequest.Domain.RootNodeId.ToString() + DomainHelper.PathRelativeToDomain(contentRequest.DomainUri, contentRequest.Uri.GetAbsolutePathDecoded());
            else
                route = contentRequest.Uri.GetAbsolutePathDecoded();

            // This simple logic should do the trick: basically if I find an url with more than 4 segments (the 3 date parts and the slug)
            // I leave the last segment (the slug), remove the 3 date parts, and keep all the rest.
            var segmentLength = contentRequest.Uri.Segments.Length;
            if (segmentLength > 4)
            {
                var stringDate = contentRequest.Uri.Segments[segmentLength - 4]+ contentRequest.Uri.Segments[segmentLength - 3]+ contentRequest.Uri.Segments[segmentLength - 2].TrimEnd("/");
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
                for (int i = 0; i < segmentLength; i++)
                {
                    if (i < segmentLength - 4 || i > segmentLength - 2)
                        newRoute += contentRequest.Uri.Segments[i];
                }
                var node = FindContent(contentRequest, newRoute);
                contentRequest.PublishedContent = null;
                // If by chance something matches the format pattern I check again if there is sucn a node and if it's an articulate post
                if (node == null || (node.DocumentTypeAlias!= "ArticulateRichText" && node.DocumentTypeAlias != "ArticulateMarkdown")) return false;
                if (!node.Parent.Parent.GetPropertyValue<bool>("useDateFormatForUrl")) return false;
                if (node.GetPropertyValue<DateTime>("publishedDate").Date!= postDate.Date) return false;
                
                contentRequest.PublishedContent = node;
                return true;
            }

            return false;
        }
    }
}
