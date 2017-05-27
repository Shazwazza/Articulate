
using Umbraco.Core;
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
            // To be 100% sure of getting the right path in case of two blog roots or similar url structure in other parts
            // of the content tree we should probably retrieve all blog root nodes and match the url against them, but since
            // potentially one could put articulate nodes very deep inside the tree, it would be very expensive to look for them all.
            var segmentLength = contentRequest.Uri.Segments.Length;
            if (segmentLength > 4)
            {
                var newRoute = string.Empty;
                for (int i = 0; i < segmentLength; i++)
                {
                    if (i < segmentLength - 4 || i > segmentLength - 2)
                        newRoute += contentRequest.Uri.Segments[i];
                }
                var node = FindContent(contentRequest, newRoute);
                
                // If by chance something matches the format pattern I check again if there is sucn a node and if it's an articulate post
                if (node == null || (node.DocumentTypeAlias!= "ArticulateRichText" && node.DocumentTypeAlias != "ArticulateMarkdown")) return false;
                contentRequest.PublishedContent = node;
                return true;
            }

            return false;
        }
    }
}
