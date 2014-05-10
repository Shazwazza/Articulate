using System;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web.Routing;

namespace Articulate
{
    /// <summary>
    /// Used to route custom articulate routes such as "/tags" "/tags/mytag" and "/search"
    /// </summary>
    public class ArticulateContentFinder : ContentFinderByNiceUrl
    {
        public override bool TryFindContent(PublishedContentRequest contentRequest)
        {
            var route = !contentRequest.HasDomain ? contentRequest.Uri.GetAbsolutePathDecoded() : contentRequest.Domain.RootNodeId.ToString() + DomainHelper.PathRelativeToDomain(contentRequest.DomainUri, contentRequest.Uri.GetAbsolutePathDecoded());

            var parts = route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                //if it ends with '/search'
                if (parts[parts.Length - 1].InvariantEquals("search"))
                {
                    route = route.Substring(0, route.LastIndexOf("/search", StringComparison.InvariantCultureIgnoreCase) + 1);
                    var parent = FindContent(contentRequest, route);
                    if (parent != null && parent.DocumentTypeAlias.InvariantEquals("Articulate"))
                    {
                        //ok, so we want to render the tag list page, we don't set  the template name so the
                        // Index MVC action will execute.
                        contentRequest.PublishedContent = new ArticulateVirtualPage(parent, "Search", "ArticulateList");
                        //set the template name to specify which MVC Action to execute on the tags controller
                        contentRequest.SetTemplate(new Template("", "Search", "Search"));
                        return true;
                    }
                }    

                //if it ends with '/tags'
                if (parts[parts.Length - 1].InvariantEquals("tags"))
                {
                    route = route.Substring(0, route.LastIndexOf("/tags", StringComparison.InvariantCultureIgnoreCase) + 1);
                    var parent = FindContent(contentRequest, route);
                    if (parent != null && parent.DocumentTypeAlias.InvariantEquals("Articulate"))
                    {
                        //ok, so we want to render the tag list page, we don't set  the template name so the
                        // Index MVC action will execute.
                        contentRequest.PublishedContent = new ArticulateVirtualPage(parent, "Tags", "ArticulateArchive");
                        //set the template name to specify which MVC Action to execute on the tags controller
                        contentRequest.SetTemplate(new Template("", "Tags", "Tags"));
                        return true;
                    }
                }    

                //if the 2nd last item is /tags
                if (parts.Length > 1 && parts[parts.Length - 2].InvariantEquals("tags"))
                {                    
                    route = route.Substring(0, route.LastIndexOf("/tags", StringComparison.InvariantCultureIgnoreCase) + 1);
                    var parent = FindContent(contentRequest, route);
                    if (parent != null && parent.DocumentTypeAlias.InvariantEquals("Articulate"))
                    {
                        //ok, so we want to render a tag page
                        var tagName = parts[parts.Length - 1];                        
                        contentRequest.PublishedContent = new ArticulateVirtualPage(parent, tagName, "ArticulateList", "tags/" + tagName);
                        //set the template name to specify which MVC Action to execute on the tags controller
                        contentRequest.SetTemplate(new Template("", "Tag", "Tag"));
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}